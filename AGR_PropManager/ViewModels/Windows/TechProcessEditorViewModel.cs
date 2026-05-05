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
using Agrovent.DAL.Entities.TechProcess;
using AGR_PropManager.ViewModels.Components;
using AGR_PropManager.ViewModels.TechProcess;
using Agrovent.DAL.Services.Repositories;
using System.Windows;
using AGR_PropManager.Views;

namespace AGR_PropManager.ViewModels.Windows
{
    /// <summary>
    /// ViewModel для диалогового окна редактора технологического процесса
    /// Содержит функционал группировки и редактирования компонентов
    /// </summary>
    public class TechProcessEditorViewModel : BaseViewModel
    {
        private readonly DataContext _dataContext;
        private readonly UnitOfWork _unitOfWork;
        private readonly ILogger _logger;
        private readonly ComponentItemViewModel _selectedComponent;

        #region CTOR

        public TechProcessEditorViewModel(
            ComponentItemViewModel selectedComponent,
            DataContext dataContext,
            ILogger? logger,
            UnitOfWork unitOfWork)
        {
            _selectedComponent = selectedComponent;
            _dataContext = dataContext;
            _logger = logger;
            _unitOfWork = unitOfWork;

            // Инициализируем коллекцию компонентов из выбранных
            LoadAssemblyStructureAsync(selectedComponent.PartNumber);

            // Настраиваем CollectionViewSource
            Components_CVS.Source = Components;
            RefreshGrouping();
        }

        // Конструктор по умолчанию (для Design-time)
        public TechProcessEditorViewModel() { }

        #endregion

        #region Commands

        #region CloseCommand
        private ICommand _CloseCommand;
        public ICommand CloseCommand => _CloseCommand
            ??= new RelayCommand(OnCloseCommandExecuted, CanCloseCommandExecute);
        private bool CanCloseCommandExecute(object p) => true;
        private void OnCloseCommandExecuted(object p)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region SaveCommand
        private ICommand _SaveCommand;
        public ICommand SaveCommand => _SaveCommand
            ??= new RelayCommand(OnSaveCommandExecuted, CanSaveCommandExecute);
        private bool CanSaveCommandExecute(object p) => true;
        private async void OnSaveCommandExecuted(object p)
        {
            await SaveChangesAsync();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region InsertTechProcessCommand
        private ICommand _InsertTechProcessCommand;
        public ICommand InsertTechProcessCommand => _InsertTechProcessCommand
            ??= new RelayCommand(OnInsertTechProcessCommandExecuted, CanInsertTechProcessCommandExecute);
        private bool CanInsertTechProcessCommandExecute(object p) => HasSelectedComponents;
        private void OnInsertTechProcessCommandExecuted(object p)
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

        #region DeleteOperationCommand (для удаления операций из главного DataGrid)
        private ICommand _DeleteOperationCommand;
        public ICommand DeleteOperationCommand => _DeleteOperationCommand
            ??= new RelayCommand<TechOperationViewModel>(OnDeleteOperationCommandExecuted, CanDeleteOperationCommandExecute);
        private bool CanDeleteOperationCommandExecute(TechOperationViewModel? p) => p != null;
        private async void OnDeleteOperationCommandExecuted(TechOperationViewModel? operation)
        {
            if (operation == null || operation.ParentComponent == null) return;
            
            if (operation.TechProcess != null)
            {
                var entOp = _dataContext.Operations.FirstOrDefault(o => 
                    o.TechnologicalProcessId == operation.TechProcess.Id && 
                    o.SequenceNumber == operation.SequenceNumber);
                if (entOp != null)
                {
                    _dataContext.Operations.Remove(entOp);
                    await _dataContext.SaveChangesAsync();
                    operation.ParentComponent.Operations.Remove(operation);
                }
            }
            else
            {
                operation.ParentComponent.Operations.Remove(operation);
            }
            OnPropertyChanged(nameof(ComponentItemViewModel.HasZeroTimeOperations));
        }
        #endregion

        #endregion

        #region PROPS

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
        public bool HasSelectedComponents => Components.Any(c => c.IsSelected);
        private void NotifyHasSelectedComponentsChanged()
        {
            OnPropertyChanged(nameof(HasSelectedComponents));
        }
        #endregion

        #region Коллекция компонентов
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

        #region Коллекция выбранных компонентов
        private ObservableCollection<ComponentItemViewModel> _SelectedComponents = new ObservableCollection<ComponentItemViewModel>();
        public ObservableCollection<ComponentItemViewModel> SelectedComponents
        {
            get => _SelectedComponents;
            set => Set(ref _SelectedComponents, value);
        }
        #endregion

        #endregion

        #region Methods

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

            Components_CVS.View.Refresh();
        }
        #endregion

        #region Фильтр при группировке по материалу
        private bool FilterGroupedItemByMaterial(object item)
        {
            if (item is not ComponentItemViewModel compItem) return false;
            if (compItem.ComponentType is AGR_ComponentType_e.Purchased) return false;

            return true;
        }
        #endregion

        #region Метод редактирования процесса (открывает окно выбора операции)
        private void EditProcess()
        {
            var selectedComponents = Components.Where(c => c.IsSelected).ToList();
            if (selectedComponents.Any())
            {
                _logger?.LogInformation($"Открытие окна выбора операции для {selectedComponents.Count} компонентов.");
                var operationSelectionViewModel = new OperationSelectionViewModel(
                    new ObservableCollection<ComponentItemViewModel>(selectedComponents),
                    _unitOfWork,
                    _logger);
                
                var selectionWindow = new OperationSelectionWindow(operationSelectionViewModel);
                DeselectAllComponents();
            }
        }
        #endregion

        #region Сохранение изменений
        private async Task SaveChangesAsync()
        {
            try
            {
                await _unitOfWork.CompleteAsync();
                _logger?.LogInformation("Изменения сохранены.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при сохранении изменений.");
            }
        }
        #endregion

        #region Снятие выделения со всех компонентов
        private void DeselectAllComponents()
        {
            foreach (var component in Components)
            {
                component.IsSelected = false;
            }
            NotifyHasSelectedComponentsChanged();
        }
        #endregion

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
                    var operationsList = techProcess?.Operations.Select(op => new TechOperationViewModel(op) { ParentComponent = vm }).ToList();

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

        #endregion

        public event EventHandler? CloseRequested;
    }
}
