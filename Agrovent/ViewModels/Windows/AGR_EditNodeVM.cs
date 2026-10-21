namespace Agrovent.ViewModels.Windows
{
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Windows;
    using System.Windows.Input;
    using Agrovent.DAL.Services;
    using Agrovent.Infrastructure.Commands;
    using Agrovent.Infrastructure.Extensions;
    using Agrovent.Services;
    using Agrovent.ViewModels.Base;
    using AgroventInfrastructure;
    using AgroventInfrastructure.Enums;
    using AgroventInfrastructure.Interfaces.Components;
    using AgroventInfrastructure.Interfaces.Components.Base;
    using Microsoft.Extensions.Logging;
    using Xarial.XCad.Documents;
    using Xarial.XCad.Documents.Enums;
    using Xarial.XCad.SolidWorks.Documents;

    /// <summary>Режим окна выбора: взять на редактирование или снять свои замки.</summary>
    public enum AGR_EditSelectionMode_e
    {
        Edit,
        Cancel
    }

    public class AGR_EditNodeVM : System.ComponentModel.INotifyPropertyChanged
    {
        public int ComponentId { get; set; }                         // 0 = в БД нет
        public string PartNumber { get; init; } = "";
        public string Name { get; init; } = "";
        public string FilePath { get; init; } = "";
        /// <summary>Версия файла: из маркера, если он есть, иначе последняя в БД. Становится BaseVersion замка.</summary>
        public int Version { get; set; }
        /// <summary>Последняя версия в БД (для показа «устаревшей» версии файла).</summary>
        public int LatestVersion { get; set; }
        /// <summary>Маркер, прочитанный при построении дерева (null = нет или не прочитан).</summary>
        internal AGR_Marker? Marker { get; init; }
        /// <summary>Маркер есть, но повреждён: брать на редактирование нельзя.</summary>
        public bool IsMarkerInvalid { get; init; }
        public int Quantity { get; set; } = 1;
        public bool IsPurchased { get; init; }                       // заблокирован
        public AGR_EditState_e State { get; set; }
        public string? LockedByUser { get; set; }
        public DateTime? LockedAt { get; set; }

        /// <summary>Родительская подсборка занята другим: узел недоступен.</summary>
        public bool IsBlockedByParent { get; set; }

        public AGR_EditNodeVM? Parent { get; set; }
        public ObservableCollection<AGR_EditNodeVM> Children { get; } = new();

        /// <summary>Глубина в дереве (корень = 0).</summary>
        public int Level => Parent == null ? 0 : Parent.Level + 1;

        // TreeView сдвигает каждый уровень вправо на ширину колонки expander'а (19 px в стандартном
        // шаблоне TreeViewItem). Колонку «Обозначение» сужаем на этот сдвиг, чтобы остальные колонки
        // стояли ровно под заголовками на любом уровне.
        private const double TreeIndent = 19;
        private const double PartNumberColumnWidth = 220;
        public GridLength PartNumberWidth => new(Math.Max(40, PartNumberColumnWidth - Level * TreeIndent));

        internal AGR_EditSelectionViewModel? Owner { get; set; }

        public bool IsInDatabase => ComponentId != 0;

        // Можно ли менять отметку. В режиме Edit свой замок отмечен и не снимается здесь,
        // в режиме Cancel отмечать можно только свои замки.
        public bool CanToggle => Owner?.Mode == AGR_EditSelectionMode_e.Cancel
            ? State == AGR_EditState_e.EditingByMe
            : IsInDatabase
                && !IsPurchased
                && !IsMarkerInvalid
                && !IsBlockedByParent
                && State != AGR_EditState_e.EditingByOther
                && State != AGR_EditState_e.EditingByMe;

        public string StateText => State switch
        {
            AGR_EditState_e.EditingByMe => "На редактировании у меня",
            AGR_EditState_e.EditingByOther => $"Занят: {LockedByUser} с {LockedAt?.ToLocalTime():dd.MM.yyyy HH:mm}",
            _ when IsMarkerInvalid => "Маркер файла повреждён",
            _ when IsPurchased => "Покупной",
            _ when !IsInDatabase => "Нет в БД",
            _ when IsBlockedByParent => "Подсборка занята",
            _ when LatestVersion > Version => $"Версия файла не последняя (в БД v{LatestVersion})",
            _ => ""
        };

        public string VersionText => !IsInDatabase ? ""
            : LatestVersion > Version ? $"{Version} / {LatestVersion}" // колонка 50 px: «версия файла / последняя»
            : Version.ToString();

        private bool _IsChecked;
        /// <summary>Отметка из UI: каскад по правилам окна (см. AGR_EditSelectionViewModel.SetChecked).</summary>
        public bool IsChecked
        {
            get => _IsChecked;
            set
            {
                if (_IsChecked == value || !CanToggle) return;
                if (Owner != null) Owner.SetChecked(this, value);
                else SetCheckedSilently(value);
            }
        }

        internal void SetCheckedSilently(bool value)
        {
            if (_IsChecked == value) return;
            _IsChecked = value;
            PropertyChanged?.Invoke(this, new(nameof(IsChecked)));
        }

        private bool _IsExpanded = true;
        public bool IsExpanded
        {
            get => _IsExpanded;
            set { _IsExpanded = value; PropertyChanged?.Invoke(this, new(nameof(IsExpanded))); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    public class AGR_EditSelectionViewModel : BaseViewModel
    {
        public AGR_EditSelectionMode_e Mode { get; }
        public ObservableCollection<AGR_EditNodeVM> Roots { get; } = new();

        public string Title => Mode == AGR_EditSelectionMode_e.Edit
            ? "Взять на редактирование"
            : "Отменить редактирование";

        public string ConfirmText => Mode == AGR_EditSelectionMode_e.Edit ? "Редактировать" : "Снять замки";

        /// <summary>false, если активный документ уже лежит в LocalWorkFolder: копирование не нужно.</summary>
        public bool IsCopyRequired { get; init; }

        private string _TargetFolder = "";
        public string TargetFolder                                   // по умолчанию LocalWorkFolder + имя компонента
        {
            get => _TargetFolder;
            set => Set(ref _TargetFolder, value);
        }

        public bool? DialogResult { get; private set; }

        public AGR_EditSelectionViewModel(AGR_EditSelectionMode_e mode)
        {
            Mode = mode;
        }

        /// <summary>
        /// Отметка узла: меняем все его вхождения по ComponentId.
        /// Edit: отметка отмечает всех родителей; снятие отметки родителей не трогает.
        /// Cancel: отметка сборки отмечает всех её потомков со своим замком.
        /// </summary>
        public void SetChecked(AGR_EditNodeVM node, bool value)
        {
            foreach (var n in Flatten().Where(n => n.ComponentId == node.ComponentId && n.CanToggle).ToList())
            {
                n.SetCheckedSilently(value);

                if (Mode == AGR_EditSelectionMode_e.Edit)
                {
                    if (!value) continue;
                    for (var p = n.Parent; p != null; p = p.Parent)
                        if (p.CanToggle) p.SetCheckedSilently(true);
                }
                else if (value)
                {
                    foreach (var d in Descendants(n).Where(d => d.CanToggle))
                        d.SetCheckedSilently(true);
                }
            }
            CommandManager.InvalidateRequerySuggested();
        }

        public IEnumerable<AGR_EditNodeVM> Flatten()
        {
            var stack = new Stack<AGR_EditNodeVM>(Roots);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                yield return n;
                foreach (var c in n.Children) stack.Push(c);
            }
        }

        private static IEnumerable<AGR_EditNodeVM> Descendants(AGR_EditNodeVM node)
        {
            foreach (var c in node.Children)
            {
                yield return c;
                foreach (var d in Descendants(c)) yield return d;
            }
        }

        /// <summary>Отмеченные компоненты (по одному на ComponentId).</summary>
        public List<AGR_EditNodeVM> GetCheckedComponents() => Flatten()
            .Where(n => n.IsChecked && n.IsInDatabase)
            .GroupBy(n => n.ComponentId)
            .Select(g => g.First())
            .ToList();

        #region Построение дерева

        /// <summary>
        /// Строит дерево по xCAD-структуре активного документа (как AGR_PackNGoVM.BuildChildrenFromRepo:
        /// подавленные / ExcludedFromBom / Envelope пропускаются, одинаковые файлы среди братьев
        /// схлопываются в один узел с количеством), затем одним запросом подтягивает из БД Id, версию и замки.
        /// </summary>
        public static async Task<AGR_EditSelectionViewModel> CreateAsync(
            AGR_EditSelectionMode_e mode,
            IAGR_BaseComponent rootComponent,
            IAGR_EditLockService editLocks,
            IAGR_MarkerService markers,
            ILogger? logger)
        {
            var rootPath = GetPath(rootComponent);
            var localFolder = AGR_Options.LocalWorkFolder ?? "";
            var isLocal = IsUnderFolder(rootPath, localFolder);

            var vm = new AGR_EditSelectionViewModel(mode)
            {
                IsCopyRequired = mode == AGR_EditSelectionMode_e.Edit && !isLocal,
                TargetFolder = Path.Combine(localFolder, rootComponent.Name ?? Path.GetFileNameWithoutExtension(rootPath))
            };

            // 1. Структура и маркеры из SolidWorks (синхронно, на потоке SolidWorks)
            vm._markers = markers;
            vm._logger = logger;
            var root = vm.CreateNode(rootComponent, null);
            if (rootComponent.SwDocument is ISwAssembly asm)
                vm.BuildChildren(asm.Configurations.Active.Components, root, logger);
            vm.Roots.Add(root);

            // 2. Состояние из БД одним запросом
            var nodes = vm.Flatten().ToList();
            var info = await editLocks.GetEditInfoAsync(nodes.Select(n => n.PartNumber));
            foreach (var n in nodes)
            {
                if (string.IsNullOrWhiteSpace(n.PartNumber) || !info.TryGetValue(n.PartNumber, out var i))
                {
                    n.State = AGR_EditState_e.Unknown;
                    continue;
                }
                n.ComponentId = i.ComponentId;
                n.LatestVersion = i.Version;
                n.Version = i.Version;

                // Версия файла берётся из маркера: замок получит её как BaseVersion (ветвистая история)
                var m = n.Marker;
                if (m != null)
                {
                    if (m.ComponentId != i.ComponentId)
                        logger?.LogWarning($"Редактирование: маркер {n.Name} указывает на Id {m.ComponentId}, а PartNumber {n.PartNumber} - на Id {i.ComponentId}. Берём последнюю версию.");
                    else if (m.Version > i.Version)
                        logger?.LogWarning($"Редактирование: в маркере {n.Name} v{m.Version}, в БД последняя v{i.Version}. Берём последнюю версию.");
                    else
                        n.Version = m.Version;
                }
                n.State = i.State;
                n.LockedByUser = i.LockedBy;
                n.LockedAt = i.LockedAt;
            }

            // 3. Дети подсборки, занятой другим, недоступны
            foreach (var n in nodes.Where(n => n.State == AGR_EditState_e.EditingByOther))
                foreach (var d in Descendants(n))
                    d.IsBlockedByParent = true;

            // 4. Начальные отметки: свои замки в режиме Edit отмечены (и не снимаются),
            //    корень предотмечен, если его можно взять.
            if (mode == AGR_EditSelectionMode_e.Edit)
            {
                foreach (var n in nodes.Where(n => n.State == AGR_EditState_e.EditingByMe))
                    n.SetCheckedSilently(true);
                if (root.CanToggle) root.SetCheckedSilently(true);
            }

            return vm;
        }

        private IAGR_MarkerService? _markers;
        private ILogger? _logger;

        private AGR_EditNodeVM CreateNode(IAGR_BaseComponent comp, AGR_EditNodeVM? parent)
        {
            AGR_Marker? marker = null;
            var invalid = false;
            var doc = comp.SwDocument;
            if (_markers != null && doc != null && doc.IsAlive)
            {
                var result = _markers.TryRead(doc, out marker, out var error);
                invalid = result == AGR_MarkerReadResult_e.Invalid;
                if (invalid) _logger?.LogWarning($"Редактирование: маркер {comp.Name} повреждён: {error}");
            }
            return CreateNode(comp, parent, marker, invalid);
        }

        private AGR_EditNodeVM CreateNode(IAGR_BaseComponent comp, AGR_EditNodeVM? parent, AGR_Marker? marker, bool markerInvalid) => new AGR_EditNodeVM
        {
            Marker = marker,
            IsMarkerInvalid = markerInvalid,
            PartNumber = comp.PartNumber ?? "",
            Name = comp.Name ?? "",
            FilePath = GetPath(comp),
            IsPurchased = comp.ComponentType == AGR_ComponentType_e.Purchased,
            Parent = parent,
            Owner = this
        };

        private void BuildChildren(IXComponentRepository repo, AGR_EditNodeVM parentNode, ILogger? logger)
        {
            List<IXComponent> comps;
            try
            {
                comps = repo.ToList();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Редактирование: не удалось перечислить компоненты сборки.");
                return;
            }

            // path -> (узел, представитель для обхода детей)
            var groups = new Dictionary<string, (AGR_EditNodeVM Node, IXComponent Representative)>(StringComparer.OrdinalIgnoreCase);

            foreach (var xComp in comps)
            {
                try
                {
                    var state = xComp.State;
                    if (state.HasFlag(ComponentState_e.Suppressed)
                        || state.HasFlag(ComponentState_e.SuppressedIdMismatch)
                        || state.HasFlag(ComponentState_e.ExcludedFromBom)
                        || state.HasFlag(ComponentState_e.Envelope))
                    {
                        continue;
                    }

                    var baseComp = xComp.AGR_BaseComponent();
                    if (baseComp == null) continue;

                    var path = GetPath(baseComp);
                    if (string.IsNullOrEmpty(path)) continue; // Embedded / виртуальные без файла

                    if (groups.TryGetValue(path, out var existing))
                    {
                        existing.Node.Quantity++;
                        continue;
                    }

                    var node = CreateNode(baseComp, parentNode);
                    parentNode.Children.Add(node);
                    groups[path] = (node, xComp);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, $"Редактирование: ошибка обработки компонента {xComp?.Name}, пропускаем.");
                }
            }

            foreach (var (node, representative) in groups.Values)
            {
                if (!node.FilePath.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase)) continue;
                if (node.IsPurchased) continue; // внутрь покупных не заходим: они заблокированы целиком

                IXComponentRepository? children = null;
                try { children = representative.Children; }
                catch (Exception ex) { logger?.LogDebug(ex, $"Редактирование: у {node.Name} нет доступной структуры детей."); }

                if (children != null) BuildChildren(children, node, logger);
            }
        }

        private static string GetPath(IAGR_BaseComponent comp)
        {
            var path = comp.SwDocument?.Path;
            if (string.IsNullOrEmpty(path)) path = (comp as IAGR_HasFile)?.CurrentModelFilePath;
            return path ?? "";
        }

        public static bool IsUnderFolder(string path, string folder)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder)) return false;
            try
            {
                var full = Path.GetFullPath(path);
                var dir = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return full.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        #endregion

        #region COMMANDS

        #region ConfirmEditSelectionCommand
        private ICommand _ConfirmEditSelectionCommand;
        public ICommand ConfirmEditSelectionCommand => _ConfirmEditSelectionCommand
            ??= new RelayCommand(OnConfirmEditSelectionCommandExecuted, CanConfirmEditSelectionCommandExecute);
        private bool CanConfirmEditSelectionCommandExecute(object p)
        {
            if (Mode == AGR_EditSelectionMode_e.Cancel)
                return Flatten().Any(n => n.IsChecked);

            return (!IsCopyRequired || !string.IsNullOrWhiteSpace(TargetFolder))
                && Flatten().Any(n => n.IsChecked && n.State != AGR_EditState_e.EditingByMe);
        }
        private void OnConfirmEditSelectionCommandExecuted(object p)
        {
            // Папка существует и не пуста: перезаписать или вернуться к выбору папки
            if (IsCopyRequired
                && Directory.Exists(TargetFolder)
                && Directory.EnumerateFileSystemEntries(TargetFolder).Any())
            {
                var answer = MessageBox.Show(
                    $"Папка уже существует и содержит файлы:\n{TargetFolder}\n\nПерезаписать файлы?\n(«Нет» вернёт к выбору папки)",
                    Title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) return;
            }

            DialogResult = true;
            (p as Window)?.Close();
        }
        #endregion

        #region CancelEditSelectionCommand
        private ICommand _CancelEditSelectionCommand;
        public ICommand CancelEditSelectionCommand => _CancelEditSelectionCommand
            ??= new RelayCommand(OnCancelEditSelectionCommandExecuted, CanCancelEditSelectionCommandExecute);
        private bool CanCancelEditSelectionCommandExecute(object p) => true;
        private void OnCancelEditSelectionCommandExecuted(object p)
        {
            DialogResult = false;
            (p as Window)?.Close();
        }
        #endregion

        #region BrowseTargetFolderCommand
        private ICommand _BrowseTargetFolderCommand;
        public ICommand BrowseTargetFolderCommand => _BrowseTargetFolderCommand
            ??= new RelayCommand(OnBrowseTargetFolderCommandExecuted, CanBrowseTargetFolderCommandExecute);
        private bool CanBrowseTargetFolderCommandExecute(object p) => IsCopyRequired;
        private void OnBrowseTargetFolderCommandExecuted(object p)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Папка для локальной копии",
                SelectedPath = Directory.Exists(TargetFolder) ? TargetFolder : (AGR_Options.LocalWorkFolder ?? string.Empty)
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                TargetFolder = dialog.SelectedPath;
        }
        #endregion

        #endregion
    }
}
