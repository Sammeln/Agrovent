// File: ViewModels/TaskPane/AGR_ComponentRegistryTaskPaneVM.cs
using Agrovent.DAL;
using Agrovent.DAL.Entities.Components;
using Agrovent.DAL.Services.Repositories;
using Agrovent.Infrastructure;
using Agrovent.Infrastructure.AGR_Converters;
using Agrovent.Infrastructure.Commands; // Для RelayCommand
using Agrovent.Infrastructure.Converters;
using Agrovent.Infrastructure.Enums;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.Windows.Details;
using Agrovent.Views.Windows.Details;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO; // Для File.Exists
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input; // Для CollectionViewSource
using Xarial.XCad.Base;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.TaskPane
{
    public class AGR_ComponentRegistryTaskPaneVM : BaseViewModel
    {
        private readonly IAGR_ComponentRepository _componentRepository;
        private readonly ILogger<AGR_ComponentRegistryTaskPaneVM> _logger;
        private static readonly AGR_AvaTypeConverter _avaTypeConverter = new AGR_AvaTypeConverter();
        private static readonly AGR_ComponentTypeConverter _componentTypeConverter = new AGR_ComponentTypeConverter();

        #region CTOR
        public AGR_ComponentRegistryTaskPaneVM(
    IAGR_ComponentRepository componentRepository,
    ILogger<AGR_ComponentRegistryTaskPaneVM> logger)
        {
            _componentRepository = componentRepository ?? throw new ArgumentNullException(nameof(componentRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Инициализация CollectionViewSource
            RegistryItemsView = CollectionViewSource.GetDefaultView(RegistryItems);
            RegistryItemsView.Filter = FilterRegistryItems; // Устанавливаем метод фильтрации

            // Инициализация коллекций для ComboBox
            AvailableAvaTypes = new ObservableCollection<string>
            {
                _avaTypeConverter.Convert(AGR_AvaType_e.Component, typeof(string), null, CultureInfo.CurrentCulture) as string,
                _avaTypeConverter.Convert(AGR_AvaType_e.Production, typeof(string), null, CultureInfo.CurrentCulture) as string,
                _avaTypeConverter.Convert(AGR_AvaType_e.Purchased, typeof(string), null, CultureInfo.CurrentCulture) as string,
                _avaTypeConverter.Convert(AGR_AvaType_e.DontBuy, typeof(string), null, CultureInfo.CurrentCulture) as string,
                _avaTypeConverter.Convert(AGR_AvaType_e.VirtualComponent, typeof(string), null, CultureInfo.CurrentCulture) as string,
                "Все типы"

            };

            AvailableComponentTypes = new ObservableCollection<string>
            {
                _componentTypeConverter.Convert(AGR_ComponentType_e.Assembly, typeof(string), null, CultureInfo.CurrentCulture) as string,
                _componentTypeConverter.Convert(AGR_ComponentType_e.SheetMetallPart, typeof(string), null, CultureInfo.CurrentCulture) as string,
                _componentTypeConverter.Convert(AGR_ComponentType_e.Part, typeof(string), null, CultureInfo.CurrentCulture) as string,
                _componentTypeConverter.Convert(AGR_ComponentType_e.Purchased, typeof(string), null, CultureInfo.CurrentCulture) as string,
                "Все типы"
            };

            // Загружаем данные при создании VM (или вызывайте LoadDataCommand извне)
            // Task.Run(async () => await LoadDataAsync()); // Не рекомендуется запускать асинхронный код в конструкторе
        } 
        #endregion

        // Команда для загрузки данных

        #region LoadDataCommand
        private ICommand _LoadDataCommand;
        public ICommand LoadDataCommand => _LoadDataCommand
            ??= new RelayCommand(OnLoadDataCommandExecuted, CanLoadDataCommandExecute);
        private bool CanLoadDataCommandExecute(object p) => true;
        private void OnLoadDataCommandExecuted(object p)
        {
            LoadDataAsync();
        }
        // Метод загрузки данных из БД
        private async Task LoadDataAsync()
        {

            try
            {
                _logger.LogInformation("Загрузка данных для реестра компонентов TaskPane...");

                var versions = await _componentRepository.GetAllLatestComponentVersionsAsync();

                var source = new ObservableCollection<ComponentVersion>(versions);

                // Очищаем текущую коллекцию
                RegistryItems.Clear();

                // Преобразуем сущности в VM и добавляем в коллекцию
                foreach (var version in versions)
                {
                    var itemVm = new AGR_ComponentRegistryItemVM(version, AGR_Options.StorageRootFolderPath);
                    RegistryItems.Add(itemVm);
                }

                _logger.LogInformation($"Загружено {RegistryItems.Count} записей в реестр компонентов TaskPane.");

                // Обновляем фильтр, если был текст поиска
                RegistryItemsView.Refresh();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных реестра компонентов TaskPane.");
                // Можно показать сообщение пользователю
            }
            //try
            //{
            //    _logger.LogInformation("Загрузка данных для реестра компонентов TaskPane...");

            //    var versions = await _componentRepository.GetAllLatestComponentVersionsAsync();

            //    var source = new ObservableCollection<ComponentVersion>(versions);

            //    // Очищаем текущую коллекцию
            //    RegistryItems.Clear();

            //    // Преобразуем сущности в VM и добавляем в коллекцию
            //    foreach (var version in versions)
            //    {
            //        var itemVm = new AGR_ComponentRegistryItemVM(version, _storageConfig.StorageRootFolder);
            //        RegistryItems.Add(itemVm);
            //    }

            //    _logger.LogInformation($"Загружено {RegistryItems.Count} записей в реестр компонентов TaskPane.");

            //    // Обновляем фильтр, если был текст поиска
            //    RegistryItemsView.Refresh();
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, "Ошибка при загрузке данных реестра компонентов TaskPane.");
            //    // Можно показать сообщение пользователю
            //}
        }

        #endregion

        // Команды для контекстного меню (скопированы из AGR_ComponentRegistryVM)
        #region OpenComponentCommand
        private ICommand _OpenComponentCommand;
        public ICommand OpenComponentCommand => _OpenComponentCommand
            ??= new RelayCommand<AGR_ComponentRegistryItemVM>(OnOpenComponentCommandExecuted, CanOpenComponentCommandExecute);
        private bool CanOpenComponentCommandExecute(AGR_ComponentRegistryItemVM p) => p != null && !string.IsNullOrEmpty(p.StoragePath) && File.Exists(p.StoragePath);
        private void OnOpenComponentCommandExecuted(AGR_ComponentRegistryItemVM selectedItem)
        {
            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.StoragePath)) return;

            var filePath = selectedItem.StoragePath;

            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"Команда 'Открыть': Файл не существует: {filePath}");
                return;
            }

            try
            {
                // Получаем ISwApplication
                var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    _logger.LogError("Команда 'Открыть': Не удалось получить ISwApplication.");
                    return;
                }

                // Проверяем, открыт ли документ
                var openDoc = swApp.Documents.FirstOrDefault(x => x.Path == filePath);
                if (openDoc != null)
                {
                    // Документ уже открыт, делаем его активным
                    swApp.Documents.Active = openDoc as ISwDocument;
                    _logger.LogDebug($"Команда 'Открыть': Документ уже открыт, активирован: {filePath}");
                }
                else
                {
                    // Документ не открыт, открываем
                    var newDoc = swApp.Documents.PreCreateFromPath(filePath);
                    newDoc.Commit(CancellationToken.None);
                    _logger.LogDebug($"Команда 'Открыть': Документ открыт: {filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Команда 'Открыть': Ошибка при открытии файла {filePath}");
            }
        }
        #endregion

        #region AddToAssemblyCommand
        private ICommand _AddToAssemblyCommand;
        public ICommand AddToAssemblyCommand => _AddToAssemblyCommand
            ??= new RelayCommand<AGR_ComponentRegistryItemVM>(OnAddToAssemblyCommandExecuted, CanAddToAssemblyCommandExecute);
        private bool CanAddToAssemblyCommandExecute(AGR_ComponentRegistryItemVM p)
        {
            if (p == null || string.IsNullOrEmpty(p.StoragePath) || !File.Exists(p.StoragePath)) return false;

            // Проверяем, активен ли сборочный документ
            var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
            if (swApp == null) return false;

            var activeDoc = swApp.Documents.Active;
            return activeDoc is ISwAssembly;
        }
        private void OnAddToAssemblyCommandExecuted(AGR_ComponentRegistryItemVM selectedItem)
        {
            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.StoragePath)) return;

            var filePath = selectedItem.StoragePath;

            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"Команда 'Добавить в сборку': Файл не существует: {filePath}");
                return;
            }

            try
            {
                var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    _logger.LogError("Команда 'Добавить в сборку': Не удалось получить ISwApplication.");
                    return;
                }

                // Проверяем, активен ли сборочный документ
                var activeDoc = swApp.Documents.Active;
                if (!(activeDoc is ISwAssembly swAssembly))
                {
                    _logger.LogWarning("Команда 'Добавить в сборку': Активный документ не является сборкой.");
                    return;
                }

                // Проверяем, открыт ли документ компонента
                IXDocument3D? compDoc = swApp.Documents.FirstOrDefault(x => x.Path == filePath) as IXDocument3D;
                if (compDoc == null)
                {
                    // Документ не открыт, открываем его
                    compDoc = swApp.Documents.PreCreateFromPath(filePath) as IXDocument3D;
                    if (compDoc == null)
                    {
                        _logger.LogError($"Команда 'Добавить в сборку': Не удалось открыть документ компонента: {filePath}");
                        return;
                    }
                    //compDoc.Commit(CancellationToken.None);
                    _logger.LogDebug($"Команда 'Добавить в сборку': Документ компонента открыт: {filePath}");
                }

                // Создаем шаблон компонента
                var xComp = swAssembly.Configurations.Active.Components.PreCreate<IXComponent>();
                if (xComp == null)
                {
                    _logger.LogError($"Команда 'Добавить в сборку': Не удалось создать шаблон компонента для {filePath}");
                    return;
                }

                // Устанавливаем ссылку на документ
                xComp.ReferencedDocument = compDoc;

                // Добавляем в сборку
                swAssembly.Configurations.Active.Components.Add(xComp);
                _logger.LogDebug($"Команда 'Добавить в сборку': Компонент добавлен в сборку: {filePath}");

                // Выделяем компонент
                xComp.Select(false);

                // Запускаем внутреннюю команду для перемещения компонента (Move Component)
                // 1993 - это ID команды "Move Component"
                swApp.Sw.RunCommand(1993, "");


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Команда 'Добавить в сборку': Ошибка при добавлении файла {filePath} в сборку.");
            }
        }
        #endregion

        #region ShowDetailsCommand
        private ICommand _ShowDetailsCommand;
        public ICommand ShowDetailsCommand => _ShowDetailsCommand
            ??= new RelayCommand<AGR_ComponentRegistryItemVM>(OnShowDetailsCommandExecuted, CanShowDetailsCommandExecute);
        private bool CanShowDetailsCommandExecute(AGR_ComponentRegistryItemVM p) => p != null; // Всегда доступна, если элемент выбран
        private void OnShowDetailsCommandExecuted(AGR_ComponentRegistryItemVM selectedItem)
        {
            if (selectedItem == null) return;

            // Создаем и открываем окно с деталями

            var unitOfWork = AGR_ServiceContainer.GetService<IUnitOfWork>();

            var detailsVM = new AGR_ComponentDetailsVM(selectedItem, unitOfWork); // Предполагаем, что ViewModel будет создана
            var detailsView = new AGR_ComponentDetailsView { DataContext = detailsVM };

            var window = new Window
            {
                Title = $"Детали: {selectedItem.Name} ({selectedItem.PartNumber})",
                Content = detailsView,
                Width = 800,
                Height = 600,
                ResizeMode = ResizeMode.CanResizeWithGrip
            };

            window.ShowDialog(); // Открываем модально
        }
        #endregion


        // Коллекция для хранения данных
        private ObservableCollection<AGR_ComponentRegistryItemVM> _registryItems = new();
        public ObservableCollection<AGR_ComponentRegistryItemVM> RegistryItems => _registryItems;

        // View для фильтрации
        public ICollectionView RegistryItemsView { get; }

        // Свойство для текста поиска
        private string? _searchText;
        public string? SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                {
                    RegistryItemsView.Refresh(); // Обновляем фильтр при изменении текста
                }
            }
        }

        #region SelectedComponentType
        private string? _selectedComponentType = "Все типы";
        public string? SelectedComponentType
        {
            get => _selectedComponentType;
            set
            {
                if (Set(ref _selectedComponentType, value))
                {
                    RegistryItemsView.Refresh(); // Обновляем фильтр при изменении типа
                }
            }
        }
        #endregion

        #region SelectedAvaType
        private string? _selectedAvaType = "Все типы";
        public string? SelectedAvaType
        {
            get => _selectedAvaType;
            set
            {
                if (Set(ref _selectedAvaType, value))
                {
                    RegistryItemsView.Refresh(); // Обновляем фильтр при изменении AvaType
                }
            }
        }
        #endregion

        #region AvailableComponentTypes
        private ObservableCollection<string> _availableComponentTypes;
        public ObservableCollection<string> AvailableComponentTypes
        {
            get => _availableComponentTypes;
            set => Set(ref _availableComponentTypes, value);
        }
        #endregion

        #region AvailableAvaTypes
        private ObservableCollection<string> _availableAvaTypes;
        public ObservableCollection<string> AvailableAvaTypes
        {
            get => _availableAvaTypes;
            set => Set(ref _availableAvaTypes, value);
        }
        #endregion


        // Метод фильтрации для CollectionViewSource
        private bool FilterRegistryItems(object item)
        {
            if (item is not AGR_ComponentRegistryItemVM registryItem)
                return false;
            // 1. Фильтр по тексту поиска
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string[] splitSearch = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToArray();

                if (registryItem.Name is null) return false;
                if (!splitSearch.All(s => registryItem.Name.Contains(s.ToString(), StringComparison.OrdinalIgnoreCase)))
                {
                    if (registryItem.PartNumber == null || !registryItem.PartNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // Ни по имени, ни по PartNumber не совпало
                    }
                }
                // Если прошло проверку по SearchText, продолжаем
            }
            // Если SearchText пустой, пропускаем проверку выше

            // 2. Фильтр по ComponentTypeDisplay
            if (!string.IsNullOrEmpty(SelectedComponentType))
            {
                if (SelectedComponentType == "Все типы") { }
                else
                {
                    var itemCompType = _componentTypeConverter.Convert(registryItem.ComponentTypeDisplay, typeof(string), null, CultureInfo.CurrentCulture) as string;
                    if (itemCompType != SelectedComponentType) return false;
                }
            }

            // 3. Фильтр по AvaTypeDisplay
            if (!string.IsNullOrEmpty(SelectedAvaType))
            {
                if (SelectedAvaType == "Все типы") { }
                else
                {
                    var itemAvaType = _avaTypeConverter.Convert(registryItem.AvaTypeDisplay, typeof(string), null, CultureInfo.CurrentCulture) as string;
                    if (itemAvaType != SelectedAvaType) return false;
                }
            }

            // Если все проверки пройдены, показываем элемент
            return true;
        }
    }
}