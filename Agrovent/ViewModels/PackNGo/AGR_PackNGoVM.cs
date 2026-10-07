using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Services;
using Agrovent.ViewModels.Base;
using Agrovent.Views.Windows;
using AgroventInfrastructure;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Interfaces.Components;
using AgroventInfrastructure.Interfaces.Components.Base;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.Documents.Extensions;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.PackNGo
{
    /// <summary>
    /// ViewModel диалога "Pack'n'Go" - расширенная замена штатного Pack and Go SolidWorks.
    /// Строит табличное дерево всех компонентов активной сборки (с сохранением вложенности),
    /// позволяет переименовать любой компонент ("Сохранить как"), пометить его как новый -
    /// партномер при этом просто сбрасывается в пустой (с автоматическим каскадным сбросом
    /// партномеров всех вышестоящих сборок), выбрать, какие компоненты копировать.
    /// Сам Pack'n'Go НЕ генерирует новые партномера и НЕ создаёт записи Component в БД - состав
    /// сборки в процессе проектирования ещё может измениться, поэтому занимать номер под деталь,
    /// которая, возможно, вообще не попадёт в финальную сборку, не нужно. Реальный партномер
    /// назначается позже, обычной командой "Присвоить новый партномер" (SetNewPartnumberCommand).
    /// Сохранение выполняется в два шага: 1) копирование файлов через нативный
    /// ISldWorks.CopyDocument (см. ExecuteCopyDocument), 2) простановка партномеров (в т.ч. пустых -
    /// для помеченных как новые) уже в СКОПИРОВАННЫЕ файлы и их сохранение (см. ApplyPartNumbersToCopy) -
    /// исходные документы при этом никогда не изменяются и не сохраняются.
    /// </summary>
    public class AGR_PackNGoVM : BaseViewModel
    {
        private readonly ISwApplication _swApp;
        private readonly ILogger<AGR_PackNGoVM> _logger;

        /// <summary>Все уникальные компоненты дерева (по одному на файл), независимо от того,
        /// сколько раз файл встречается в структуре сборки.</summary>
        public List<AGR_PackNGoComponentVM> AllComponents { get; } = new List<AGR_PackNGoComponentVM>();

        /// <summary>Плоский список строк, которые сейчас должны быть видны в таблице
        /// (с учётом свёрнутых/развёрнутых узлов). Именно он - ItemsSource таблицы.</summary>
        public ObservableCollection<AGR_PackNGoNodeVM> VisibleNodes { get; } = new ObservableCollection<AGR_PackNGoNodeVM>();

        public AGR_PackNGoNodeVM RootNode { get; }

        /// <summary>Результат диалога: true - пользователь нажал "Сохранить" и сохранение
        /// прошло успешно; false/null - отмена.</summary>
        public bool? DialogResult { get; private set; }

        // path -> общий ComponentVM (один на уникальный файл)
        private readonly Dictionary<string, AGR_PackNGoComponentVM> _componentVMsByPath =
            new Dictionary<string, AGR_PackNGoComponentVM>(StringComparer.OrdinalIgnoreCase);

        // componentVM -> множество ВСЕХ различных родителей (сборок), в которые он прямо входит
        // где-либо в дереве. Используется для каскадного распространения "новизны" наверх.
        private readonly Dictionary<AGR_PackNGoComponentVM, HashSet<AGR_PackNGoComponentVM>> _parentsOf =
            new Dictionary<AGR_PackNGoComponentVM, HashSet<AGR_PackNGoComponentVM>>();

        private bool _saveFolderManuallyEdited;

        #region CTOR

        public AGR_PackNGoVM(IAGR_BaseComponent rootComponent, ISwApplication swApp,
            ILogger<AGR_PackNGoVM> logger)
        {
            if (rootComponent == null) throw new ArgumentNullException(nameof(rootComponent));
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            _logger = logger;

            // Сама сборка (или деталь) тоже присутствует в таблице - первой строкой (п.1 ТЗ).
            var rootVM = GetOrCreateComponentVM(rootComponent);
            RootNode = new AGR_PackNGoNodeVM(rootVM, null, 0);
            RootNode.ExpandedChanged += Node_ExpandedChanged;

            if (rootComponent.SwDocument is ISwAssembly rootAssembly)
            {
                BuildChildrenFromRepo(rootAssembly.Configurations.Active.Components, RootNode);
            }

            RebuildVisibleNodes();

            rootVM.PropertyChanged += RootVM_PropertyChanged;
            SaveFolder = BuildDefaultSaveFolder(rootVM.SaveAsName);
        }

        #endregion

        #region Построение дерева

        private void BuildChildrenFromRepo(IXComponentRepository repo, AGR_PackNGoNodeVM parentNode)
        {
            List<IXComponent> comps;
            try
            {
                comps = repo.ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Pack'n'Go: не удалось перечислить компоненты сборки.");
                return;
            }

            // Группируем прямых потомков ЭТОГО узла по имени/партномеру компонента (т.е. по
            // фактическому файлу - см. GetOrCreateComponentVM: одному файлу всегда соответствует
            // один и тот же AGR_PackNGoComponentVM). Несколько одинаковых компонентов на одном
            // уровне вложенности (например, 3 одинаковых болта в одной подсборке) схлопываются
            // в одну строку с количеством, вместо N визуально идентичных строк. Один и тот же файл
            // в РАЗНЫХ подсборках при этом остаётся отдельными строками (группировка - только
            // среди непосредственных "братьев", а не по всему дереву), но все они по-прежнему
            // ссылаются на общий ComponentVM, поэтому правки синхронизируются между ними.
            var order = new List<string>();
            var groups = new Dictionary<string, (AGR_PackNGoComponentVM componentVM, IXComponent representative, int quantity)>();

            foreach (var xComp in comps)
            {
                try
                {
                    // Фильтруем подавленные / исключённые из спецификации / envelope-компоненты -
                    // аналогично AGR_XComponentsRepoExtension.AGR_BaseComponents(onlyActive: true).
                    var state = xComp.State;
                    if (state.HasFlag(ComponentState_e.Suppressed)
                        || state.HasFlag(ComponentState_e.SuppressedIdMismatch)
                        || state.HasFlag(ComponentState_e.ExcludedFromBom)
                        || state.HasFlag(ComponentState_e.Envelope))
                    {
                        continue;
                    }

                    IAGR_BaseComponent baseComp;
                    try
                    {
                        baseComp = xComp.AGR_BaseComponent();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"Pack'n'Go: не удалось получить компонент {xComp.Name}, пропускаем.");
                        continue;
                    }

                    if (baseComp == null) continue;

                    var componentVM = GetOrCreateComponentVM(baseComp);

                    // Ключ группировки: партномер, если он есть, иначе - имя файла. Оба значения
                    // берутся из общего ComponentVM, так что фактически группировка идёт по
                    // "одному и тому же файлу", что и требуется (имя и партномер - это как раз
                    // его атрибуты).
                    var groupKey = !string.IsNullOrWhiteSpace(componentVM.PartNumber)
                        ? "PN:" + componentVM.PartNumber
                        : "NAME:" + componentVM.Name;

                    if (groups.TryGetValue(groupKey, out var existing))
                    {
                        groups[groupKey] = (existing.componentVM, existing.representative, existing.quantity + 1);
                    }
                    else
                    {
                        groups[groupKey] = (componentVM, xComp, 1);
                        order.Add(groupKey);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"Pack'n'Go: ошибка обработки компонента {xComp?.Name}, пропускаем.");
                }
            }

            foreach (var groupKey in order)
            {
                var group = groups[groupKey];
                var componentVM = group.componentVM;

                var childNode = new AGR_PackNGoNodeVM(componentVM, parentNode, parentNode.Level + 1, group.quantity);
                childNode.ExpandedChanged += Node_ExpandedChanged;
                parentNode.Children.Add(childNode);

                RegisterParentRelation(parent: parentNode.ComponentVM, child: componentVM);

                // Спускаемся глубже только если это сборка. Используем "представителя" группы -
                // структура вложенности у всех одинаковых экземпляров одного и того же файла
                // идентична (это ведь один и тот же документ), так что достаточно одного.
                if (componentVM.ComponentType == AGR_ComponentType_e.Assembly)
                {
                    IXComponentRepository children = null;
                    try
                    {
                        children = group.representative.Children;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, $"Pack'n'Go: у компонента {componentVM.Name} нет доступной структуры детей.");
                    }

                    if (children != null)
                    {
                        BuildChildrenFromRepo(children, childNode);
                    }
                }
            }
        }

        /// <summary>Возвращает уже существующий ComponentVM для этого файла (если он уже встречался
        /// где-то в дереве), либо создаёт новый - ровно один экземпляр на уникальный файл.</summary>
        private AGR_PackNGoComponentVM GetOrCreateComponentVM(IAGR_BaseComponent baseComponent)
        {
            var path = baseComponent.SwDocument?.Path;
            if (string.IsNullOrEmpty(path))
            {
                path = (baseComponent as IAGR_HasFile)?.CurrentModelFilePath;
            }
            if (string.IsNullOrEmpty(path))
            {
                // Крайний случай - без пути. Используем имя, чтобы хотя бы не падать
                // (дедупликация в этом случае не гарантирована).
                path = baseComponent.Name ?? Guid.NewGuid().ToString();
            }

            if (_componentVMsByPath.TryGetValue(path, out var existing))
            {
                return existing;
            }

            var vm = new AGR_PackNGoComponentVM(baseComponent);
            vm.PartNumberCleared += ComponentVM_PartNumberCleared;
            _componentVMsByPath[path] = vm;
            AllComponents.Add(vm);
            return vm;
        }

        private void RegisterParentRelation(AGR_PackNGoComponentVM parent, AGR_PackNGoComponentVM child)
        {
            if (ReferenceEquals(parent, child)) return;

            if (!_parentsOf.TryGetValue(child, out var set))
            {
                set = new HashSet<AGR_PackNGoComponentVM>();
                _parentsOf[child] = set;
            }
            set.Add(parent);
        }

        #endregion

        #region Каскадное распространение "новизны" вверх по дереву

        /// <summary>
        /// Когда у компонента сбрасывается партномер (пользователь переименовал деталь либо явно
        /// выбрал "Новый" в контекстном меню), все сборки, в которые этот компонент прямо входит,
        /// тоже должны получить новый партномер - ведь их состав фактически изменился. Каждая такая
        /// сборка сама вызовет ClearPartNumber(), которая (если значение реально изменилось) снова
        /// поднимет PartNumberCleared - и распространение продолжится вверх по дереву само собой,
        /// пока не дойдёт до корневой сборки. Явной рекурсии тут не нужно.
        /// </summary>
        private void ComponentVM_PartNumberCleared(object sender, EventArgs e)
        {
            if (sender is not AGR_PackNGoComponentVM child) return;
            if (!_parentsOf.TryGetValue(child, out var parents)) return;

            foreach (var parent in parents)
            {
                parent.ClearPartNumber();
            }
        }

        #endregion

        #region Видимые строки (сворачивание/разворачивание сборок)

        private void Node_ExpandedChanged(object sender, EventArgs e) => RebuildVisibleNodes();

        private void RebuildVisibleNodes()
        {
            VisibleNodes.Clear();
            AddVisible(RootNode);

            void AddVisible(AGR_PackNGoNodeVM node)
            {
                VisibleNodes.Add(node);
                if (node.IsExpanded)
                {
                    foreach (var child in node.Children)
                    {
                        AddVisible(child);
                    }
                }
            }
        }

        #endregion

        #region SaveFolder ("Сохранить куда")

        private string _saveFolder = string.Empty;
        public string SaveFolder
        {
            get => _saveFolder;
            set => Set(ref _saveFolder, value);
        }

        private void RootVM_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Пока пользователь не выбрал папку вручную, держим значение по умолчанию
            // синхронизированным с именем "Сохранить как" главной сборки.
            if (e.PropertyName == nameof(AGR_PackNGoComponentVM.SaveAsName) && !_saveFolderManuallyEdited)
            {
                SaveFolder = BuildDefaultSaveFolder(RootNode.ComponentVM.SaveAsName);
            }
        }

        private static string BuildDefaultSaveFolder(string rootSaveAsName)
        {
            var baseFolder = AGR_Options.LocalWorkFolder ?? string.Empty;
            return Path.Combine(baseFolder, rootSaveAsName ?? string.Empty);
        }

        #region BrowseFolderCommand

        private ICommand _browseFolderCommand;
        public ICommand BrowseFolderCommand => _browseFolderCommand ??= new RelayCommand(_ => OnBrowseFolder());

        private void OnBrowseFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Выберите папку для сохранения",
                SelectedPath = Directory.Exists(SaveFolder) ? SaveFolder : (AGR_Options.LocalWorkFolder ?? string.Empty)
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _saveFolderManuallyEdited = true;
                SaveFolder = dialog.SelectedPath;
            }
        }

        #endregion

        #endregion

        #region FindReplaceCommand

        private ICommand _findReplaceCommand;
        public ICommand FindReplaceCommand => _findReplaceCommand ??= new RelayCommand(OnFindReplace);

        private void OnFindReplace(object parameter)
        {
            var frVm = new AGR_PackNGoFindReplaceVM();
            var frWindow = new AGR_PackNGoFindReplaceWindow
            {
                DataContext = frVm,
                Owner = parameter as Window,
                Topmost = true
            };

            var result = frWindow.ShowDialog();
            if (result == true && !string.IsNullOrEmpty(frVm.FindWhat))
            {
                ApplyFindReplace(frVm.FindWhat, frVm.ReplaceWith ?? string.Empty);
            }
        }

        /// <summary>Применяет замену части строки в поле "Сохранить как" ко всем уникальным
        /// компонентам, чьё текущее имя содержит искомую подстроку (п. "Найти/Заменить" ТЗ).</summary>
        private void ApplyFindReplace(string findWhat, string replaceWith)
        {
            foreach (var comp in AllComponents.ToList())
            {
                if (comp.SaveAsName?.IndexOf(findWhat, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Через публичный сеттер - он сам сбросит PartNumber и запустит каскад вверх,
                    // как и при обычном редактировании ячейки пользователем.
                    comp.SaveAsName = ReplaceIgnoreCase(comp.SaveAsName, findWhat, replaceWith);
                }
            }
        }

        private static string ReplaceIgnoreCase(string source, string find, string replace)
        {
            if (string.IsNullOrEmpty(find)) return source;

            var sb = new System.Text.StringBuilder();
            int pos = 0;
            int idx;
            while ((idx = source.IndexOf(find, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                sb.Append(source, pos, idx - pos);
                sb.Append(replace);
                pos = idx + find.Length;
            }
            sb.Append(source, pos, source.Length - pos);
            return sb.ToString();
        }

        #endregion

        #region SaveCommand / CancelCommand

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            private set => Set(ref _isSaving, value);
        }

        private ICommand _saveCommand;
        public ICommand SaveCommand => _saveCommand ??= new RelayCommand(async p => await OnSaveAsync(p), _ => !IsSaving);

        private async Task OnSaveAsync(object parameter)
        {
            if (IsSaving) return;

            if (string.IsNullOrWhiteSpace(SaveFolder))
            {
                MessageBox.Show("Укажите папку для сохранения.", "Pack'n'Go", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var checkedComponents = AllComponents.Where(c => c.IsChecked).ToList();
            if (checkedComponents.Count == 0)
            {
                MessageBox.Show("Не выбрано ни одного компонента для копирования.", "Pack'n'Go", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSaving = true;
            IsLoading = true;
            try
            {
                Directory.CreateDirectory(SaveFolder);

                // 1. Копируем файлы через нативный ISldWorks.CopyDocument - как и в
                // ComponentVersionService.CopyFilesToStorageAsync. В отличие от штатного
                // SW Pack and Go, здесь мы полностью сами задаём целевые пути (без
                // приведения имён к верхнему регистру и без схемы префикс/суффикс).
                // Закрытие всех документов без сохранения выполняется внутри метода,
                // непосредственно перед копированием - точно так же, как в CopyFilesToStorageAsync.
                LoadingStatus = "Копирование файлов...";
                var copyOk = ExecuteCopyDocument(checkedComponents);
                if (!copyOk)
                {
                    MessageBox.Show(
                        "Не удалось скопировать файлы. Подробности см. в логе.",
                        "Pack'n'Go", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }



                //Проверяем на readonly файлы, если есть - снимаем чтение
                LoadingStatus = "Проверка файлов 'только для чтения'...";
                CheckReadOnlyFiles();


                // 2. Партномера записываем не в исходник, а в СКОПИРОВАННЫЕ файлы: открываем
                // скопированную сборку, обходим её компоненты и для каждого, у которого значение
                // отличается от того, что нужно по диалогу, проставляем свойство PartNumber и
                // сохраняем документ. Для компонентов, помеченных как "новые" (переименованные
                // либо явно очищенные через ПКМ), значение просто сбрасывается в пустое - реальный
                // новый партномер и запись Component в БД создаются позже, штатной командой
                // "Присвоить новый партномер" (SetNewPartnumberCommand), когда пользователь
                // окончательно определится с составом сборки. Здесь этим специально не занимаемся,
                // чтобы не занимать номера впустую под детали, которые могут ещё поменяться.
                LoadingStatus = "Простановка партномеров в скопированных файлах...";
                ApplyPartNumbersToCopy(checkedComponents);

                DialogResult = true;
                (parameter as Window)?.Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выполнении Pack'n'Go.");
                MessageBox.Show($"Ошибка: {ex.Message}", "Pack'n'Go", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
                IsLoading = false;
            }
        }

        /// <summary>
        /// Копирует файлы через нативный ISldWorks.CopyDocument - тот же метод, который
        /// используется в ComponentVersionService.CopyFilesToStorageAsync/CopyFilesToProdAsync.
        /// В отличие от штатного SolidWorks Pack and Go (IPackAndGo), CopyDocument принимает
        /// явные пары "исходный путь -> целевой путь" для каждого файла и ничего не приводит
        /// к верхнему регистру и не навязывает единую схему переименования (префикс/суффикс) -
        /// именно поэтому он и подходит здесь: каждый компонент переименовывается ровно так,
        /// как указано в "Сохранить как".
        /// </summary>
        private bool ExecuteCopyDocument(List<AGR_PackNGoComponentVM> checkedComponents)
        {
            try
            {
                // 1. Формируем параллельные массивы "исходный путь -> целевой путь" - модель
                // каждого отмеченного галочкой компонента, плюс его чертёж (.slddrw), если есть.
                var sourceList = new List<string>();
                var targetList = new List<string>();

                foreach (var comp in checkedComponents)
                {
                    if (string.IsNullOrEmpty(comp.FilePath)) continue;
                    if (comp.FilePath.Contains("Local\\Temp")) continue;

                    var ext = Path.GetExtension(comp.FilePath);
                    sourceList.Add(comp.FilePath);
                    targetList.Add(Path.Combine(SaveFolder, comp.SaveAsName + ext));

                    if (comp.HasDrawing)
                    {
                        var drawSource = Path.ChangeExtension(comp.FilePath, ".slddrw");
                        var drawExt = Path.GetExtension(drawSource);
                        sourceList.Add(drawSource);
                        targetList.Add(Path.Combine(SaveFolder, comp.SaveAsName + drawExt));
                    }
                }

                if (sourceList.Count == 0)
                {
                    _logger?.LogWarning("Pack'n'Go: не найдено файлов для копирования.");
                    return false;
                }

                var sourceArray = sourceList.ToArray();
                var targetArray = targetList.ToArray();

                // 2. Корневой файл (сама сборка/деталь) передаётся в CopyDocument отдельными
                // параметрами (в дополнение к тому, что он же присутствует и в массивах).
                var rootComp = RootNode.ComponentVM;
                var rootIndex = Array.IndexOf(sourceArray, rootComp.FilePath);
                if (rootIndex < 0)
                {
                    _logger?.LogError("Pack'n'Go: корневой компонент не отмечен галочкой или недоступен - копирование невозможно.");
                    return false;
                }

                var sourceFile = sourceArray[rootIndex];
                var targetFile = targetArray[rootIndex];

                // 3. Закрываем все документы без сохранения перед копированием - как и в
                // CopyFilesToStorageAsync (swApp.Sw.CloseAllDocuments(true), true - подавляет
                // запросы на сохранение). Партномера уже записаны в БД на предыдущем шаге,
                // сами файлы на диске мы не трогаем.
                _logger?.LogDebug("Pack'n'Go: закрытие всех документов перед копированием.");
                _swApp.Sw.CloseAllDocuments(true);

                var errorsRaw = _swApp.Sw.CopyDocument(
                    sourceFile,
                    targetFile,
                    sourceArray,
                    targetArray,
                    (int)swMoveCopyOptions_e.swMoveCopyOptionsOverwriteExistingDocs
                );

                if (errorsRaw != 0)
                {
                    _logger?.LogError($"Pack'n'Go: ошибки SolidWorks при копировании через CopyDocument: {errorsRaw}");
                    return false;
                }

                _logger?.LogInformation($"Pack'n'Go: файлы успешно скопированы в {SaveFolder}.");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при копировании файлов через CopyDocument.");
                return false;
            }
        }

        /// <summary>
        /// Проставляет партномера в уже СКОПИРОВАННЫЕ файлы (а не в исходники): открывает
        /// скопированную корневую сборку/деталь, обходит её компоненты и для каждого, у которого
        /// значение в копии отличается от того, что решено в диалоге (новое сгенерированное или
        /// явно очищенное), записывает свойство PartNumber и сохраняет документ. По завершении
        /// закрывает все документы, открытые для этой операции (без повторного сохранения -
        /// нужное уже сохранено явно).
        /// </summary>
        private void ApplyPartNumbersToCopy(List<AGR_PackNGoComponentVM> checkedComponents)
        {
            try
            {
                // Ключ - имя файла без расширения ("Сохранить как"), под которым компонент
                // сейчас лежит в копии; значение - соответствующий ComponentVM из диалога.
                var lookup = checkedComponents
                    .Where(c => !string.IsNullOrEmpty(c.SaveAsName))
                    .GroupBy(c => c.SaveAsName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var rootComp = RootNode.ComponentVM;
                var rootExt = Path.GetExtension(rootComp.FilePath);
                var rootCopyPath = Path.Combine(SaveFolder, rootComp.SaveAsName + rootExt);

                if (!File.Exists(rootCopyPath))
                {
                    _logger?.LogError($"Pack'n'Go: копия корневого файла не найдена: {rootCopyPath}");
                    return;
                }

                var viewModelCache = AGR_ServiceContainer.GetService<IAGR_ViewModelCacheService>();
                var componentFactory = AGR_ServiceContainer.GetService<IAGR_ComponentViewModelFactory>();
                if (viewModelCache == null || componentFactory == null)
                {
                    _logger?.LogError("Pack'n'Go: не удалось получить сервисы для простановки партномеров в копии.");
                    return;
                }

                // path -> нативный документ, в который реально что-то записали и который нужно сохранить.
                var docsToSave = new Dictionary<string, ModelDoc2>(StringComparer.OrdinalIgnoreCase);

                var rootDoc = _swApp.Documents.Open(rootCopyPath, DocumentState_e.Silent) as ISwDocument3D;
                if (rootDoc == null)
                {
                    _logger?.LogError($"Pack'n'Go: не удалось открыть копию {rootCopyPath}.");
                    return;
                }

                var rootCopyComponent = viewModelCache.GetOrCreate(rootDoc, d => componentFactory.CreateComponent(d));
                if (TryApplyPartNumber(rootCopyComponent, rootComp.SaveAsName, lookup))
                {
                    AddDocToSave(docsToSave, rootDoc);
                }

                if (rootDoc is ISwAssembly rootAsmDoc)
                {
                    ApplyToChildrenRecursive(rootAsmDoc.Configurations.Active.Components, lookup, docsToSave);
                }

                // Сохраняем все документы, в которые реально что-то записали.
                foreach (var model in docsToSave.Values)
                {
                    try
                    {
                        model.ForceRebuild3(false);
                        int errors = 0, warnings = 0;
                        model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
                        if (errors != 0)
                        {
                            _logger?.LogWarning($"Pack'n'Go: при сохранении {model.GetPathName()} код ошибки {errors}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"Pack'n'Go: не удалось сохранить {model.GetPathName()}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при простановке партномеров в скопированных файлах.");
            }
            finally
            {
                // Закрываем всё, что открывали для этой операции. Нужное уже сохранено явно выше,
                // поэтому подавление диалога сохранения (true) здесь безопасно.
                //_swApp.Sw.CloseAllDocuments(true);
            }
        }

        private void ApplyToChildrenRecursive(IXComponentRepository repo,
            Dictionary<string, AGR_PackNGoComponentVM> lookup,
            Dictionary<string, ModelDoc2> docsToSave)
        {
            List<IXComponent> comps;
            try
            {
                comps = repo.ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Pack'n'Go: не удалось перечислить компоненты копии сборки.");
                return;
            }

            foreach (var xComp in comps)
            {
                try
                {
                    var baseComp = xComp.AGR_BaseComponent();
                    if (baseComp == null) continue;

                    var filePath = (baseComp as IAGR_HasFile)?.CurrentModelFilePath
                                   ?? baseComp.SwDocument?.Path;
                    var fileNameNoExt = string.IsNullOrEmpty(filePath) ? null : Path.GetFileNameWithoutExtension(filePath);

                    if (fileNameNoExt != null
                        && TryApplyPartNumber(baseComp, fileNameNoExt, lookup)
                        && baseComp.SwDocument is ISwDocument3D swDoc3D)
                    {
                        AddDocToSave(docsToSave, swDoc3D);
                    }

                    if (baseComp.ComponentType == AGR_ComponentType_e.Assembly)
                    {
                        IXComponentRepository children = null;
                        try
                        {
                            children = xComp.Children;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug(ex, $"Pack'n'Go: у компонента {xComp.Name} в копии нет доступной структуры детей.");
                        }

                        if (children != null)
                        {
                            ApplyToChildrenRecursive(children, lookup, docsToSave);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"Pack'n'Go: ошибка простановки партномера для {xComp?.Name} в копии.");
                }
            }
        }

        /// <summary>Если для этого файла (по имени без расширения) в диалоге есть решение
        /// (SaveAsName -> ComponentVM) и записанное сейчас в копии значение отличается от того,
        /// что нужно - записывает правильное значение в копию (через тот же IAGR_BaseComponent.PartNumber,
        /// т.е. TryWriteProperty, но уже на объекте, представляющем СКОПИРОВАННЫЙ документ).
        /// Возвращает true, если запись действительно произошла (документ нужно будет сохранить).</summary>
        private bool TryApplyPartNumber(IAGR_BaseComponent copyComponent, string fileNameNoExt,
            Dictionary<string, AGR_PackNGoComponentVM> lookup)
        {
            if (string.IsNullOrEmpty(fileNameNoExt)) return false;
            if (!lookup.TryGetValue(fileNameNoExt, out var dialogComponentVM)) return false;

            var targetPn = dialogComponentVM.PartNumber ?? string.Empty;
            var currentPn = copyComponent.PartNumber ?? string.Empty;

            if (string.Equals(currentPn, targetPn, StringComparison.Ordinal))
            {
                return false; // уже совпадает - в копии и так правильное значение (пришло из исходника)
            }

            copyComponent.PartNumber = targetPn;
            copyComponent.Article = null;
            return true;
        }

        private void CheckReadOnlyFiles()
        {
            var files = Directory.GetFiles(SaveFolder);
            foreach (var file in files)
            {
                if (File.GetAttributes(file)
                    .HasFlag(FileAttributes.ReadOnly))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
            }
        }

        private static void AddDocToSave(Dictionary<string, ModelDoc2> docsToSave, ISwDocument3D swDoc)
        {
            var model = (swDoc as ISwDocument)?.Model as ModelDoc2;
            if (model == null) return;

            var path = swDoc.Path;
            if (string.IsNullOrEmpty(path)) return;

            docsToSave[path] = model;
        }

        private ICommand _cancelCommand;
        public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(p =>
        {
            DialogResult = false;
            (p as Window)?.Close();
        });

        #endregion
    }
}