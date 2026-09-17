// File: ViewModels/Windows/ProjectExplorerVM.cs
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Agrovent.DAL;
using Agrovent.Infrastructure.Commands;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Tree;
using Agrovent.Views.Windows;
using AgroventInfrastructure.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Windows
{
    /// <summary>
    /// VM "проводника" сохранённых в БД изделий.
    ///
    /// Принцип работы:
    ///  - "Несортированное" — виртуальный корень (в БД не хранится). В нём показываются
    ///    сборки верхнего уровня (не являющиеся чьей-то подсборкой), у которых нет
    ///    ни одной связи ProjectComponent — т.е. они ещё никуда не отсортированы.
    ///  - Остальные корневые узлы — это реальные Project с ParentId == null. Пользователь
    ///    создаёт их сам (кнопка "Новый проект"), сколько угодно и с любыми именами —
    ///    никакой захардкоженной папки "Продукция" больше нет.
    ///  - Перемещение (drag) компонента убирает старую связь ProjectComponent и создаёт новую.
    ///  - Копирование (Ctrl+drag) добавляет новую связь, не трогая старую — так одна и та же
    ///    сборка может отображаться сразу в нескольких проектах (ProjectComponent — это M:N).
    ///  - Все операции с БД выполняются через собственный scope (IServiceScopeFactory.CreateScope()
    ///    на каждую операцию), а не через один DataContext на всё время жизни окна.
    /// </summary>
    public class AGR_ProjectExplorerVM : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AGR_ProjectExplorerVM> _logger;

        private AGR_ProjectNode? _unsortedNode;

        // Конструктор для дизайнера XAML
        public AGR_ProjectExplorerVM()
        {
            RootNodes = new ObservableCollection<object>();
        }

        public AGR_ProjectExplorerVM(IServiceScopeFactory scopeFactory, ILogger<AGR_ProjectExplorerVM> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            RootNodes = new ObservableCollection<object>();
        }

        #region PROPS

        public ObservableCollection<object> RootNodes { get; }

        private object? _selectedNode;
        public object? SelectedNode
        {
            get => _selectedNode;
            set => Set(ref _selectedNode, value);
        }

        #endregion

        #region Commands

        #region LoadProjectsCommand
        private ICommand? _LoadProjectsCommand;
        public ICommand LoadProjectsCommand => _LoadProjectsCommand
            ??= new RelayCommand(async _ => await LoadProjectsAsync(), _ => !IsLoading);
        #endregion

        #region AddFolderCommand (создание нового проекта - в корне или вложенным в выбранный)
        private ICommand? _AddFolderCommand;
        public ICommand AddFolderCommand => _AddFolderCommand
            ??= new RelayCommand(async p => await OnAddFolderCommandExecutedAsync(p), CanAddFolderCommandExecute);

        private bool CanAddFolderCommandExecute(object p) => !IsLoading;

        private async Task OnAddFolderCommandExecutedAsync(object p)
        {
            // Если выбран реальный проект - создаём вложенный, иначе (ничего не выбрано
            // или выбрано "Несортированное") - создаём новый проект в корне.
            var parent = SelectedNode as AGR_ProjectNode;
            if (parent != null && parent.IsUnsortedRoot) parent = null;

            var folderName = PromptUserForName("Введите имя нового проекта:");
            if (string.IsNullOrWhiteSpace(folderName)) return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                var newDbProject = new Project { Name = folderName, ParentId = parent?.DatabaseId };
                dataContext.Projects.Add(newDbProject);
                await dataContext.SaveChangesAsync();

                var newNode = new AGR_ProjectNode(folderName, parent, newDbProject.Id);
                if (parent != null)
                    parent.AddChild(newNode);
                else
                    RootNodes.Add(newNode);

                SelectedNode = newNode;
                _logger.LogInformation("Создан проект '{Name}' (Id={Id}).", folderName, newDbProject.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании проекта '{Name}'.", folderName);
                ShowError("Не удалось создать проект. Подробности в логе.");
            }
        }
        #endregion

        #region RenameNodeCommand (только для реальных проектов, "Несортированное" не переименовывается)
        private ICommand? _RenameNodeCommand;
        public ICommand RenameNodeCommand => _RenameNodeCommand
            ??= new RelayCommand(async p => await OnRenameNodeCommandExecutedAsync(p as AGR_ProjectNode), CanRenameNodeCommandExecute);

        private bool CanRenameNodeCommandExecute(object? node) => node is AGR_ProjectNode pn && !pn.IsUnsortedRoot && !IsLoading;

        private async Task OnRenameNodeCommandExecutedAsync(AGR_ProjectNode? projNode)
        {
            if (projNode == null || projNode.DatabaseId is not int dbId) return;

            var newName = PromptUserForName("Введите новое имя проекта:", projNode.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == projNode.Name) return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                var dbProject = await dataContext.Projects.FindAsync(dbId);
                if (dbProject == null) return;

                dbProject.Name = newName;
                await dataContext.SaveChangesAsync();

                projNode.Name = newName;
                _logger.LogInformation("Проект Id={Id} переименован в '{Name}'.", dbId, newName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переименовании проекта Id={Id}.", dbId);
                ShowError("Не удалось переименовать проект. Подробности в логе.");
            }
        }
        #endregion

        #region DeleteNodeCommand
        private ICommand? _DeleteNodeCommand;
        public ICommand DeleteNodeCommand => _DeleteNodeCommand
            ??= new RelayCommand(async p => await OnDeleteNodeCommandExecutedAsync(p), CanDeleteNodeCommandExecute);

        private bool CanDeleteNodeCommandExecute(object? node)
        {
            if (IsLoading || node == null) return false;

            var parent = GetParentNode(node);
            return node switch
            {
                // Проект нельзя удалить только если это виртуальное "Несортированное"
                AGR_ProjectNode pn => !pn.IsUnsortedRoot,
                // Компонент можно убрать только из настоящего проекта - в "Несортированном"
                // нет самой связи ProjectComponent, убирать нечего
                AGR_ComponentNode when parent is AGR_ProjectNode pp => !pp.IsUnsortedRoot,
                _ => false
            };
        }

        private async Task OnDeleteNodeCommandExecutedAsync(object? node)
        {
            if (node == null) return;
            var parent = GetParentNode(node); // null для корневого проекта - это нормально

            var message = node switch
            {
                AGR_ProjectNode pn when pn.Children.Count > 0 =>
                    $"Проект «{pn.Name}» содержит вложенные элементы. Удалить проект вместе с ними?\n" +
                    "Сами сохранённые сборки/детали из базы данных удалены не будут - " +
                    "пропадёт только их связь с этим проектом.",
                AGR_ProjectNode pn => $"Удалить проект «{pn.Name}»?",
                _ => $"Убрать «{GetNodeName(node)}» из этого проекта?\nСборка останется в базе данных."
            };

            if (!ConfirmDeletion(message)) return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                if (node is AGR_ProjectNode projNode && projNode.DatabaseId is int dbProjectId)
                {
                    var dbProject = await dataContext.Projects.FindAsync(dbProjectId);
                    if (dbProject != null)
                    {
                        // Каскад на ParentId/ProjectId (см. миграцию ProjectsTree) сам удалит
                        // дочерние Project и ProjectComponent. ComponentVersion не затрагивается.
                        dataContext.Projects.Remove(dbProject);
                        await dataContext.SaveChangesAsync();
                    }
                }
                else if (node is AGR_ComponentNode compNode && parent is AGR_ProjectNode parentProj
                         && parentProj.DatabaseId is int parentProjectId)
                {
                    var link = await dataContext.ProjectComponents.FirstOrDefaultAsync(pc =>
                        pc.ComponentVersionId == compNode.ComponentVersion.Id && pc.ProjectId == parentProjectId);

                    if (link != null)
                    {
                        dataContext.ProjectComponents.Remove(link);
                        await dataContext.SaveChangesAsync();
                    }
                }

                if (parent != null) parent.RemoveChild(node);
                else if (node is AGR_ProjectNode rootProjNode) RootNodes.Remove(rootProjNode);

                // Если убрали компонент из проекта - он мог "вернуться" в Несортированное
                if (node is AGR_ComponentNode)
                {
                    await RefreshUnsortedNodeAsync();
                }

                _logger.LogInformation("Узел '{Name}' удалён.", GetNodeName(node));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении узла '{Name}'.", GetNodeName(node));
                ShowError("Не удалось удалить элемент. Подробности в логе.");
            }
        }
        #endregion

        #region MoveNodeCommand (drag - перенос: убирает старую связь и создаёт новую / переносит проект)
        private ICommand? _MoveNodeCommand;
        public ICommand MoveNodeCommand => _MoveNodeCommand
            ??= new RelayCommand<(object node, AGR_ProjectNode targetProject)>(
                async p => await OnMoveNodeCommandExecutedAsync(p), CanMoveNodeCommandExecute);

        private bool CanMoveNodeCommandExecute((object node, AGR_ProjectNode targetProject) p)
        {
            var (node, targetProject) = p;
            if (IsLoading || node == null || targetProject == null) return false;
            if (ReferenceEquals(node, targetProject)) return false;

            var sourceParent = GetParentNode(node);
            if (ReferenceEquals(sourceParent, targetProject)) return false; // уже там

            if (node is AGR_ProjectNode projNode)
            {
                // Проект нельзя "разсортировать" и нельзя переместить в себя/своего потомка
                if (targetProject.IsUnsortedRoot) return false;
                if (IsSameOrDescendant(targetProject, projNode)) return false;
            }

            return true;
        }

        private async Task OnMoveNodeCommandExecutedAsync((object node, AGR_ProjectNode targetProject) p)
        {
            var (node, targetProject) = p;
            var sourceParent = GetParentNode(node);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                if (node is AGR_ComponentNode compNode)
                {
                    var compVerId = compNode.ComponentVersion.Id;
                    var oldProjectId = sourceParent?.DatabaseId; // null - был в "Несортированном"
                    var newProjectId = targetProject.DatabaseId; // null - переносим в "Несортированное"

                    if (oldProjectId.HasValue)
                    {
                        var oldLink = await dataContext.ProjectComponents.FirstOrDefaultAsync(pc =>
                            pc.ComponentVersionId == compVerId && pc.ProjectId == oldProjectId.Value);
                        if (oldLink != null) dataContext.ProjectComponents.Remove(oldLink);
                    }

                    if (newProjectId.HasValue)
                    {
                        var alreadyLinked = await dataContext.ProjectComponents.AnyAsync(pc =>
                            pc.ComponentVersionId == compVerId && pc.ProjectId == newProjectId.Value);
                        if (!alreadyLinked)
                        {
                            dataContext.ProjectComponents.Add(new ProjectComponent
                            {
                                ComponentVersionId = compVerId,
                                ProjectId = newProjectId.Value
                            });
                        }
                    }

                    await dataContext.SaveChangesAsync();

                    sourceParent?.RemoveChild(node);
                    if (targetProject.IsUnsortedRoot)
                        await RefreshUnsortedNodeAsync(); // содержимое "Несортированного" всегда перечитывается
                    else
                        targetProject.AddChild(node);
                }
                else if (node is AGR_ProjectNode projNode && projNode.DatabaseId is int dbProjectId)
                {
                    var dbProject = await dataContext.Projects.FindAsync(dbProjectId);
                    if (dbProject == null) return;

                    dbProject.ParentId = targetProject.DatabaseId;
                    await dataContext.SaveChangesAsync();

                    sourceParent?.RemoveChild(node);
                    targetProject.AddChild(node);
                }

                SelectedNode = node;
                _logger.LogInformation("Узел '{Name}' перемещён в '{Target}'.", GetNodeName(node), targetProject.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при перемещении узла '{Name}'.", GetNodeName(node));
                ShowError("Не удалось переместить элемент. Подробности в логе.");
                await LoadProjectsAsync(); // безопасный способ привести дерево в консистентное состояние
            }
        }
        #endregion

        #region CopyComponentToProjectCommand (Ctrl+drag - копирование: сборка появляется ещё в одном проекте)
        private ICommand? _CopyComponentToProjectCommand;
        public ICommand CopyComponentToProjectCommand => _CopyComponentToProjectCommand
            ??= new RelayCommand<(AGR_ComponentNode node, AGR_ProjectNode targetProject)>(
                async p => await OnCopyComponentToProjectCommandExecutedAsync(p), CanCopyComponentToProjectCommandExecute);

        private bool CanCopyComponentToProjectCommandExecute((AGR_ComponentNode node, AGR_ProjectNode targetProject) p)
        {
            var (node, targetProject) = p;
            if (IsLoading || node == null || targetProject == null) return false;
            if (targetProject.IsUnsortedRoot) return false; // копировать "в несортированное" бессмысленно
            if (ReferenceEquals(GetParentNode(node), targetProject)) return false; // уже там
            return true;
        }

        private async Task OnCopyComponentToProjectCommandExecutedAsync((AGR_ComponentNode node, AGR_ProjectNode targetProject) p)
        {
            var (node, targetProject) = p;
            if (targetProject.DatabaseId is not int targetProjectId) return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                var compVerId = node.ComponentVersion.Id;
                var alreadyLinked = await dataContext.ProjectComponents.AnyAsync(pc =>
                    pc.ComponentVersionId == compVerId && pc.ProjectId == targetProjectId);

                if (alreadyLinked)
                {
                    _logger.LogDebug("Компонент '{Name}' уже есть в проекте '{Project}'.", node.Name, targetProject.Name);
                    return;
                }

                dataContext.ProjectComponents.Add(new ProjectComponent
                {
                    ComponentVersionId = compVerId,
                    ProjectId = targetProjectId
                });
                await dataContext.SaveChangesAsync();

                // Исходный узел не трогаем - добавляем второй, независимый узел на тот же ComponentVersion
                targetProject.AddChild(new AGR_ComponentNode(node.ComponentVersion, targetProject));

                // Если исходный узел был в "Несортированном" - он должен из него пропасть,
                // т.к. теперь у сборки появилась связь хотя бы с одним проектом
                if (GetParentNode(node) is AGR_ProjectNode sourceParent && sourceParent.IsUnsortedRoot)
                {
                    await RefreshUnsortedNodeAsync();
                }

                _logger.LogInformation("Компонент '{Name}' скопирован в проект '{Project}'.", node.Name, targetProject.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при копировании компонента '{Name}' в проект '{Project}'.", node.Name, targetProject.Name);
                ShowError("Не удалось скопировать элемент в проект. Подробности в логе.");
            }
        }
        #endregion

        #region OpenComponentCommand (двойной клик или пункт меню "Открыть" - открыть в SolidWorks и закрыть проводник)
        private ICommand? _OpenComponentCommand;
        public ICommand OpenComponentCommand => _OpenComponentCommand
            ??= new RelayCommand<AGR_ComponentNode>(OnOpenComponentCommandExecuted, CanOpenComponentCommandExecute);

        private bool CanOpenComponentCommandExecute(AGR_ComponentNode? node)
            => node != null && !string.IsNullOrEmpty(node.StoragePath) && File.Exists(node.StoragePath);

        private void OnOpenComponentCommandExecuted(AGR_ComponentNode? node)
        {
            if (node?.StoragePath == null) return;

            try
            {
                var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    _logger.LogError("Не удалось получить ISwApplication для открытия компонента.");
                    return;
                }

                var openDoc = swApp.Documents.FirstOrDefault(x => x.Path == node.StoragePath);
                if (openDoc != null)
                {
                    swApp.Documents.Active = openDoc as ISwDocument;
                }
                else
                {
                    var newDoc = swApp.Documents.PreCreateFromPath(node.StoragePath);
                    newDoc.Commit(CancellationToken.None);
                }

                // Сборка открылась в SW - проводник больше не нужен, закрываем его
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии компонента '{Name}' из проводника.", node.Name);
                ShowError("Не удалось открыть файл. Подробности в логе.");
            }
        }
        #endregion

        #region CopyToProjectCommand (пункт меню "Скопировать в проект..." - выбор проекта через диалог)
        private ICommand? _CopyToProjectCommand;
        public ICommand CopyToProjectCommand => _CopyToProjectCommand
            ??= new RelayCommand<AGR_ComponentNode>(OnCopyToProjectCommandExecuted, node => node != null && !IsLoading);

        private void OnCopyToProjectCommandExecuted(AGR_ComponentNode? node)
        {
            if (node == null) return;

            // Собираем все реальные проекты (без "Несортированного") из уже загруженного дерева -
            // отдельный запрос к БД тут не нужен, дерево и так в памяти.
            var projectRoots = RootNodes.OfType<AGR_ProjectNode>().Where(p => !p.IsUnsortedRoot).ToList();

            if (projectRoots.Count == 0)
            {
                ShowError("Сначала создайте хотя бы один проект («Новый проект» на панели инструментов).");
                return;
            }

            var pickerVm = new AGR_ProjectPickerVM(projectRoots);
            var pickerView = new AGR_ProjectPickerView { DataContext = pickerVm };
            pickerView.ShowDialog();

            if (pickerVm.IsDialogResultAccepted == true && pickerVm.SelectedProject != null)
            {
                // Тот же самый механизм копирования, что и при Ctrl+drag
                CopyComponentToProjectCommand.Execute((node, pickerVm.SelectedProject));
            }
        }
        #endregion

        #region SelectNodeCommand
        private ICommand? _SelectNodeCommand;
        public ICommand SelectNodeCommand => _SelectNodeCommand
            ??= new RelayCommand<object>(OnSelectNodeCommandExecuted);

        private void OnSelectNodeCommandExecuted(object? p)
        {
            if (p is RoutedPropertyChangedEventArgs<object> args)
            {
                SelectedNode = args.NewValue;
            }
            else
            {
                SelectedNode = p;
            }
        }
        #endregion

        /// <summary>Поднимается, когда проводник пора закрыть (например, после успешного открытия сборки).
        /// Подписка выполняется снаружи, в AGR_CommandService, тем же способом, что и у других диалогов проекта
        /// (см. AGR_SelectAvaArticleVM.CloseRequested).</summary>
        public event EventHandler? CloseRequested;

        #endregion


        #region Methods

        /// <summary>Полная перезагрузка дерева. Не блокирует поток - вызывается без await
        /// сразу после открытия окна, пока показывается оверлей IsLoading.</summary>
        public async Task LoadProjectsAsync()
        {
            IsLoading = true;
            LoadingStatus = "Загрузка проектов...";

            try
            {
                _logger.LogInformation("Загрузка проектов и компонентов для проводника...");

                RootNodes.Clear();

                using var scope = _scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                // 1. Виртуальный корень "Несортированное"
                _unsortedNode = new AGR_ProjectNode("Несортированное", isUnsortedRoot: true);
                var unsortedAssemblies = await unitOfWork.ComponentRepository.GetTopLevelAssembliesNotInProjectsAsync();
                foreach (var compVer in unsortedAssemblies.OrderBy(c => c.Name))
                {
                    _unsortedNode.AddChild(new AGR_ComponentNode(compVer, _unsortedNode));
                }
                RootNodes.Add(_unsortedNode);

                // 2. Реальные проекты. Забираем все одним запросом (их обычно немного) и строим
                //    дерево в памяти - так поддерживается произвольная глубина вложенности без
                //    "лесенки" из Include/ThenInclude под конкретное число уровней.
                var allProjects = await dataContext.Projects
                    .Include(pr => pr.ProjectComponents)
                        .ThenInclude(pc => pc.ComponentVersion)
                            .ThenInclude(cv => cv.Component)
                    .Include(pr => pr.ProjectComponents)
                        .ThenInclude(pc => pc.ComponentVersion)
                            .ThenInclude(cv => cv.Files)
                    .AsNoTracking()
                    .ToListAsync();

                var byParent = allProjects.ToLookup(pr => pr.ParentId);

                foreach (var rootDbProject in byParent[null].OrderBy(pr => pr.Name))
                {
                    RootNodes.Add(BuildProjectNode(rootDbProject, null, byParent));
                }

                _logger.LogInformation("Дерево проектов загружено: {Count} корневых проектов.", byParent[null].Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке проектов и компонентов.");
                ShowError("Не удалось загрузить дерево проектов. Подробности в логе.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static AGR_ProjectNode BuildProjectNode(Project dbProject, AGR_ProjectNode? parent, ILookup<int?, Project> byParent)
        {
            var treeNode = new AGR_ProjectNode(dbProject.Name, parent, dbProject.Id);

            foreach (var pc in dbProject.ProjectComponents.OrderBy(x => x.ComponentVersion.Name))
            {
                treeNode.AddChild(new AGR_ComponentNode(pc.ComponentVersion, treeNode));
            }

            foreach (var childDbProject in byParent[dbProject.Id].OrderBy(pr => pr.Name))
            {
                treeNode.AddChild(BuildProjectNode(childDbProject, treeNode, byParent));
            }

            return treeNode;
        }

        /// <summary>Перечитывает содержимое виртуального узла "Несортированное" из репозитория.
        /// Не пытается точечно добавлять/убирать один узел, т.к. содержимое этого узла - это
        /// не структура из БД, а результат вычисляемого запроса.</summary>
        private async Task RefreshUnsortedNodeAsync()
        {
            if (_unsortedNode == null) return;

            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var unsortedAssemblies = await unitOfWork.ComponentRepository.GetTopLevelAssembliesNotInProjectsAsync();

            _unsortedNode.Children.Clear();
            foreach (var compVer in unsortedAssemblies.OrderBy(c => c.Name))
            {
                _unsortedNode.AddChild(new AGR_ComponentNode(compVer, _unsortedNode));
            }
        }

        private static AGR_ProjectNode? GetParentNode(object node)
        {
            return node switch
            {
                AGR_ProjectNode pn => pn.Parent,
                AGR_ComponentNode cn => cn.Parent,
                _ => null
            };
        }

        private static string GetNodeName(object node)
        {
            return node switch
            {
                AGR_ProjectNode pn => pn.Name,
                AGR_ComponentNode cn => cn.Name,
                _ => ""
            };
        }

        /// <summary>true, если candidate == potentialAncestor или candidate лежит внутри его поддерева.</summary>
        private static bool IsSameOrDescendant(AGR_ProjectNode candidate, AGR_ProjectNode potentialAncestor)
        {
            var current = candidate;
            while (current != null)
            {
                if (current == potentialAncestor) return true;
                current = current.Parent;
            }
            return false;
        }

        private static string? PromptUserForName(string prompt, string defaultValue = "")
        {
            // Простой встроенный диалог ввода строки (сборка Microsoft.VisualBasic уже
            // используется в проекте, см. ComponentRepository.cs) - минимально инвазивное решение.
            // При желании можно заменить на кастомное WPF-окно.
            var result = Microsoft.VisualBasic.Interaction.InputBox(prompt, "Agrovent", defaultValue);
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        }

        private static bool ConfirmDeletion(string message)
            => MessageBox.Show(message, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        private static void ShowError(string message)
            => MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

        #endregion
    }
}
