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
using Agrovent.DAL.Entities.Components;
using AGR_PropManager.ViewModels.Windows;
using System.Linq;
using Xarial.XCad.Documents;


namespace AGR_PropManager.ViewModels.Windows
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly DataContext _dataContext;
        private readonly UnitOfWork _unitOfWork;
        private readonly IAGR_ComponentRepository _componentRepository;
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
            _componentRepository = _unitOfWork.ComponentRepository;

            // Инициализация коллекции вкладок
            OpenTabs = new ObservableCollection<TabItemViewModel>();

            // Создаем вкладку классификатора при старте
            var classifierTab = new ClassifierTabViewModel();
            OpenTabs.Add(classifierTab);
            SelectedTab = classifierTab;

            // Инициализация коллекции шаблонов операций
            TemplateOperations = new ObservableCollection<TemplateOperationItemViewModel>();

            _ = InitializeAsync();
        }

        #endregion
        private async Task InitializeAsync()
        {
            try
            {
                // Выполняем загрузку последовательно, чтобы избежать конфликта DbContext
                await LoadTemplateOperationsAsync();
                await LoadClassifierDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации MainWindowViewModel");
            }
        }
        #region LoadClassifierDataAsync
        public async Task LoadClassifierDataAsync()
        {
            try
            {
                _logger.LogInformation("Загрузка данных классификатора...");
                // Группируем по PartNumber и берем последнюю версию для каждого
                var latestVersions = await _unitOfWork.ComponentRepository.GetAllLatestComponentVersionsAsync();// _componentRepository.GetAllLatestComponentVersionsAsync();

                var classifierTab = OpenTabs.OfType<ClassifierTabViewModel>().FirstOrDefault();
                if (classifierTab == null)
                {
                    classifierTab = new ClassifierTabViewModel();
                    OpenTabs.Add(classifierTab);
                }

                foreach (var cv in latestVersions)
                {
                    var item = new ClassifierItemViewModel
                    {
                        Id = cv.Id,
                        PartNumber = cv.Component.PartNumber,
                        Name = cv.Name,
                        SavedDate = cv.CreatedAt,
                        PreviewImage = cv.PreviewImage != null ? LoadImageFromBytes(cv.PreviewImage) : null
                    };
                    classifierTab.ClassifierItems.Add(item);
                }

                classifierTab.InitializeView();
                _logger.LogInformation($"Загружено {classifierTab.ClassifierItems.Count} записей классификатора.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных классификатора");
            }
        }
        #endregion

        #region LoadTemplateOperationsAsync
        public async Task LoadTemplateOperationsAsync()
        {
            try
            {
                _logger.LogInformation("Загрузка шаблонов операций...");
                
                var templates = await _dataContext.TemplateOperations
                    .Include(to => to.Workstation)
                    .ToListAsync();

                TemplateOperations.Clear();
                foreach (var template in templates)
                {
                    TemplateOperations.Add(new TemplateOperationItemViewModel(template));
                }
                
                _logger.LogInformation($"Загружено {TemplateOperations.Count} шаблонов операций.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке шаблонов операций");
            }
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

        #region PROPS - Tabs

        #region OpenTabs
        private ObservableCollection<TabItemViewModel> _openTabs;
        public ObservableCollection<TabItemViewModel> OpenTabs
        {
            get => _openTabs;
            set => Set(ref _openTabs, value);
        }
        #endregion

        #region SelectedTab
        private TabItemViewModel _selectedTab;
        public TabItemViewModel SelectedTab
        {
            get => _selectedTab;
            set => Set(ref _selectedTab, value);
        }
        #endregion

        #region TemplateOperations
        private ObservableCollection<TemplateOperationItemViewModel> _templateOperations;
        public ObservableCollection<TemplateOperationItemViewModel> TemplateOperations
        {
            get => _templateOperations;
            set => Set(ref _templateOperations, value);
        }
        #endregion

        #region SelectedTemplateOperation
        private TemplateOperationItemViewModel _selectedTemplateOperation;
        public TemplateOperationItemViewModel SelectedTemplateOperation
        {
            get => _selectedTemplateOperation;
            set => Set(ref _selectedTemplateOperation, value);
        }
        #endregion

        #endregion

        #region Commands - Tabs

        #region OpenClassifierItemCommand
        private ICommand _OpenClassifierItemCommand;
        public ICommand OpenClassifierItemCommand => _OpenClassifierItemCommand
            ??= new RelayCommand<ClassifierItemViewModel>(OnOpenClassifierItemCommandExecuted, CanOpenClassifierItemCommandExecute);
        private bool CanOpenClassifierItemCommandExecute(object o) => o != null;
        private async void OnOpenClassifierItemCommandExecuted(object o)
        {
            if (o == null) return;

            var item = o as ClassifierItemViewModel;
            try
            {
                _logger.LogInformation($"Открытие компонента {item.PartNumber} для редактирования...");

                // Загружаем полную информацию о компоненте
                var componentVersion = await _dataContext.ComponentVersions
                    .Include(cv => cv.Component)
                        .ThenInclude(c => c.TechnologicalProcess)
                            .ThenInclude(tp => tp.Operations)
                    .Include(cv => cv.Material)
                    .Include(cv => cv.Properties)
                    .Include(cv => cv.AvaArticle)
                    .Where(cv => cv.Component.PartNumber == item.PartNumber)
                    .OrderByDescending(cv => cv.Version)
                    .FirstOrDefaultAsync();

                if (componentVersion == null)
                {
                    _logger.LogWarning($"Компонент {item.PartNumber} не найден.");
                    MessageBox.Show($"Компонент {item.PartNumber} не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Создаем ComponentItemViewModel
                var componentVM = new ComponentItemViewModel(_dataContext, _unitOfWork)
                {
                    PartNumber = componentVersion.Component.PartNumber,
                    Name = componentVersion.Name,
                    Version = componentVersion.Version,
                    Quantity = 1,
                    Material = componentVersion.Material?.BaseMaterial ?? "",
                    Paint = componentVersion.Material?.Paint ?? "",
                    ComponentType = componentVersion.ComponentType,
                    PreviewImage = componentVersion.PreviewImage != null ? LoadImageFromBytes(componentVersion.PreviewImage) : null,
                    Article = componentVersion.AvaArticleArticle?.ToString() ?? "",
                    AvaArticle = componentVersion.AvaArticle
                };

                // Загружаем свойства
                var propsVM = componentVersion.Properties.Select(prop => new AGR_PropertyViewModel(prop)).ToList();
                foreach (var prop in propsVM)
                {
                    componentVM.PropertiesCollection.Add(prop);
                }

                // Загружаем техпроцесс и операции
                var techProcess = componentVersion.Component.TechnologicalProcess;
                if (techProcess != null && techProcess.Operations != null)
                {
                    var operationsList = techProcess.Operations.Select(op => new TechOperationViewModel(op) { ParentComponent = componentVM }).ToList();
                    componentVM.Operations = new ObservableCollection<TechOperationViewModel>(operationsList);
                    componentVM.TechnologicalProcessModel.Operations = componentVM.Operations;
                }

                // Проверяем, есть ли уже вкладка для этого компонента
                var existingTab = OpenTabs.OfType<TechProcessEditorTabViewModel>()
                    .FirstOrDefault(t => t.Component.PartNumber == componentVM.PartNumber);

                if (existingTab != null)
                {
                    // Переключаемся на существующую вкладку
                    SelectedTab = existingTab;
                }
                else
                {
                    // Создаем новую вкладку редактора техпроцесса
                    var editorTab = new TechProcessEditorTabViewModel(componentVM, _unitOfWork, _logger);
                    OpenTabs.Add(editorTab);
                    SelectedTab = editorTab;
                }

                _logger.LogInformation($"Вкладка для {item.PartNumber} открыта.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при открытии компонента {item.PartNumber}");
                MessageBox.Show($"Ошибка при открытии компонента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region CloseTabCommand
        private ICommand _CloseTabCommand;
        public ICommand CloseTabCommand => _CloseTabCommand
            ??= new RelayCommand<TabItemViewModel>(OnCloseTabCommandExecuted, CanCloseTabCommandExecute);
        private bool CanCloseTabCommandExecute(TabItemViewModel tab) => tab != null && !tab.IsClassifierTab; // Нельзя закрыть вкладку классификатора
        private void OnCloseTabCommandExecuted(TabItemViewModel tab)
        {
            if (tab == null || tab.IsClassifierTab) return;

            // Если закрыли текущую вкладку, переключаемся на другую
            if (SelectedTab == tab && OpenTabs.Count > 0)
            {
                SelectedTab = OpenTabs[0];
            }

            var tabIndex = OpenTabs.IndexOf(tab);

            if (tabIndex >= 0)
            {
                OpenTabs.RemoveAt(tabIndex);
            }
            

            _logger.LogInformation($"Вкладка '{tab.TabHeader}' закрыта.");
        }
        #endregion

        #region AddOperationCommand
        private ICommand _AddOperationCommand;
        public ICommand AddOperationCommand => _AddOperationCommand
            ??= new RelayCommand<ComponentItemViewModel>(OnAddOperationCommandExecuted, CanAddOperationCommandExecute);
        private bool CanAddOperationCommandExecute(ComponentItemViewModel component) => component != null && SelectedTemplateOperation != null;
        private void OnAddOperationCommandExecuted(ComponentItemViewModel component)
        {
            if (component == null || SelectedTemplateOperation == null) return;

            // Создаем новую операцию на основе выбранного шаблона
            var newOperation = new TechOperationViewModel(SelectedTemplateOperation)
            {
                SequenceNumber = component.Operations.Count + 1,
                ParentComponent = component
            };
            
            component.Operations.Add(newOperation);
            _logger.LogInformation($"Добавлена операция '{newOperation.Name}' для {component.PartNumber}");
        }
        #endregion

        #region SaveTechProcessCommand
        private ICommand _SaveTechProcessCommand;
        public ICommand SaveTechProcessCommand => _SaveTechProcessCommand
            ??= new RelayCommand<ComponentItemViewModel>(OnSaveTechProcessCommandExecuted, CanSaveTechProcessCommandExecute);
        private bool CanSaveTechProcessCommandExecute(ComponentItemViewModel component) => component != null;
        private async void OnSaveTechProcessCommandExecuted(ComponentItemViewModel component)
        {
            if (component == null) return;

            try
            {
                _logger.LogInformation($"Сохранение техпроцесса для {component.PartNumber}...");

                // Получаем техпроцесс из БД или создаем новый
                var techProcess = await _dataContext.TechProcesses
                    .Include(tp => tp.Operations)
                    .FirstOrDefaultAsync(tp => tp.PartNumber == component.PartNumber);

                if (techProcess == null)
                {
                    techProcess = new TechnologicalProcess
                    {
                        PartNumber = component.PartNumber
                    };
                    _dataContext.TechProcesses.Add(techProcess);
                }

                // Обновляем операции
                techProcess.Operations.Clear();
                foreach (var opVM in component.Operations)
                {
                    var operation = new Operation
                    {
                        SequenceNumber = opVM.SequenceNumber,
                        WorkstationName = opVM.WorkstationName,
                        Name = opVM.Name,
                        CostPerHour = opVM.CostPerHour,
                        TechnologicalProcessId = techProcess.Id
                    };
                    techProcess.Operations.Add(operation);
                }

                await _dataContext.SaveChangesAsync();
                _logger.LogInformation($"Техпроцесс для {component.PartNumber} успешно сохранен.");
                MessageBox.Show("Техпроцесс успешно сохранен!", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при сохранении техпроцесса для {component.PartNumber}");
                MessageBox.Show($"Ошибка при сохранении техпроцесса: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #endregion

    }
}