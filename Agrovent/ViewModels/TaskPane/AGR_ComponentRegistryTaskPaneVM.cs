// File: ViewModels/TaskPane/AGR_ComponentRegistryTaskPaneVM.cs
using Agrovent.DAL;
using Agrovent.DAL.Services.Repositories;
using Agrovent.Infrastructure;
using AgroventInfrastructure.AGR_Converters;
using Agrovent.Infrastructure.Commands; // Для RelayCommand
using Agrovent.Infrastructure.Converters;
using Agrovent.Infrastructure.Enums;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.Windows.Details;
using Agrovent.Views.Windows.Details;
using AgroventInfrastructure.Entities.Components;
using AgroventInfrastructure.Interfaces.Entities.Components;
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
using Xarial.XCad.Documents.Extensions;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;
using AgroventInfrastructure.Enums;
using Agrovent.Infrastructure.Helpers;
using Agrovent.ViewModels.Specification;
using Agrovent.ViewModels.Windows;
using Agrovent.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Agrovent.ViewModels.TaskPane
{
    public class AGR_ComponentRegistryTaskPaneVM : BaseViewModel
    {
        private readonly IAGR_ComponentRepository _componentRepository;
        private readonly ILogger<AGR_ComponentRegistryTaskPaneVM> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly AGR_AvaTypeConverter _avaTypeConverter = new AGR_AvaTypeConverter();
        private static readonly AGR_ComponentTypeConverter _componentTypeConverter = new AGR_ComponentTypeConverter();

        #region CTOR
        public AGR_ComponentRegistryTaskPaneVM(
                IAGR_ComponentRepository componentRepository,
                IServiceScopeFactory scopeFactory,
                ILogger<AGR_ComponentRegistryTaskPaneVM> logger)
        {
            _componentRepository = componentRepository ?? throw new ArgumentNullException(nameof(componentRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

            // Инициализация CollectionViewSource
            RegistryItemsView = CollectionViewSource.GetDefaultView(RegistryItems);
            RegistryItemsView.Filter = FilterRegistryItems;

            // Инициализация коллекций для ComboBox
            AvailableAvaTypes = new ObservableCollection<string>
            {
                AGR_AvaTypeNames.Production,
                AGR_AvaTypeNames.Component,
                AGR_AvaTypeNames.Purchased,
                AGR_AvaTypeNames.DontBuy,
                AGR_AvaTypeNames.VirtualComponent,
                AGR_AvaTypeNames.AllTypes
            };

            AvailableComponentTypes = new ObservableCollection<string>
            {
                AGR_ComponentTypeNames.Assembly,
                AGR_ComponentTypeNames.SheetMetallPart,
                AGR_ComponentTypeNames.Part,
                AGR_ComponentTypeNames.Purchased,
                AGR_ComponentTypeNames.AllTypes
            };

            // Загружаем данные при создании VM (или вызывайте LoadDataCommand извне)
            // Task.Run(async () => await LoadDataAsync()); // Не рекомендуется запускать асинхронный код в конструкторе
        }

        public AGR_ComponentRegistryTaskPaneVM()
        {

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
                    var itemVm = new AGR_ComponentRegistryItemVM(version);
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
                    var newDoc = swApp.Documents.Open(filePath, Xarial.XCad.Documents.Enums.DocumentState_e.ReadOnly);// PreCreateFromPath(filePath);
                    //newDoc.Commit(CancellationToken.None);
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
            var drawPath = Path.ChangeExtension(filePath, ".slddrw");
            var fileTitle = Path.GetFileNameWithoutExtension(filePath);

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
                //Папка текущей сборки
                var assemblyFolderPath = Path.GetDirectoryName(swAssembly.Path);

                if (string.IsNullOrEmpty(assemblyFolderPath))
                {
                    assemblyFolderPath = AGR_Options.LocalWorkFolder;
                }

                if (assemblyFolderPath.Contains(AGR_Options.OldStorageRootFolderPath) || assemblyFolderPath.Contains(AGR_Options.OldStorageRootFolderPath))
                {
                    AGR_Helper.ShowMessage($"Попытка добавить компонент в папку хранилища\n{assemblyFolderPath}.\nОперация добавления отменена."
                        , Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error,
                        Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                    return;
                }

                //Путь к файлу компонента для копирования из хранилища
                var destFilePath = Path.Combine(assemblyFolderPath, Path.GetFileName(filePath));
                var destDrawPath = Path.ChangeExtension(destFilePath, ".slddrw");
                // Проверяем, открыт ли документ компонента
                IXDocument3D? compDoc = swApp.Documents.FirstOrDefault(x => x.Title == fileTitle) as IXDocument3D;
                if (compDoc == null)
                {
                    //Проверяем есть ли файл в рабочей папке, если есть убираем для чтения
                    if (File.Exists(destFilePath))
                    {
                        File.SetAttributes(destFilePath, FileAttributes.Normal);
                    }

                    //Проверяем есть ли файл чертежа в рабочей папке, если есть убираем для чтения
                    if (File.Exists(destDrawPath))
                    {
                        File.SetAttributes(destDrawPath, FileAttributes.Normal);
                    }

                    // Нельзя открывать документы из центрального хранилища, копируем файл в папку с активной сборкой
                    File.Copy(filePath, destFilePath, true);
                    //Если есть чертеж копируемого  компонента, то копируем его в папку тоже
                    if (File.Exists(drawPath))
                    {
                        File.Copy(drawPath, destDrawPath, true);
                    }


                    // Документ не открыт, открываем его
                    compDoc = swApp.Documents.PreCreateFromPath(destFilePath) as IXDocument3D;
                    if (compDoc == null)
                    {
                        _logger.LogError($"Команда 'Добавить в сборку': Не удалось открыть документ компонента: {destFilePath}");
                        return;
                    }
                    //compDoc.Commit(CancellationToken.None);
                    _logger.LogDebug($"Команда 'Добавить в сборку': Документ компонента открыт: {destFilePath}");
                }

                // Создаем шаблон компонента
                var xComp = swAssembly.Configurations.Active.Components.PreCreate<IXComponent>();
                if (xComp == null)
                {
                    _logger.LogError($"Команда 'Добавить в сборку': Не удалось создать шаблон компонента для {destFilePath}");
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

            // Отдельный скоуп на время жизни окна — свой DataContext, изолированный
            // от главного пайплайна сохранения и от других одновременно открытых окон деталей.
            var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                if (selectedItem.ComponentType == AGR_ComponentType_e.Assembly)
                {
                    var vm = new AGR_AssemblyEditVM(selectedItem, unitOfWork,
                        scope.ServiceProvider.GetService<ILogger<AGR_AssemblyEditVM>>());

                    var view = new AGR_SpecificationWindow { DataContext = vm, Topmost = true, Title = $"Подробная информация" };
                    view.Closed += (_, _) => scope.Dispose(); // скоуп закрывается вместе с окном

                    // Окно показывается сразу с оверлеем прогресса (IsLoading/LoadingStatus),
                    // а фактическая загрузка из БД запускается его Window_Loaded.
                    view.Show();
                }
                else
                {
                    var vm = new AGR_ComponentEditVM(selectedItem, unitOfWork,
                        scope.ServiceProvider.GetService<ILogger<AGR_ComponentEditVM>>());

                    var view = new SaveConfirmationView { DataContext = vm, Topmost = true, Title = $"Подробная информация" };
                    view.Closed += (_, _) => scope.Dispose();

                    view.Show();
                }
            }
            catch (Exception ex)
            {
                scope.Dispose(); // если что-то упало до открытия окна — скоуп некому будет закрыть
                _logger.LogError(ex, $"Ошибка при открытии окна деталей для {selectedItem.Name} ({selectedItem.RawPartNumber})");
            }
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
        private string? _selectedComponentType = AGR_ComponentTypeNames.AllTypes;
        public string? SelectedComponentType
        {
            get => _selectedComponentType;
            set
            {
                if (Set(ref _selectedComponentType, value))
                {
                    if (value == AGR_ComponentTypeNames.Purchased)
                    {
                        SelectedAvaType = AGR_AvaTypeNames.Purchased;
                    }
                    if (value == AGR_ComponentTypeNames.AllTypes)
                    {
                        SelectedAvaType = AGR_AvaTypeNames.AllTypes;
                    }
                    RegistryItemsView.Refresh(); // Обновляем фильтр при изменении типа
                }
            }
        }
        #endregion

        #region SelectedAvaType
        private string? _selectedAvaType = AGR_AvaTypeNames.AllTypes;
        public string? SelectedAvaType
        {
            get => _selectedAvaType;
            set
            {
                if (Set(ref _selectedAvaType, value))
                {
                    if (value == AGR_AvaTypeNames.Purchased)
                    {
                        SelectedComponentType = AGR_ComponentTypeNames.Purchased;
                    }
                    if (value == AGR_AvaTypeNames.AllTypes)
                    {
                        SelectedComponentType = AGR_ComponentTypeNames.AllTypes;
                    }


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
                string[] splitSearch = SearchText
                        .Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .ToArray();

                if (registryItem.Name is null) return false;

                if (!splitSearch.All
                        (s => registryItem.Name.Contains(s.ToString(), StringComparison.OrdinalIgnoreCase)
                            || registryItem.SavedByUserInitials.Contains(s.ToString(), StringComparison.OrdinalIgnoreCase)
                            || registryItem.PartNumber.Contains(s.ToString(), StringComparison.OrdinalIgnoreCase)
                        ))
                {
                    return false;
                }
            }

            // 2. Фильтр по ComponentTypeDisplay
            if (!string.IsNullOrEmpty(SelectedComponentType))
            {
                if (SelectedComponentType != AGR_ComponentTypeNames.AllTypes)
                {
                    var itemCompType = _componentTypeConverter.Convert(registryItem.ComponentTypeDisplay, typeof(string), null, CultureInfo.CurrentCulture) as string;
                    if (itemCompType != SelectedComponentType) return false;
                }
            }

            // 3. Фильтр по AvaTypeDisplay
            if (!string.IsNullOrEmpty(SelectedAvaType))
            {
                if (SelectedAvaType != AGR_AvaTypeNames.AllTypes)
                {
                    var itemAvaType = _avaTypeConverter.Convert(registryItem.AvaTypeDisplay, typeof(string), null, CultureInfo.CurrentCulture) as string;
                    if (itemAvaType != SelectedAvaType) return false;
                }
            }
            // Если все проверки пройдены, показываем элемент
            return true;
        }
        private bool FilterBySearch(object item)
        {
            if (item is not AGR_ComponentRegistryItemVM registryItem)
                return false;

            // 1. Фильтр по тексту поиска
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string[] splitSearch = SearchText
                        .Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .ToArray();

                if (registryItem.Name is null) return false;

                if (!splitSearch.All
                        (s => registryItem.Name.Contains(s.ToString(), StringComparison.OrdinalIgnoreCase)
                            || registryItem.SavedByUserInitials.Contains(s.ToString(), StringComparison.OrdinalIgnoreCase)
                            || registryItem.PartNumber.Contains(s.ToString(), StringComparison.OrdinalIgnoreCase)
                        ))
                {
                    return false;
                }
            }
            return true;
        }
        private bool FilterByAvaType(object item)
        {
            if (item is not AGR_ComponentRegistryItemVM registryItem)
                return false;
            // 3. Фильтр по AvaTypeDisplay
            if (!string.IsNullOrEmpty(SelectedAvaType))
            {
                if (SelectedAvaType != AGR_AvaTypeNames.AllTypes) return false;
                else
                {
                    var itemAvaType = _avaTypeConverter.Convert(registryItem.AvaTypeDisplay, typeof(string), null, CultureInfo.CurrentCulture) as string;
                    return itemAvaType == SelectedAvaType;
                }
            }
            return true;
        }
        private bool FilterByComponentType(object item)
        {
            if (item is not AGR_ComponentRegistryItemVM registryItem)
                return false;
            // 2. Фильтр по ComponentTypeDisplay
            if (!string.IsNullOrEmpty(SelectedComponentType))
            {
                if (SelectedComponentType != AGR_ComponentTypeNames.AllTypes)
                {
                    var itemCompType = _componentTypeConverter.Convert(registryItem.ComponentTypeDisplay, typeof(string), null, CultureInfo.CurrentCulture) as string;
                    if (itemCompType != SelectedComponentType) return false;
                }
            }
            return true;
        }
    }
}