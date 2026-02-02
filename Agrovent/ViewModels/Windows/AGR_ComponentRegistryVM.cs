// File: ViewModels/Windows/AGR_ComponentRegistryVM.cs
using Agrovent.DAL.Entities.Components;
using Agrovent.DAL.Repositories;
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Configuration;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input; // Для CollectionViewSource

namespace Agrovent.ViewModels.Windows
{
    public class AGR_ComponentRegistryVM : BaseViewModel
    {
        private readonly IAGR_ComponentRepository _componentRepository;
        private readonly AGR_FileStorageConfig _storageConfig; // Для формирования StoragePath
        private readonly ILogger<AGR_ComponentRegistryVM> _logger;

        public AGR_ComponentRegistryVM(
            IAGR_ComponentRepository componentRepository,
            AGR_FileStorageConfig storageConfig,
            ILogger<AGR_ComponentRegistryVM> logger)
        {
            _componentRepository = componentRepository ?? throw new ArgumentNullException(nameof(componentRepository));
            _storageConfig = storageConfig ?? throw new ArgumentNullException(nameof(storageConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            //LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());

            // Инициализация CollectionViewSource
            RegistryItemsView = CollectionViewSource.GetDefaultView(RegistryItems);
            RegistryItemsView.Filter = FilterRegistryItems; // Устанавливаем метод фильтрации

            // Загружаем данные при создании VM (или вызывайте LoadDataCommand извне)
            // Task.Run(async () => await LoadDataAsync()); // Не рекомендуется запускать асинхронный код в конструкторе
        }

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
                    var itemVm = new AGR_ComponentRegistryItemVM(version, _storageConfig.StorageRootFolder);
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



        // Метод фильтрации для CollectionViewSource
        private bool FilterRegistryItems(object item)
        {
            if (item is not AGR_ComponentRegistryItemVM registryItem)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true; // Если текст поиска пуст, показываем все

            var searchTextLower = SearchText.ToLowerInvariant();

            // Проверяем совпадение по нескольким полям
            return registryItem.Name.ToLowerInvariant().Contains(searchTextLower) ||
                   registryItem.PartNumber.ToLowerInvariant().Contains(searchTextLower) ||
                   registryItem.ComponentTypeDisplay.ToLowerInvariant().Contains(searchTextLower) ||
                   registryItem.AvaTypeDisplay.ToLowerInvariant().Contains(searchTextLower);
            // Добавьте другие поля по необходимости, например Version.ToString(), StoragePath и т.д.
        }
    }
}