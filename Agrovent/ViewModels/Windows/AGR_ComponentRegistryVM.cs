// File: ViewModels/Windows/AGR_ComponentRegistryVM.cs
using Agrovent.DAL;
using Agrovent.DAL.Entities.Components;
using Agrovent.DAL.Services.Repositories;
using Agrovent.Infrastructure;
using Agrovent.Infrastructure.Commands;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.Windows.Details;
using Agrovent.Views.Windows.Details;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shell.Interop;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Xarial.XCad.Base;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents; // Для CollectionViewSource

namespace Agrovent.ViewModels.Windows
{
    public class AGR_ComponentRegistryVM : BaseViewModel
    {
        private readonly IAGR_ComponentRepository _componentRepository;
        private readonly ILogger<AGR_ComponentRegistryVM> _logger;

        public AGR_ComponentRegistryVM(
            IAGR_ComponentRepository componentRepository,
            ILogger<AGR_ComponentRegistryVM> logger)
        {
            _componentRepository = componentRepository ?? throw new ArgumentNullException(nameof(componentRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Инициализация CollectionViewSource
            RegistryItemsView = CollectionViewSource.GetDefaultView(RegistryItems);
            RegistryItemsView.Filter = FilterRegistryItems; // Устанавливаем метод фильтрации
        }

        #region COMMANDS
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
                _logger.LogInformation("Загрузка данных для реестра компонентов...");

                // Загружаем все последние версии компонентов
                // Это может быть дорогая операция, в реальном приложении можно добавить пагинацию
                var versions = await _componentRepository.GetAllLatestComponentVersionsAsync(); // Предполагаем, что такой метод есть в репозитории

                var source = new ObservableCollection<ComponentVersion>(versions);

                // Очищаем текущую коллекцию
                RegistryItems.Clear();

                // Преобразуем сущности в VM и добавляем в коллекцию
                foreach (var version in versions)
                {
                    var itemVm = new AGR_ComponentRegistryItemVM(version, AGR_Options.StorageRootFolderPath);
                    RegistryItems.Add(itemVm);
                }

                _logger.LogInformation($"Загружено {RegistryItems.Count} записей в реестр компонентов.");

                // Обновляем фильтр, если был текст поиска
                RegistryItemsView.Refresh();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных реестра компонентов.");
                // Можно показать сообщение пользователю
            }
        }

        #endregion

        #region OpenComponentCommand
        private ICommand _OpenComponentCommand;
        public ICommand OpenComponentCommand => _OpenComponentCommand
            ??= new RelayCommand<AGR_ComponentRegistryItemVM>(OnOpenComponentCommandExecuted, CanOpenComponentCommandExecute);
        private bool CanOpenComponentCommandExecute(AGR_ComponentRegistryItemVM p) => true;//p != null && !string.IsNullOrEmpty(p.StoragePath) && File.Exists(p.StoragePath);
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
                    //compDoc = swApp.Documents.PreCreateFromPath(filePath) as IXDocument3D;

                    compDoc = swApp.Documents.PreCreate<ISwDocument3D>();
                    compDoc.Path = filePath;

                    if (compDoc == null)
                    {
                        _logger.LogError($"Команда 'Добавить в сборку': Не удалось открыть документ компонента: {filePath}");
                        return;
                    }
                    //compDoc.Commit(CancellationToken.None);
                    _logger.LogDebug($"Команда 'Добавить в сборку': Документ компонента открыт: {filePath}");

                    swApp.Documents.Active = activeDoc;
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
                swApp.Sw.RunCommand(1993, "");

                // Запускаем внутреннюю команду ZoomToFit (Zoom to Fit)
                swApp.Sw.RunCommand(332, "");

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
            var unitOfWork = AGR_ServiceContainer.GetService<IUnitOfWork>();

            // Создаем и открываем окно с деталями
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

        public string Title = "title";

        // Метод фильтрации для CollectionViewSource
        private bool FilterRegistryItems(object item)
        {
            if (item is not AGR_ComponentRegistryItemVM registryItem)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true; // Если текст поиска пуст, показываем все

            string[] splitSearch = SearchText.Split(' ').ToArray();

            var searchTextLower = SearchText.ToLowerInvariant();

            if (registryItem.Name is null) return true;
            if (splitSearch.All(s => registryItem.Name.Contains(s.ToString(), StringComparison.OrdinalIgnoreCase))) return true;
            if (registryItem.PartNumber != null && registryItem.PartNumber.Contains(SearchText)) return true;

            return false;
        }
    }
}