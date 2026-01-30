// File: ViewModels/Windows/ProjectExplorerVM.cs
using Agrovent.DAL.Entities.Components;
using Agrovent.DAL.Entities.Projects;
using Agrovent.DAL.Repositories;
using Agrovent.Infrastructure.Enums;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Tree;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Agrovent.Infrastructure.Commands;
using Agrovent.DAL;

namespace Agrovent.ViewModels.Windows
{
    public class AGR_ProjectExplorerVM : BaseViewModel
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AGR_ProjectExplorerVM> _logger;
        private readonly DataContext _context; // Для прямого доступа к Project и ProjectComponent

        public AGR_ProjectExplorerVM()
        {
                
        }
        public AGR_ProjectExplorerVM(IUnitOfWork unitOfWork, DataContext context, ILogger<AGR_ProjectExplorerVM> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            RootNodes = new ObservableCollection<object>(); // Корневые узлы: Unsorted, Products
        }

        #region Commands


        #region LoadProjectsCommand
        private ICommand _LoadProjectsCommand;
        public ICommand LoadProjectsCommand => _LoadProjectsCommand
            ??= new RelayCommand(OnLoadProjectsCommandExecuted, CanLoadProjectsCommandExecute);
        private bool CanLoadProjectsCommandExecute(object p) => true;
        private async void OnLoadProjectsCommandExecuted(object p)
        {
            await LoadProjectsAsync();
        }
        #endregion

        #region AddFolderCommand
        private ICommand _AddFolderCommand;
        public ICommand AddFolderCommand => _AddFolderCommand
            ??= new RelayCommand(OnAddFolderCommandExecuted, CanAddFolderCommandExecute);
        private bool CanAddFolderCommandExecute(object p) => SelectedNode != null && SelectedNode is AGR_ProjectNode; // Только в проекты можно добавлять папки
        private void OnAddFolderCommandExecuted(object p)
        {
            
            if (SelectedNode == null) return;
            if (SelectedNode is AGR_ProjectNode parent)
            {
                // Показать диалог ввода имени
                var folderName = PromptUserForName("Введите имя папки:");
                if (string.IsNullOrWhiteSpace(folderName)) return;

                // Создать новый узел в дереве
                var newFolderNode = new AGR_ProjectNode(folderName, parent, databaseId: null); // Инициализируем как null
                parent.AddChild(newFolderNode);

                // Создать сущность БД
                // Используем DatabaseId родителя для установки ParentId
                var parentDatabaseId = GetProjectIdFromNode(parent); // Это вернет Id родителя из БД или null для корня
                var newDbProject = new Project { Name = folderName, ParentId = parentDatabaseId };
                _context.Projects.Add(newDbProject); // Добавляем в контекст

                try
                {
                    _context.SaveChanges(); // Выполняем сохранение

                    // --- КРИТИЧЕСКОЕ ИЗМЕНЕНИЕ ---
                    // Теперь newDbProject.Id содержит значение, присвоенное БД
                    // Обновляем DatabaseId узла дерева
                    newFolderNode.DatabaseId = newDbProject.Id;
                    _logger.LogDebug($"Добавлен проект в БД с Id: {newDbProject.Id}, обновлен узел дерева {newFolderNode.Name}.");
                    // --- КОНЕЦ КРИТИЧЕСКОГО ИЗМЕНЕНИЯ ---

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при добавлении папки в БД.");
                    // Откатить изменения в дереве или показать ошибку
                    parent.RemoveChild(newFolderNode); // Удаляем узел из дерева
                                                       // Важно: сама сущность newDbProject может остаться в контексте в состоянии Added/Error,
                                                       // но так как транзакция не используется в этом фрагменте, изменения в БД не будут откачены автоматически.
                                                       // EF не откатывает транзакции при исключениях автоматически, если вы не управляете транзакцией вручную.
                                                       // В реальном приложении может потребоваться явное управление транзакцией.
                }
            }
        }
        #endregion

