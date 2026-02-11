// File: ViewModels/MainWindowViewModel.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AGR_PropManager.ViewModels.Base; // BaseViewModel
using System.Windows.Input; // ICommand
using AGR_PropManager.Infrastructure.Commands;
using Agrovent.DAL;
using System.Windows.Media.Imaging; // RelayCommand
using Agrovent.Infrastructure.Enums;

namespace AGR_PropManager.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly DataContext _context; // Используем DataContext напрямую для чтения
        private readonly ILogger<MainWindowViewModel> _logger;

        public MainWindowViewModel(DataContext context, ILogger<MainWindowViewModel> logger)
        {
            _context = context;
            _logger = logger;

            // Инициализация CollectionViewSource
            Components_CVS = new CollectionViewSource();
            Components_CVS.Source = Components; // Привязка к ObservableCollection
        }

        #region Commands

        #region LoadStructureCommand
        private ICommand _LoadStructureCommand;
        public ICommand LoadStructureCommand => _LoadStructureCommand
            ??= new RelayCommand<string>(OnLoadStructureCommandExecuted, CanLoadStructureCommandExecute);
        private bool CanLoadStructureCommandExecute(string p) => !string.IsNullOrWhiteSpace(p); // Проверяем, что PartNumber не пуст
        private async void OnLoadStructureCommandExecuted(string partNumber)
        {
            await LoadAssemblyStructureAsync(partNumber);
        }
        #endregion

        #region EditProcessCommand
        private ICommand _EditProcessCommand;
        public ICommand EditProcessCommand => _EditProcessCommand
            ??= new RelayCommand(OnEditProcessCommandExecuted, CanEditProcessCommandExecute);
        private bool CanEditProcessCommandExecute(object p) => HasSelectedComponents; // Проверяем, есть ли выделенные
        private void OnEditProcessCommandExecuted(object p)
        {
            EditProcess();
        }
        #endregion

        #region HandleKeyDownCommand
        private ICommand _HandleKeyDownCommand;
        public ICommand HandleKeyDownCommand => _HandleKeyDownCommand
            ??= new RelayCommand(OnHandleKeyDownCommandExecuted, CanHandleKeyDownCommandExecute);
        private bool CanHandleKeyDownCommandExecute(object p) => true; // Пока всегда разрешено
        private void OnHandleKeyDownCommandExecuted(object p)
        {
            EditProcess();
        }
        #endregion

        #endregion

        #region Properties

        #region SearchPartNumber
        private string _SearchPartNumber = "";
        public string SearchPartNumber
        {
            get => _SearchPartNumber;
            set => Set(ref _SearchPartNumber, value);
        }
        #endregion

        #region SelectedGroupingMode
        private string _SelectedGroupingMode = "По типу"; // Значение по умолчанию
        public string SelectedGroupingMode
        {
            get => _SelectedGroupingMode;
            set
            {
                if (Set(ref _SelectedGroupingMode, value))
                {
                    RefreshGrouping();
                }
            }
        }
        #endregion

        #region HasSelectedComponents
        // Это свойство не сохраняется, но уведомляет об изменении для CanExecute команд
        public bool HasSelectedComponents => Components.Any(c => c.IsSelected);
        private void NotifyHasSelectedComponentsChanged()
        {
            OnPropertyChanged(nameof(HasSelectedComponents));
        }
        #endregion

        #endregion

        #region CollectionViewSource
        private CollectionViewSource Components_CVS = new CollectionViewSource();
        public ICollectionView ComponentsView => Components_CVS?.View;
        #endregion

        // Коллекция для хранения данных
        private ObservableCollection<ComponentItemViewModel> _Components = new();
        public ObservableCollection<ComponentItemViewModel> Components
        {
            get => _Components;
            set => Set(ref _Components, value);
        }

        // Метод загрузки структуры сборки
        private async Task LoadAssemblyStructureAsync2(string partNumber)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
            {
                _logger.LogWarning("PartNumber пуст при попытке загрузки структуры.");
                return;
            }

            try
            {
                _logger.LogInformation($"Загрузка структуры для PartNumber: {partNumber}");

                // Очищаем текущую коллекцию
                Components.Clear();

                // Найдем версию сборки по PartNumber (берем последнюю по версии)
                var assemblyVersion = await _context.ComponentVersions
                    .Include(cv => cv.Component) // Загружаем связанный компонент
                    .Include(cv => cv.Properties) // Загружаем свойства
                    .Where(cv => cv.Component.PartNumber == partNumber && cv.ComponentType == (int)Agrovent.Infrastructure.Enums.AGR_ComponentType_e.Assembly)
                    .OrderByDescending(cv => cv.Version) // Берем последнюю версию
                    .FirstOrDefaultAsync();

                if (assemblyVersion == null)
                {
                    _logger.LogWarning($"Сборка с PartNumber {partNumber} не найдена.");
                    return;
                }

                // Найдем все элементы структуры для этой версии сборки
                var assemblyStructureEntries = await _context.AssemblyStructures
                    .Include(s => s.ComponentVersion) // Загружаем версию компонента из структуры
                        .ThenInclude(cv => cv.Component) // Загружаем сам компонент
                    .Include(s => s.ComponentVersion) // Загружаем версию компонента из структуры
                        .ThenInclude(cv => cv.Properties) // Загружаем свойства
                    .Where(s => s.AssemblyVersionId == assemblyVersion.Id) // Фильтруем по AssemblyVersionId
                    .ToListAsync();

                // Загрузим все связанные TechnologicalProcesses за один запрос
                var partNumbersInStructure = assemblyStructureEntries.Select(entry => entry.ComponentVersion.Component.PartNumber).Distinct().ToList();
                var techProcesses = await _context.TechProcesses
                    .Include(tp => tp.Operations)
                        .ThenInclude(op => op.Workstation)
                    .Where(tp => partNumbersInStructure.Contains(tp.PartNumber))
                    .ToListAsync();

                foreach (var entry in assemblyStructureEntries)
                {
                    var compVer = entry.ComponentVersion;
                    var comp = compVer.Component;
                    var partNum = comp.PartNumber;

                    // Загрузим нужные свойства из ComponentProperties
                    var materialProp = compVer.Properties.FirstOrDefault(p => p.Name == "Material");
                    var paintProp = compVer.Properties.FirstOrDefault(p => p.Name == "Paint");
                    var bendCountProp = compVer.Properties.FirstOrDefault(p => p.Name == "BendCount");
                    var contourLengthProp = compVer.Properties.FirstOrDefault(p => p.Name == "ContourLength");
                    var previewImage = compVer.PreviewImage != null ? LoadImageFromBytes(compVer.PreviewImage) : null;

                    // Найдем соответствующий техпроцесс по PartNumber
                    var techProcess = techProcesses.FirstOrDefault(tp => tp.PartNumber == partNum);

                    var vm = new ComponentItemViewModel
                    {
                        PartNumber = partNum,
                        Name = compVer.Name,
                        Quantity = entry.Quantity,
                        Material = materialProp?.Value ?? "",
                        Paint = paintProp?.Value ?? "",
                        BendCount = int.TryParse(bendCountProp?.Value, out int bc) ? bc : 0,
                        ContourLength = decimal.TryParse(contourLengthProp?.Value, out decimal cl) ? cl : 0,
                        ComponentTypeDisplay = compVer.ComponentType.ToString(),
                        PreviewImage = previewImage,
                        // Заполняем Operations и TechProcessSummary из найденного TechnologicalProcess
                        Operations = techProcess?.Operations.Select(op => new ComponentTechProcessOperationViewModel
                        {
                            OperationName = op.Name, // Из новой сущности Operation
                            TotalTime = op.LaborIntensityMinutes, // Из новой сущности Operation
                            SequenceNumber = op.SequenceNumber // Из новой сущности Operation
                        }).ToList() ?? new List<ComponentTechProcessOperationViewModel>()
                    };

                    vm.TechProcessSummary = string.Join(", ", vm.Operations.OrderBy(o => o.SequenceNumber).Select(o => $"{o.OperationName}({o.TotalTime})"));
                    vm.HasZeroTimeOperations = vm.Operations.Any(o => o.IsZeroTime);

                    Components.Add(vm);
                }

                // Обновляем CollectionViewSource Source, чтобы он отслеживал изменения в коллекции
                Components_CVS.Source = Components;

                _logger.LogInformation($"Загружено {Components.Count} компонентов для сборки {partNumber}.");

                // Обновляем группировку
                RefreshGrouping();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрузке структуры сборки {partNumber}");
            }
        }

        private async Task LoadAssemblyStructureAsync(string partNumber)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
            {
                _logger.LogWarning("PartNumber пуст при попытке загрузки структуры.");
                return;
            }

            try
            {
                _logger.LogInformation($"Загрузка структуры для PartNumber: {partNumber}");

                // Очищаем текущую коллекцию
                Components.Clear();

                // Найдем версию сборки по PartNumber (берем последнюю по версии)
                var assemblyVersion = await _context.ComponentVersions
                    .Include(cv => cv.Component) // Загружаем связанный компонент
                    .Include(cv => cv.Properties) // Загружаем свойства
                    .Where(cv => cv.Component.PartNumber == partNumber && cv.ComponentType == (int)Agrovent.Infrastructure.Enums.AGR_ComponentType_e.Assembly)
                    .OrderByDescending(cv => cv.Version) // Берем последнюю версию
                    .FirstOrDefaultAsync();

                if (assemblyVersion == null)
                {
                    _logger.LogWarning($"Сборка с PartNumber {partNumber} не найдена.");
                    return;
                }

                // Найдем все элементы структуры для этой версии сборки
                var assemblyStructureEntries = await _context.AssemblyStructures
                    .Include(s => s.ComponentVersion) // Загружаем версию компонента из структуры
                        .ThenInclude(cv => cv.Component) // Загружаем сам компонент
                    .Include(s => s.ComponentVersion) // Загружаем версию компонента из структуры
                        .ThenInclude(cv => cv.Properties) // Загружаем свойства
                    .Include(s => s.ComponentVersion) // Загружаем версию компонента из структуры
                        .ThenInclude(cv => cv.Material) // Загружаем данные материалов
                    .Where(s => s.AssemblyVersionId == assemblyVersion.Id) // Фильтруем по AssemblyVersionId
                    .ToListAsync();

                // --- НОВАЯ ЛОГИКА: Группировка и суммирование ---
                var groupedEntries = assemblyStructureEntries
                    .GroupBy(s => s.ComponentVersion.Component.PartNumber) // Группируем по PartNumber
                    .Select(g => new
                    {
                        PartNumber = g.Key,
                        // Берем данные из *одной* версии компонента в группе (предполагаем, что они одинаковые для одного PartNumber в структуре)
                        FirstComponentVersion = g.First().ComponentVersion,
                        TotalQuantity = g.Sum(s => s.Quantity) // Суммируем Quantity
                    })
                    .ToList();

                // Загрузим все связанные TechnologicalProcesses за один запрос
                var partNumbersInStructure = groupedEntries.Select(entry => entry.PartNumber).Distinct().ToList();
                var techProcesses = await _context.TechProcesses
                    .Include(tp => tp.Operations)
                        .ThenInclude(op => op.Workstation)
                    .Where(tp => partNumbersInStructure.Contains(tp.PartNumber))
                    .ToListAsync();

                foreach (var groupedEntry in groupedEntries)
                {
                    var compVer = groupedEntry.FirstComponentVersion;
                    var comp = compVer.Component;
                    var partNum = comp.PartNumber;
                    var totalQty = groupedEntry.TotalQuantity; // Используем суммарное количество
                    var props = compVer.Properties;
                    var materials = compVer.Material;



                    // Загрузим нужные свойства из ComponentProperties (берем из первой версии в группе)
                    var materialProp = materials?.BaseMaterial;
                    var paintProp = materials?.Paint;
                    var bendCountProp = compVer.Properties.FirstOrDefault(p => p.Name == AGR_PropertyNames.BlankBends);
                    var blankOuterContour = compVer.Properties.FirstOrDefault(p => p.Name == AGR_PropertyNames.BlankOuterContour);
                    var blankInnerContour = compVer.Properties.FirstOrDefault(p => p.Name == AGR_PropertyNames.BlankInnerContour);

                    var contourSum = (decimal.TryParse(blankInnerContour?.Value, out decimal c1) ? c1 : 0) +
                        (decimal.TryParse(blankOuterContour?.Value, out decimal c2) ? c2 : 0);


                    var previewImage = compVer.PreviewImage != null ? LoadImageFromBytes(compVer.PreviewImage) : null;

                    // Найдем соответствующий техпроцесс по PartNumber
                    var techProcess = techProcesses.FirstOrDefault(tp => tp.PartNumber == partNum);


                    var vm = new ComponentItemViewModel
                    {
                        PartNumber = partNum,
                        Name = compVer.Name,
                        Quantity = totalQty, // Устанавливаем суммарное количество
                        Material = materialProp ?? "",
                        Paint = paintProp ?? "",
                        BendCount = int.TryParse(bendCountProp?.Value, out int bc) ? bc : 0,
                        ContourLength = contourSum,
                        ComponentTypeDisplay = compVer.ComponentType.ToString(),
                        PreviewImage = previewImage,
                        // Заполняем Operations и TechProcessSummary из найденного TechnologicalProcess
                        Operations = techProcess?.Operations.Select(op => new ComponentTechProcessOperationViewModel
                        {
                            OperationName = op.Name, // Из новой сущности Operation
                            TotalTime = op.LaborIntensityMinutes, // Из новой сущности Operation
                            SequenceNumber = op.SequenceNumber // Из новой сущности Operation
                        }).ToList() ?? new List<ComponentTechProcessOperationViewModel>()
                    };

                    vm.TechProcessSummary = string.Join(", ", vm.Operations.OrderBy(o => o.SequenceNumber).Select(o => $"{o.OperationName}({o.TotalTime})"));
                    vm.HasZeroTimeOperations = vm.Operations.Any(o => o.IsZeroTime);
                    

                    Components.Add(vm);

                }
                // --- КОНЕЦ НОВОЙ ЛОГИКИ ---

                // Обновляем CollectionViewSource Source, чтобы он отслеживал изменения в коллекции
                Components_CVS.Source = Components;

                _logger.LogInformation($"Загружено {Components.Count} уникальных компонентов (суммарные количества) для сборки {partNumber}.");

                // Обновляем группировку
                RefreshGrouping();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрузке структуры сборки {partNumber}");
            }
        }

        // Вспомогательный метод для загрузки изображения
        private BitmapImage? LoadImageFromBytes(byte[] imageData)
        {
            try
            {
                using var ms = new System.IO.MemoryStream(imageData);
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze(); // Оптимизация для UI Thread
                return image;
            }
            catch
            {
                return null;
            }
        }

        // Метод обновления группировки
        private void RefreshGrouping()
        {
            Components_CVS.GroupDescriptions.Clear();

            switch (SelectedGroupingMode)
            {
                case "По материалу":
                    Components_CVS.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ComponentItemViewModel.Material)));
                    break;
                case "По типу":
                    Components_CVS.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ComponentItemViewModel.ComponentTypeDisplay)));
                    break;
                default:
                    break;
            }

            // Обновляем View
            Components_CVS.View.Refresh();
        }

        // Метод редактирования процесса
        private void EditProcess()
        {
            var selectedComponents = Components.Where(c => c.IsSelected).ToList();
            if (selectedComponents.Any())
            {
                _logger.LogInformation($"Открытие окна редактирования процесса для {selectedComponents.Count} компонентов.");
                // Здесь будет вызов окна OperationSelectionWindow
                // var dialog = new OperationSelectionWindow(selectedComponents);
                // dialog.ShowDialog();
                // После закрытия диалога, снимаем выделение
                DeselectAllComponents();
            }
        }

        // Вспомогательный метод для снятия выделения
        private void DeselectAllComponents()
        {
            foreach (var component in Components)
            {
                component.IsSelected = false;
            }
            NotifyHasSelectedComponentsChanged(); // Уведомляем, что выделение изменилось
        }
    }
}