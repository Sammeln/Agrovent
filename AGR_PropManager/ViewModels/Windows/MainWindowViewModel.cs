// File: ViewModels/MainWindowViewModel.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AGR_PropManager.ViewModels.Base;
using System.Windows.Input;
using AGR_PropManager.Infrastructure.Commands;
using Agrovent.DAL;
using System.Windows.Media.Imaging;
using Agrovent.Infrastructure.Enums;
using AGR_PropManager.Views;
using Agrovent.DAL.Entities.TechProcess;
using AGR_PropManager.ViewModels.Components;
using AGR_PropManager.ViewModels.TechProcess;
using Agrovent.DAL.Services.Repositories;
using System.Windows.Media.Media3D;
using AGR_PropManager.ViewModels.Reports;
using AGR_PropManager.Views.Reports;
using System.Windows; // Add this for the View/Window


namespace AGR_PropManager.ViewModels.Windows
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly DataContext _dataContext;
        private readonly UnitOfWork _unitOfWork;
        private readonly ILogger _logger;

        private ImportClassifierReportViewModel _importClassifierReportViewModel;
        private TreeImportReportViewModel _treeImportReportViewModel;
        private TechOpsImportReportViewModel _techOpsImportReportViewModel;
        #region CTOR

        public MainWindowViewModel(
            DataContext dataContext,
            ILogger<MainWindowViewModel>? logger,
            UnitOfWork unitOfWork)
        {
            _dataContext = dataContext;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Commands

        #region LoadStructureCommand
        private ICommand _LoadStructureCommand;
        public ICommand LoadStructureCommand => _LoadStructureCommand
            ??= new RelayCommand<string>(OnLoadStructureCommandExecuted, CanLoadStructureCommandExecute);
        private bool CanLoadStructureCommandExecute(string p) => !string.IsNullOrWhiteSpace(p); // Проверяем, что PartNumber не пуст
        private async void OnLoadStructureCommandExecuted(string partNumber)
        {
            await LoadAssemblyStructureAsync(partNumber);
            if (Components.Count != 0)
            {
                DataVisible = "Visible";
            }
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

        #region InsertTechProcessCommand
        private ICommand _InsertTechProcessCommand;
        public ICommand InsertTechProcessCommand => _InsertTechProcessCommand
            ??= new RelayCommand(OnInsertTechProcessCommandExecuted, CanInsertTechProcessCommandExecute); // Изменен тип на RelayCommand<string>
        private bool CanInsertTechProcessCommandExecute(object p) => true; // Пока всегда разрешено
        private void OnInsertTechProcessCommandExecuted(object p) // Изменен тип параметра
        {
            EditProcess();
        }
        #endregion

        #region SelectComponentCommand
        private ICommand _SelectComponentCommand;
        public ICommand SelectComponentCommand => _SelectComponentCommand
            ??= new RelayCommand(OnSelectComponentCommandExecuted, CanSelectComponentCommandExecute);
        private bool CanSelectComponentCommandExecute(object p) => true;
        private void OnSelectComponentCommandExecuted(object p)
        {
            foreach (var item in SelectedComponents)
            {
                item.IsSelected = !item.IsSelected;
            }
        }
        #endregion


        #region REPORTS

        #region ShowImportClassifierReportCommand
        private ICommand _ShowImportClassifierReportCommand;
        public ICommand ShowImportClassifierReportCommand => _ShowImportClassifierReportCommand
            ??= new RelayCommand(OnShowImportClassifierReportCommandExecuted, CanShowImportClassifierReportCommandExecute);
        private bool CanShowImportClassifierReportCommandExecute(object p) => true;
        private void OnShowImportClassifierReportCommandExecuted(object p)
        {
            var mainComponents = Components;

            try
            {
                _importClassifierReportViewModel = new ImportClassifierReportViewModel(mainComponents);

                var reportWindow = new ImportClassifierReportWindow(_importClassifierReportViewModel)
                {
                    Width = 1000,
                    Height = 700,
                    ResizeMode = ResizeMode.CanResizeWithGrip,
                };

                reportWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии отчета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region ShowTreeImportReportCommand
        private ICommand _ShowTreeImportReportCommand;
        public ICommand ShowTreeImportReportCommand => _ShowTreeImportReportCommand
            ??= new RelayCommand(OnShowTreeImportReportCommandExecuted, CanShowTreeImportReportCommandExecute);
        private bool CanShowTreeImportReportCommandExecute(object p) => true;
        private void OnShowTreeImportReportCommandExecuted(object p)
        {
            var mainComponent = Components?.FirstOrDefault();
            if (mainComponent == null)
            {
                MessageBox.Show("Нет доступных компонентов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _treeImportReportViewModel = new TreeImportReportViewModel(mainComponent, _unitOfWork);

                // Display the view in a new window
                var reportWindow = new TreeImportReportWindow(_treeImportReportViewModel)
                {
                    Width = 1200,
                    Height = 800,
                    ResizeMode = ResizeMode.CanResizeWithGrip
                };
                reportWindow.ShowDialog(); // Modal
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии отчета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion


        #region ShowTechOpsImportReportCommand
        private ICommand _ShowTechOpsImportReportCommand;
        public ICommand ShowTechOpsImportReportCommand => _ShowTechOpsImportReportCommand
            ??= new RelayCommand(OnShowTechOpsImportReportCommandExecuted, CanShowTechOpsImportReportCommandExecute);
        private bool CanShowTechOpsImportReportCommandExecute(object p) => true;
        private void OnShowTechOpsImportReportCommandExecuted(object p)
        {

            if (Components == null || !Components.Any())
            {
                MessageBox.Show("Нет доступных компонентов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _techOpsImportReportViewModel = new TechOpsImportReportViewModel(Components);

                // Display the view in a new window
                var reportWindow = new TechOpsImportReportWindow(_techOpsImportReportViewModel)
                {
                    Width = 1000,
                    Height = 800,
                    ResizeMode = ResizeMode.CanResizeWithGrip
                };
                reportWindow.ShowDialog(); // Modal
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии отчета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion 

        #endregion

        #endregion

        #region PROPS

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

        #region Коллекция для хранения данных
        private ObservableCollection<ComponentItemViewModel> _Components = new ObservableCollection<ComponentItemViewModel>();
        public ObservableCollection<ComponentItemViewModel> Components
        {
            get => _Components;
            set => Set(ref _Components, value);
        }
        #endregion

        #region CollectionViewSource
        private CollectionViewSource Components_CVS = new CollectionViewSource();
        public ICollectionView ComponentsView => Components_CVS?.View;
        #endregion

        #region Коллекция для хранения данных
        private ObservableCollection<ComponentItemViewModel> _SelectedComponents = new ObservableCollection<ComponentItemViewModel>();
        public ObservableCollection<ComponentItemViewModel> SelectedComponents
        {
            get => _SelectedComponents;
            set => Set(ref _SelectedComponents, value);
        }
        #endregion
        
        #region Property - IsLoaded
        private string _DataVisible = "Collapsed";
        public string DataVisible
        {
            get => _DataVisible;
            set => Set(ref _DataVisible, value);
        }
        #endregion

        #endregion

        #region Methods

        #region Метод загрузки структуры сборки
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
                var assemblyVersion = await _dataContext.ComponentVersions
                    .Include(cv => cv.Component) // Загружаем связанный компонент
                        .ThenInclude(c => c.TechnologicalProcess)
                    .Include(c => c.Material)
                    .Include(cv => cv.Properties) // Загружаем свойства
                    .Where(cv => cv.Component.PartNumber == partNumber)
                    .OrderByDescending(cv => cv.Version) // Берем последнюю версию
                    .FirstOrDefaultAsync();

                if (assemblyVersion == null)
                {
                    _logger.LogWarning($"Сборка с PartNumber {partNumber} не найдена.");
                    return;
                }
                
                //добавляем саму сборку
                var vmMainAssembly = new ComponentItemViewModel(_dataContext, _unitOfWork)
                {
                    PartNumber = assemblyVersion.Component.PartNumber,
                    Name = assemblyVersion.Name,
                    Version = assemblyVersion.Version,
                    Quantity = 1,
                    Material = "",
                    Paint = assemblyVersion.Material?.Paint,
                    BendCount = 0,
                    ContourLength = 0,
                    ComponentType = assemblyVersion.ComponentType,
                    PreviewImage = LoadImageFromBytes(assemblyVersion.PreviewImage),
                    Article = assemblyVersion.AvaArticleArticle.ToString(),
                    AvaArticle = assemblyVersion.AvaArticle
                };
                
                Components.Add(vmMainAssembly);

                // Найдем все элементы структуры для этой версии сборки
                var assemblyStructureEntries = await _unitOfWork.ComponentRepository.GetAssemblyStructureRecursive(partNumber, assemblyVersion.Version);

                var groupedEntries = assemblyStructureEntries
                    .GroupBy(s => s.ChildComponentVersion.Component.PartNumber) // Группируем по PartNumber
                    .Select(g => new
                    {
                        PartNumber = g.Key,
                        FirstComponentVersion = g.First().ChildComponentVersion,
                        TotalQuantity = g.Sum(s => s.Quantity) // Суммируем Quantity
                    })
                    .ToList();

                // Загрузим все связанные TechnologicalProcesses за один запрос
                var partNumbersInStructure = groupedEntries.Select(entry => entry.PartNumber).Distinct().ToList();
                var techProcesses = await _dataContext.TechProcesses // Предполагаем, что DbSet называется TechProcesses
                    .Include(tp => tp.Operations)
                    .Where(tp => partNumbersInStructure.Contains(tp.PartNumber))
                    .ToListAsync();

                foreach (var groupedEntry in groupedEntries)
                {
                    var compVer = groupedEntry.FirstComponentVersion;
                    var comp = compVer.Component;
                    var partNum = comp.PartNumber;
                    var totalQty = groupedEntry.TotalQuantity; // Используем суммарное количество
                    var props = compVer.Properties;
                    var materials = compVer.Material; // Предполагаем, что Material связан с ComponentVersion

                    // Загрузим нужные свойства из ComponentProperties (берем из первой версии в группе)
                    // Используем AGR_PropertyNames для получения свойств
                    var materialProp = materials?.BaseMaterial;
                    var paintProp = materials?.Paint;
                    var bendCountProp = props.FirstOrDefault(p => p.Name == AGR_PropertyNames.BlankBends)?.Value; // Получаем .Value
                    var blankOuterContourProp = props.FirstOrDefault(p => p.Name == AGR_PropertyNames.BlankOuterContour)?.Value; // Получаем .Value
                    var blankInnerContourProp = props.FirstOrDefault(p => p.Name == AGR_PropertyNames.BlankInnerContour)?.Value; // Получаем .Value

                    // Парсим длину контура
                    var contourSum = (decimal.TryParse(blankInnerContourProp, out decimal innerLen) ? innerLen : 0) +
                                     (decimal.TryParse(blankOuterContourProp, out decimal outerLen) ? outerLen : 0);

                    var previewImage = compVer.PreviewImage != null ? LoadImageFromBytes(compVer.PreviewImage) : null;

                    // Найдем соответствующий техпроцесс по PartNumber
                    var techProcess = techProcesses.FirstOrDefault(tp => tp.PartNumber == partNum);


                    var vm = new ComponentItemViewModel(_dataContext, _unitOfWork)
                    {
                        PartNumber = partNum,
                        Name = compVer.Name,
                        Quantity = totalQty,
                        Version = compVer.Version,
                        Material = materialProp ?? "",
                        Paint = paintProp ?? "",
                        BendCount = int.TryParse(bendCountProp, out int bc) ? bc : 0,
                        ContourLength = contourSum,
                        ComponentType = compVer.ComponentType,
                        PreviewImage = previewImage,
                        Article = compVer.AvaArticleArticle.ToString(),
                        AvaArticle = compVer.AvaArticle
                    };
                    var operationsList = techProcess?.Operations.Select(op => new TechOperationViewModel(op) { ParentComponent = vm}).ToList();

                    // Заполняем Operations и TechProcessSummary из найденного TechnologicalProcess
                    if (operationsList != null && operationsList.Count != 0)
                    {
                        vm.Operations = new ObservableCollection<TechOperationViewModel>(operationsList);
                        vm.TechnologicalProcessModel.Operations = vm.Operations;
                    }

                    var propsVM = props.Select(prop => new AGR_PropertyViewModel(prop)).ToList();
                    if (propsVM != null)
                    {
                        foreach (var prop in propsVM)
                        {
                            vm.PropertiesCollection.Add(prop);
                        }
                    }



                    Components.Add(vm);
                }

                // Обновляем CollectionViewSource Source, чтобы он отслеживал изменения в коллекции
                Components_CVS.Source = Components;

                _logger.LogInformation($"Загружено {Components.Count} уникальных компонентов (суммарные количества) для сборки {partNumber}.");

                // Обновляем группировку
                RefreshGrouping();
                ComponentsView.Refresh();

                OnPropertyChanged(nameof(ComponentsView));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрузке структуры сборки {partNumber}");
            }
        }

        #endregion

        #region Вспомогательный метод для загрузки изображения
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
        #endregion

        #region Метод обновления группировки
        private void RefreshGrouping()
        {
            Components_CVS.GroupDescriptions.Clear();
            ComponentsView.Filter = null;

            switch (SelectedGroupingMode)
            {
                case "По материалу":
                Components_CVS.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ComponentItemViewModel.Material)));
                ComponentsView.Filter = FilterGroupedItemByMaterial;
                break;
                case "По типу":
                Components_CVS.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ComponentItemViewModel.ComponentType)));

                break;
                default:
                break;
            }

            // Обновляем View
            Components_CVS.View.Refresh();
        }
        #endregion

        #region Доп фильтр при группировке по материалу

        private bool FilterGroupedItemByMaterial(object item)
        {
            if (item is not ComponentItemViewModel compItem) return false;
            if (compItem.ComponentType is AGR_ComponentType_e.Purchased) return false;

            return true;
        }

        #endregion

        #region Метод редактирования процесса
        private void EditProcess()
        {
            var selectedComponents = Components.Where(c => c.IsSelected).ToList();
            if (selectedComponents.Any())
            {
                _logger.LogInformation($"Открытие окна редактирования процесса для {selectedComponents.Count} компонентов.");
                var operationSelectionViewModel = new OperationSelectionViewModel(
                    new ObservableCollection<ComponentItemViewModel>(selectedComponents)
                    , _unitOfWork
                    , _logger);
                var selectionWindow = new OperationSelectionWindow(operationSelectionViewModel);

                // После закрытия диалога, снимаем выделение
                DeselectAllComponents();
            }
        }
        #endregion

        #region  Вспомогательный метод для снятия выделения
        private void DeselectAllComponents()
        {
            foreach (var component in Components)
            {
                component.IsSelected = false;
            }
            NotifyHasSelectedComponentsChanged(); // Уведомляем, что выделение изменилось
        }
        #endregion 

        #endregion
    }
}