        #region DeleteNodeCommand
        private ICommand _DeleteNodeCommand;
        public ICommand DeleteNodeCommand => _DeleteNodeCommand
            ??= new RelayCommand(OnDeleteNodeCommandExecuted, CanDeleteNodeCommandExecute);
        private bool CanDeleteNodeCommandExecute(object? node) => node != null && GetParentNode(node) != null; // Не удаляем корни
        private void OnDeleteNodeCommandExecuted(object? node)
        {
            if (node == null) return;

            var parent = GetParentNode(node);
            if (parent == null) return; // Не удаляем корневые узлы

            // Подтвердить удаление (по желанию)
            if (!ConfirmDeletion($"Удалить {GetNodeName(node)}?"))
            {
                return;
            }

            // Удалить из дерева
            parent.RemoveChild(node);

            // Удалить из БД
            if (node is AGR_ProjectNode projNode)
            {
                var dbProjectId = GetProjectIdFromNode(projNode); // Нужно получить Id проекта из БД
                if (dbProjectId.HasValue)
                {
                    var dbProject = _context.Projects.Find(dbProjectId.Value);
                    if (dbProject != null)
                    {
                        _context.Projects.Remove(dbProject);
                    }
                }
            }
            else if (node is AGR_ComponentNode compNode)
            {
                // Найти связь ProjectComponent в БД и удалить её
                var compVerId = compNode.ComponentVersion.Id;
                var projectId = GetProjectIdFromNode(parent); // Id проекта, из которого удаляется компонент
                if (projectId.HasValue)
                {
                    var projectCompLink = _context.ProjectComponents
                        .FirstOrDefault(pc => pc.ComponentVersionId == compVerId && pc.ProjectId == projectId.Value);

                    if (projectCompLink != null)
                    {
                        _context.ProjectComponents.Remove(projectCompLink);
                    }
                }
            }

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении узла из БД.");
                // Откатить изменения в дереве или показать ошибку
                parent.AddChild(node); // Простой откат - добавить обратно
            }
        }
        #endregion

        #region MoveNodeCommand
        private ICommand _MoveNodeCommand;
        public ICommand MoveNodeCommand => _MoveNodeCommand
            ??= new RelayCommand<(object node, AGR_ProjectNode targetProject)>(OnMoveNodeCommandExecuted, CanMoveNodeCommandExecute);
        private bool CanMoveNodeCommandExecute((object node, AGR_ProjectNode targetProject) parameters)
        {
            var (node, targetProject) = parameters;
            // Проверяем, что узел не перемещается в себя или в потомка
            return node != null && targetProject != null && node != targetProject && !IsDescendantOf(node, targetProject);
        }

        private void OnMoveNodeCommandExecuted((object node, AGR_ProjectNode targetProject) parameters)

        {
            var (node, targetProject) = parameters;
            if (node == null || targetProject == null) return;

            var sourceParent = GetParentNode(node);
            if (sourceParent == null) return; // Не перемещаем корни

            // Проверить, что перемещение возможно (не в себя или в потомка)
            if (node == targetProject || IsDescendantOf(node, targetProject))
            {
                _logger.LogWarning("Невозможно переместить узел в себя или в потомка.");
                return;
            }

            // Удалить из старого родителя
            sourceParent.RemoveChild(node);

            // Добавить к новому родителю
            targetProject.AddChild(node);

            // Обновить связи в БД
            if (node is AGR_ComponentNode compNode)
            {
                var compVerId = compNode.ComponentVersion.Id;
                var oldProjectId = GetProjectIdFromNode(sourceParent);
                var newProjectId = GetProjectIdFromNode(targetProject);

                // Удалить старую связь
                if (oldProjectId.HasValue)
                {
                    var oldLink = _context.ProjectComponents
                        .FirstOrDefault(pc => pc.ComponentVersionId == compVerId && pc.ProjectId == oldProjectId.Value);
                    if (oldLink != null)
                    {
                        _context.ProjectComponents.Remove(oldLink);
                    }
                }

                // Создать новую связь
                if (newProjectId.HasValue)
                {
                    var newLink = new ProjectComponent { ComponentVersionId = compVerId, ProjectId = newProjectId.Value };
                    _context.ProjectComponents.Add(newLink);
                }
            }
            else if (node is AGR_ProjectNode projNode)
            {
                // Обновить ParentId в БД для проекта
                var dbProjectId = GetProjectIdFromNode(projNode);
                var newParentDbProjectId = GetProjectIdFromNode(targetProject);
                if (dbProjectId.HasValue)
                {
                    var dbProject = _context.Projects.Find(dbProjectId.Value);
                    if (dbProject != null)
                    {
                        dbProject.ParentId = newParentDbProjectId; // Может быть null, если целевой проект - корень
                    }
                }
            }

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при перемещении узла в БД.");
                // Откатить изменения в дереве
                targetProject.RemoveChild(node);
                sourceParent.AddChild(node);
            }
        }
        #endregion

        #region RenameNodeCommand
        private ICommand _RenameNodeCommand;
        public ICommand RenameNodeCommand => _RenameNodeCommand
            ??= new RelayCommand(OnRenameNodeCommandExecuted, CanRenameNodeCommandExecute);
        private bool CanRenameNodeCommandExecute(object? node) => node is AGR_ProjectNode; // Пока только проекты можно переименовывать
        private void OnRenameNodeCommandExecuted(object? node)
        {
            if (node == null) return;

            var newName = PromptUserForName("Введите новое имя:", GetNodeName(node));
            if (string.IsNullOrWhiteSpace(newName) || newName == GetNodeName(node)) return;

            var oldName = GetNodeName(node);
            SetNodeName(node, newName);

            // Обновить в БД
            if (node is AGR_ProjectNode projNode)
            {
                var dbProjectId = GetProjectIdFromNode(projNode);
                if (dbProjectId.HasValue)
                {
                    var dbProject = _context.Projects.Find(dbProjectId.Value);
                    if (dbProject != null)
                    {
                        dbProject.Name = newName;
                    }
                }
            }
            // AGR_ComponentNode обычно не переименовывается, его Name берётся из ComponentVersion
            // Если нужно, можно добавить логику для изменения Name в ComponentVersion, но это влияет на историю

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при переименовании узла в БД.");
                // Откатить изменения в дереве
                SetNodeName(node, oldName);
            }
        }
        #endregion

        #region SelectNodeCommand
        private ICommand? _SelectNodeCommand;
        public ICommand SelectNodeCommand => _SelectNodeCommand
            ??= new RelayCommand<object>(OnSelectNodeCommandExecuted, CanSelectNodeCommandExecute);
        private bool CanSelectNodeCommandExecute(object? p) => true; // Пока разрешаем выбор любого узла
        private void OnSelectNodeCommandExecuted(object? selectedNode)
        {
            var node = (selectedNode as System.Windows.RoutedPropertyChangedEventArgs<object>).NewValue;

            SelectedNode = node; // Просто обновляем свойство SelectedNode

            // Здесь можно добавить дополнительную логику при выборе узла
            // Например, обновить панель свойств, открыть предпросмотр и т.д.
            if (SelectedNode is AGR_ProjectNode projNode)
            {
                System.Diagnostics.Debug.WriteLine($"Выбран проект: {projNode.Name}");
            }
            else if (SelectedNode is AGR_ComponentNode compNode)
            {
                System.Diagnostics.Debug.WriteLine($"Выбран компонент: {compNode.Name}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Выбран неизвестный узел или ничего не выбрано.");
            }
        }
        #endregion

        #endregion

        #region PROPS
        public ObservableCollection<object> RootNodes { get; }

        private AGR_ProjectNode? _unsortedNode;
        private AGR_ProjectNode? _productsNode;

        private object? _selectedNode;
        public object? SelectedNode
        {
            get => _selectedNode;
            set => Set(ref _selectedNode, value);
        }

        #endregion

        #region Methods
        private async Task LoadProjectsAsync2()
        {
            try
            {
                _logger.LogInformation("Загрузка проектов и компонентов для проводника...");

                // Очищаем старые узлы
                RootNodes.Clear();
                _unsortedNode = null;
                _productsNode = null;

                // 1. Загружаем корневые проекты (ParentId == null)
                var rootProjects = await _context.Projects
                    .Where(p => p.ParentId == null)
                    .ToListAsync();

                // 2. Создаем корневые узлы
                _unsortedNode = new AGR_ProjectNode("Несортированные компоненты");
                _productsNode = new AGR_ProjectNode("Продукция");

                // 3. Загружаем *все* компоненты верхнего уровня (не входящие в проекты)
                // Предположим, что "верхний уровень" означает сборки, не связанные с проектами
                // Или все компоненты, не связанные с проектами (в зависимости от логики)
                // Для "Несортированные" - загрузим все *сборки* верхнего уровня, не входящие в проекты
                var unsortedAssemblies = await _unitOfWork.ComponentRepository.GetTopLevelAssembliesNotInProjectsAsync(); // Нужно реализовать в репозитории

                foreach (var compVer in unsortedAssemblies)
                {
                    var compNode = new AGR_ComponentNode(compVer, _unsortedNode);
                    _unsortedNode.AddChild(compNode);
                }

                // 4. Загружаем проект "Продукция" и его структуру
                // Ищем проект "Продукция" среди корневых
                var productsDbProject = rootProjects.FirstOrDefault(p => p.Name == "Продукция"); // Предполагаем, что он есть или создаем при необходимости
                if (productsDbProject != null)
                {
                    var productsTreeNode = BuildProjectTree(productsDbProject);
                    _productsNode = productsTreeNode;
                }
                else
                {
                    // Если "Продукция" не существует в БД, создаем пустой узел
                    _productsNode = new AGR_ProjectNode("Продукция");
                }

                // 5. Добавляем корневые узлы в RootNodes
                RootNodes.Add(_unsortedNode);
                RootNodes.Add(_productsNode);

                _logger.LogInformation("Проекты и компоненты загружены.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке проектов и компонентов.");
                // Обработка ошибки
            }
        }

        private async Task LoadProjectsAsync()
        {
            try
            {
                _logger.LogInformation("Загрузка проектов и компонентов для проводника...");

                // Очищаем старые узлы
                RootNodes.Clear();
                _unsortedNode = null;
                _productsNode = null;

                
                var rootProjects = await _context.Projects
                    .Include(p => p.Children) // Загружаем первый уровень детей
                        .ThenInclude(c => c.ProjectComponents) // Загружаем компоненты для детей
                            .ThenInclude(pc => pc.ComponentVersion) // Загружаем саму версию компонента
                    .Include(p => p.Children) // Загружаем первый уровень детей снова (если нужно больше уровней, нужен другой подход)
                        .ThenInclude(c => c.Children) // Загружаем детей детей (и т.д. для глубокой иерархии)
                            .ThenInclude(gc => gc.ProjectComponents) // И их компоненты
                                .ThenInclude(pc => pc.ComponentVersion)
                    .Include(p => p.ProjectComponents) // Загружаем компоненты для корневых проектов
                        .ThenInclude(pc => pc.ComponentVersion) // Загружаем саму версию компонента
                    .Where(p => p.ParentId == null) // Фильтруем корневые
                    .ToListAsync();

                
                _unsortedNode = new AGR_ProjectNode("Несортированные компоненты", databaseId: null); // У корня нет Id в БД

                
                var productsDbProject = rootProjects.FirstOrDefault(p => p.Name == "Продукция");
                if (productsDbProject != null)
                {
                    _productsNode = BuildProjectTree(productsDbProject); // Теперь BuildProjectTree получает полностью загруженный объект
                }
                else
                {
                    _productsNode = new AGR_ProjectNode("Продукция", databaseId: null); // Создаем пустой узел
                }


                var unsortedAssemblies = await _unitOfWork.ComponentRepository.GetTopLevelAssembliesNotInProjectsAsync();
                foreach (var compVer in unsortedAssemblies)
                {
                    var compNode = new AGR_ComponentNode(compVer, _unsortedNode);
                    _unsortedNode.AddChild(compNode);
                }

                // Добавляем корневые узлы в RootNodes
                RootNodes.Add(_unsortedNode);
                RootNodes.Add(_productsNode);

                _logger.LogInformation("Проекты и компоненты загружены.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке проектов и компонентов.");
                // Обработка ошибки
            }
        }

        // Рекурсивный метод для построения дерева из сущностей БД
        private AGR_ProjectNode BuildProjectTree(Project dbProject)
        {
            var treeNode = new AGR_ProjectNode(dbProject.Name, databaseId: dbProject.Id);

            // Загружаем компоненты, связанные с этим проектом
            var projectCompIds = dbProject.ProjectComponents.Select(pc => pc.ComponentVersionId).ToList();
            var componentVersions = _context.ComponentVersions
                .Where(cv => projectCompIds.Contains(cv.Id))
                .ToList();

            foreach (var compVer in componentVersions)
            {
                var compNode = new AGR_ComponentNode(compVer, treeNode);
                treeNode.AddChild(compNode);
            }

            // Загружаем дочерние проекты и рекурсивно строим их деревья
            foreach (var childDbProject in dbProject.Children)
            {
                var childTreeNode = BuildProjectTree(childDbProject);
                childTreeNode.Parent = treeNode; // Устанавливаем родителя для вновь созданного узла
                treeNode.AddChild(childTreeNode);
            }

            return treeNode;
        }

        // Вспомогательные методы

        private AGR_ProjectNode? GetParentNode(object node)
        {
            return node switch
            {
                AGR_ProjectNode pn => pn.Parent,
                AGR_ComponentNode cn => cn.Parent,
                _ => null
            };
        }

        private string GetNodeName(object node)
        {
            return node switch
            {
                AGR_ProjectNode pn => pn.Name,
                AGR_ComponentNode cn => cn.Name,
                _ => ""
            };
        }

        private void SetNodeName(object node, string newName)
        {
            switch (node)
            {
                case AGR_ProjectNode pn:
                    pn.Name = newName;
                    break;
                case AGR_ComponentNode cn:
                    cn.Name = newName;
                    break;
            }
        }

        private int? GetProjectIdFromNode(AGR_ProjectNode node)
        {
            return node.DatabaseId;
        }

        private bool IsDescendantOf(object potentialChild, AGR_ProjectNode potentialAncestor)
        {
            var current = GetParentNode(potentialChild) as object;
            while (current != null)
            {
                if (current == potentialAncestor) return true;
                current = GetParentNode(current);
            }
            return false;
        }

        // Простые методы для ввода/подтверждения (замените на реальные диалоги)
        private string? PromptUserForName(string prompt, string defaultValue = "")
        {
            // Реализуйте диалог ввода (например, через MessageBox.Show с TextBox или кастомное окно)
            // Пока возвращаем фиктивное значение
            return defaultValue == "" ? "Новая папка" : defaultValue;
        }

        private bool ConfirmDeletion(string message)
        {
            // Реализуйте диалог подтверждения (MessageBox.Show)
            // Пока возвращаем true
            return true;
        } 
        #endregion
    }
}