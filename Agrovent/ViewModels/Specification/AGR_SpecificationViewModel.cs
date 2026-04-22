using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using AGR_PropManager;
using AGR_PropManager.ViewModels.Components;
using Agrovent.DAL;
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Helpers;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Specification;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.Windows;
using Agrovent.Views.Windows;
using EnvDTE;
using Microsoft.Extensions.Logging;
using Xarial.XCad.Base.Enums;

namespace Agrovent.ViewModels.Specification
{
    public class AGR_SpecificationViewModel : BaseViewModel
    {
        private readonly AGR_AssemblyComponentVM _baseComponent;
        private readonly ILogger _logger; // Добавляем логгер
        private readonly IUnitOfWork _unitOfWork;

        #region CTOR
        public AGR_SpecificationViewModel(AGR_AssemblyComponentVM baseComponent, IUnitOfWork unitOfWork)
        {

            //_logger = AGR_ServiceContainer.GetService<ILogger>();
            _baseComponent = baseComponent;
            _unitOfWork = unitOfWork;
            WindowTitle = $"Спецификация: {baseComponent.Name} ({baseComponent.PartNumber})";
            Initialize();
        }

        private async void Initialize()
        {
            // Инициализируем пустую коллекцию
            Components = new ObservableCollection<AGR_SpecificationItemVM>();
            GroupedComponentsView = CollectionViewSource.GetDefaultView(Components);
            UpdateGroupedView();

            // Загружаем компоненты асинхронно
            await LoadComponentsAsync(); 
            // Загружаем материалы асинхронно
            await LoadMaterialsForComponentsAsync();
            // Загружаем AvaArticle для покупных компонентов асинхронно
            await LoadAvaArticlesForPurchasedComponentsAsync();

        }
        #endregion

        #region Публичные свойства
        //public ICollectionView GroupedComponentsView => _groupedComponentsView?.View;
        public ICollectionView GroupedComponentsView { get; private set; }

        private ObservableCollection<AGR_SpecificationItemVM> _components;
        public ObservableCollection<AGR_SpecificationItemVM> Components
        {
            get => _components;
            private set
            {
                if (Set(ref _components, value))
                {
                    UpdateGroupedView();
                }
            }
        }

        #region Коллекция для хранения выбранных компонентов
        private ObservableCollection<AGR_SpecificationItemVM> _SelectedComponents = new ObservableCollection<AGR_SpecificationItemVM>();
        public ObservableCollection<AGR_SpecificationItemVM> SelectedComponents
        {
            get => _SelectedComponents;
            set => Set(ref _SelectedComponents, value);
        }
        #endregion

        private string _windowTitle;
        public string WindowTitle
        {
            get => _windowTitle;
            private set => Set(ref _windowTitle, value);
        }

        private bool _hasComponents;
        public bool HasComponents
        {
            get => _hasComponents;
            private set => Set(ref _hasComponents, value);
        }
        #endregion

        #region Команды
        private RelayCommand _closeCommand;
        public RelayCommand CloseCommand => _closeCommand ??= new RelayCommand(
            _ => CloseWindow?.Invoke(),
            _ => true);
        #endregion

        #region События
        public event Action CloseWindow;
        #endregion

        #region Статистические свойства
        private int _totalComponents;
        public int TotalComponents
        {
            get => _totalComponents;
            private set => Set(ref _totalComponents, value);
        }

        private int _totalAssemblies;
        public int TotalAssemblies
        {
            get => _totalAssemblies;
            private set => Set(ref _totalAssemblies, value);
        }

        private int _totalSheetMetal;
        public int TotalSheetMetal
        {
            get => _totalSheetMetal;
            private set => Set(ref _totalSheetMetal, value);
        }

        private int _totalParts;
        public int TotalParts
        {
            get => _totalParts;
            private set => Set(ref _totalParts, value);
        }

        private int _totalPurchased;
        public int TotalPurchased
        {
            get => _totalPurchased;
            private set => Set(ref _totalPurchased, value);
        }
        #endregion


        #region METHODS
        private void LoadComponents()
        {
            if (_baseComponent.GetChildComponents() == null)
                return;

            try
            {
                var allComponents = _baseComponent.GetFlatComponents().ToList();

                // Сортируем компоненты по типу для правильного порядка групп
                var sortedComponents = allComponents
                    .OrderBy(c => c.ComponentType switch
                    {
                        AGR_ComponentType_e.Assembly => 0,
                        AGR_ComponentType_e.SheetMetallPart => 1,
                        AGR_ComponentType_e.Part => 2,
                        AGR_ComponentType_e.Purchased => 3,
                        _ => 4
                    })
                    .ToList();
                foreach (var component in sortedComponents)
                {
                    Components.Add(component as AGR_SpecificationItemVM);
                }

                //// Обновляем коллекцию в UI потоке
                //System.Windows.Application.Current.Dispatcher.Invoke(() =>
                //{
                //    Components.Clear();
                //   

                //    UpdateStatistics();
                //    HasComponents = Components.Count > 0;
                //});
            }
            catch (Exception ex)
            {
                // Логируем ошибку
                Console.WriteLine($"Ошибка загрузки компонентов: {ex.Message}");
            }
        }

        private async Task LoadComponentsAsync()
        {
            if (_baseComponent.GetChildComponents() == null)
                return;

            try
            {
                var allComponents = _baseComponent.GetFlatComponents().ToList();

                var sortedComponents = allComponents
                    .OrderBy(c => c.ComponentType switch
                    {
                        AGR_ComponentType_e.Assembly => 0,
                        AGR_ComponentType_e.SheetMetallPart => 1,
                        AGR_ComponentType_e.Part => 2,
                        AGR_ComponentType_e.Purchased => 3,
                        _ => 4
                    })
                    .ToList();
                foreach (var component in sortedComponents)
                {
                    Components.Add(component as AGR_SpecificationItemVM);
                }

                //UpdateStatistics();
                HasComponents = Components.Count > 0;
            }
            catch (Exception ex)
            {
                // Логируем ошибку
                Console.WriteLine($"Ошибка загрузки компонентов: {ex.Message}");
            }
        }

        // Асинхронный метод для загрузки материалов
        private async Task LoadMaterialsForComponentsAsync()
        {
            try
            {
                // 1. Собрать уникальные имена материалов из нужных компонентов
                var componentsWithMaterial = Components
                    .Where(c => (c.ComponentType == AGR_ComponentType_e.Part || c.ComponentType == AGR_ComponentType_e.SheetMetallPart) && !string.IsNullOrEmpty(c.MaterialName))
                    .ToList();

                if (!componentsWithMaterial.Any()) return; // Нечего загружать

                var uniqueMaterialNames = componentsWithMaterial
                    .Select(c => c.MaterialName)
                    .Distinct()
                    .ToList();

                //_logger.LogInformation($"Поиск AvaArticle для {uniqueMaterialNames.Count} уникальных наименований материалов.");

                // 2. Использовать UnitOfWork и ComponentRepository для поиска
                // Начинаем транзакцию только для чтения, если это поддерживается и имеет смысл, иначе просто вызываем метод репозитория.
                // var transaction = await _unitOfWork.BeginTransactionAsync(); // Не обязательно для SELECT
                var materialNameToAvaArticleMap = await _unitOfWork.ComponentRepository.GetAvaArticlesByNameAsync(uniqueMaterialNames);
                // await transaction.RollbackAsync(); // Откатываем, так как это был SELECT

                //_logger.LogInformation($"Найдено {materialNameToAvaArticleMap.Count} AvaArticle по наименованиям.");

                // 3. Обновить соответствующие VM
                foreach (var specItem in componentsWithMaterial)
                {
                    if (materialNameToAvaArticleMap.TryGetValue(specItem.MaterialName, out var avaArticleModel))
                    {
                        // Предполагается, что AGR_SpecificationItemVM.MaterialAvaModel является свойством, которое при установке
                        // корректно обновляет Component (например, через привязку или внутреннюю логику).
                        // Убедитесь, что это свойство вызывает OnPropertyChanged.
                        specItem.MaterialAvaModel = avaArticleModel;
                        //_logger.LogDebug($"Установлен AvaArticle для компонента {specItem.PartNumber} (Material: {specItem.MaterialName})");
                    }
                    else
                    {
                        specItem.MaterialAvaModel = null;
                        specItem.MaterialName = "";
                        //_logger.LogWarning($"AvaArticle не найден для материала '{specItem.MaterialName}' компонента {specItem.PartNumber}.");
                    }
                }

               // _logger.LogInformation("Загрузка AvaArticle для материалов завершена.");
            }
            catch (Exception ex)
            {
               // _logger.LogError(ex, "Ошибка при загрузке AvaArticle для материалов компонентов спецификации.");
                // Можно показать сообщение пользователю, если необходимо
            }
        }
        private async Task LoadAvaArticlesForPurchasedComponentsAsync()
        {
            try
            {
                // 1. Собрать компоненты типа Purchased, у которых AvaArticle == null и Article не пуст
                var componentsToSearch = Components
                    .Where(c => c.ComponentType == AGR_ComponentType_e.Purchased && c.AvaArticle == null &&  !string.IsNullOrEmpty(c.Component.Article))
                    .ToList();

                if (!componentsToSearch.Any()) return; // Нечего загружать

                //_logger.LogInformation($"Поиск AvaArticle для {componentsToSearch.Count} покупных компонентов по Article.");

                // 2. Для каждого компонента, выполнить поиск по Article и обновить VM
                foreach (var specItem in componentsToSearch)
                {
                    int articleNumber = int.Parse(specItem.Component.Article); // Уже проверено на HasValue
                    //_logger.LogDebug($"Поиск AvaArticle для компонента {specItem.PartNumber} по Article {articleNumber}.");

                    var avaArticleModel = await _unitOfWork.ComponentRepository.GetAvaArticleByArticleNumberAsync(articleNumber);

                    if (avaArticleModel != null)
                    {
                        specItem.AvaArticle = avaArticleModel; // Устанавливаем найденный AvaArticleModel в VM
                        //_logger.LogDebug($"Установлен AvaArticle ({avaArticleModel.Article}: {avaArticleModel.Name}) для компонента {specItem.PartNumber} по Article {articleNumber}.");
                    }
                    else
                    {
                        //_logger.LogWarning($"AvaArticle не найден в БД по Article '{articleNumber}' для компонента {specItem.PartNumber}.");
                    }
                }

                //_logger.LogInformation("Загрузка AvaArticle для покупных компонентов завершена.");
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Ошибка при загрузке AvaArticle для покупных компонентов спецификации.");
                // Можно показать сообщение пользователю, если необходимо
            }
        }
        private void UpdateGroupedView()
        {
            if (GroupedComponentsView != null)
            {

                // Настраиваем группировку по типу компонента
                GroupedComponentsView.GroupDescriptions.Clear();
                GroupedComponentsView.GroupDescriptions.Add(
                    new PropertyGroupDescription("ComponentType"));

                // Сортируем группы по заданному порядку
                GroupedComponentsView.SortDescriptions.Clear();
                GroupedComponentsView.SortDescriptions.Add(
                    new SortDescription("ComponentType", ListSortDirection.Ascending));
                GroupedComponentsView.SortDescriptions.Add(
                    new SortDescription("Name", ListSortDirection.Ascending));
                OnPropertyChanged(nameof(GroupedComponentsView));

            }
        }

        private void UpdateStatistics()
        {
            if (Components == null)
                return;

            TotalComponents = Components.Count;
            TotalAssemblies = Components.Count(c => c.ComponentType == AGR_ComponentType_e.Assembly);
            TotalSheetMetal = Components.Count(c => c.ComponentType == AGR_ComponentType_e.SheetMetallPart);
            TotalParts = Components.Count(c => c.ComponentType == AGR_ComponentType_e.Part);
            TotalPurchased = Components.Count(c => c.ComponentType == AGR_ComponentType_e.Purchased);
        }
        public void Refresh()
        {
            // Загрузка компонентов и материалов теперь асинхронная.
            // Для простоты, можно вызвать Task.Run, но лучше использовать более сложные шаблоны.
            // Обратите внимание, что обновления UI должны происходить в основном потоке.
            Task.Run(async () =>
            {
                await LoadComponentsAsync();
                await LoadMaterialsForComponentsAsync();
                // Возможно, потребуется обновление UI в основном потоке, например, через Dispatcher.Invoke
            }).ContinueWith(t => {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Ошибка во время обновления спецификации.");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext()); // Или использовать подходящий способ для UI обновления
        }

        private void DeselectAllComponents()
        {
            foreach (var item in Components)
            {
                item.IsSelected = false;
            }
        }
        #endregion

        #region COMMANDS

        #region SelectComponentCommand
        private ICommand _SelectComponentCommand;
        public ICommand SelectComponentCommand => _SelectComponentCommand
            ??= new RelayCommand(OnSelectComponentCommandExecuted, CanSelectComponentCommandExecute);
        private bool CanSelectComponentCommandExecute(object p) => true;
        private void OnSelectComponentCommandExecuted(object p)
        {
            if (p is null)
            {

            }

            foreach (var item in SelectedComponents)
            {
                item.IsSelected = !item.IsSelected;
            }
        }
        #endregion

        #region SetBaseMaterialCommand
        private ICommand _SetBaseMaterialCommand;
        public ICommand SetBaseMaterialCommand => _SetBaseMaterialCommand
            ??= new RelayCommand(OnSetBaseMaterialCommandExecuted, CanSetBaseMaterialCommandExecute);
        private bool CanSetBaseMaterialCommandExecute(object p)
        {
            var _selectedComponents = Components.Where(x => x.IsSelected == true);
            if (_selectedComponents.Count() == 0) _selectedComponents = SelectedComponents;
            if (_selectedComponents.Any(p => p.Component.ComponentType != AGR_ComponentType_e.Part && p.Component.ComponentType != AGR_ComponentType_e.SheetMetallPart))
            {
                return false;
            }
            return true;
        }
        private void OnSetBaseMaterialCommandExecuted(object p)
        {
            var _selectedComponents = Components.Where(x => x.IsSelected == true) ?? SelectedComponents;

            try
            {
                // Получаем нужные сервисы для VM
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                // Создаем ViewModel
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SelectedAvaType = "Товар";

                // Создаем View и устанавливаем DataContext
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };

                selectView.ShowActivated = true;
                // Открываем окно модально
                selectView.ShowDialog();

                // Если окно закрыто с результатом OK и элемент выбран
                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    foreach (var item in _selectedComponents)
                    {
                        item.MaterialAvaModel = selectVm.SelectedArticle;
                        OnPropertyChanged(nameof(item.PartnumberOrArticle));
                        //var part = item.Component as AGR_PartComponentVM;
                        // Присваиваем выбранный AvaArticleModel в BaseMaterial.AvaModel
                        //part.BaseMaterial = new AGR_Material(selectVm.SelectedArticle);
                        //part.mProperties.FirstOrDefault(p => p.Name == AGR_PropertyNames.Material).Value = part.BaseMaterial.Name;
                        _logger?.LogInformation("Выбран AvaArticle {Article} для компонента {PartNumber}", selectVm.SelectedArticle.Article, item.PartNumber);

                        //OnPropertyChanged(nameof(part.BaseMaterial));
                        //OnPropertyChanged(nameof(item.MaterialName));
                    }

                    // Обновляем свойства, если это влияет на них (например, BaseMaterialCount)
                    //Task.Run(async () => await UpdatePropertiesAsync()).ConfigureAwait(false); // Вызов асинхронного метода
                }
                else
                {
                    _logger?.LogDebug("Окно выбора AvaArticle закрыто без выбора.");
                }
                DeselectAllComponents();
            }
            catch (Exception ex)
            {
                //_logger?.LogError(ex, "Ошибка при открытии окна выбора AvaArticle для компонента {PartNumber}", PartNumber);
            }
        }
        #endregion


        #region SetPaintCommand
        private ICommand _SetPaintCommand;
        public ICommand SetPaintCommand => _SetPaintCommand
            ??= new RelayCommand(OnSetPaintCommandExecuted, CanSetPaintCommandExecute);
        private bool CanSetPaintCommandExecute(object p)
        {
            var _selectedComponents = Components.Where(x => x.IsSelected == true);
            if (_selectedComponents.Count() == 0) _selectedComponents = SelectedComponents;
            //if (_selectedComponents.Any(p => p.Component.ComponentType != AGR_ComponentType_e.Part && p.Component.ComponentType != AGR_ComponentType_e.SheetMetallPart))
            //{
            //    return false;
            //}
            return true;
        }
        private void OnSetPaintCommandExecuted(object p)
        {
            var _selectedComponents = Components.Where(x => x.IsSelected == true) ?? SelectedComponents;
            if (p.ToString() == "NoPaint")
            {
                foreach (var item in _selectedComponents)
                {
                    item.PaintAvaModel = null;
                }
                DeselectAllComponents();
                return;
            }

            try
            {
                // Получаем нужные сервисы для VM
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                // Создаем ViewModel
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SearchText = "Краска порошковая";
                selectVm.SelectedAvaType = "Товар";

                // Создаем View и устанавливаем DataContext
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };

                selectView.ShowActivated = true;
                // Открываем окно модально
                selectView.ShowDialog();

                // Если окно закрыто с результатом OK и элемент выбран
                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    foreach (var item in _selectedComponents)
                    {
                        item.PaintAvaModel = selectVm.SelectedArticle;
                        OnPropertyChanged(nameof(item.PartnumberOrArticle));
                        //var part = item.Component as AGR_PartComponentVM;
                        // Присваиваем выбранный AvaArticleModel в BaseMaterial.AvaModel
                        //part.BaseMaterial = new AGR_Material(selectVm.SelectedArticle);
                        //part.mProperties.FirstOrDefault(p => p.Name == AGR_PropertyNames.Material).Value = part.BaseMaterial.Name;
                        _logger?.LogInformation("Выбран AvaArticle {Article} для компонента {PartNumber}", selectVm.SelectedArticle.Article, item.PartNumber);

                        //OnPropertyChanged(nameof(part.BaseMaterial));
                        //OnPropertyChanged(nameof(item.MaterialName));
                    }

                    // Обновляем свойства, если это влияет на них (например, BaseMaterialCount)
                    //Task.Run(async () => await UpdatePropertiesAsync()).ConfigureAwait(false); // Вызов асинхронного метода
                }
                else
                {
                    _logger?.LogDebug("Окно выбора AvaArticle закрыто без выбора.");
                }
                DeselectAllComponents();
            }
            catch (Exception ex)
            {
                //_logger?.LogError(ex, "Ошибка при открытии окна выбора AvaArticle для компонента {PartNumber}", PartNumber);
            }
        }
        #endregion

        #region SetAvaArticleCommand
        private ICommand _SetAvaArticleCommand;
        public ICommand SetAvaArticleCommand => _SetAvaArticleCommand
            ??= new RelayCommand(OnSetAvaArticleCommandExecuted, CanSetAvaArticleCommandExecute);
        private bool CanSetAvaArticleCommandExecute(object p)
        {
            var _selectedComponents = Components.Where(x => x.IsSelected == true);
            //Нельзя устанавливать один и тот же артикул разным компонентам
            if (_selectedComponents.Count() > 1) return false;
            return true;
        }
        private void OnSetAvaArticleCommandExecuted(object p)
        {
            var comp = SelectedComponents.FirstOrDefault();

            try
            {
                // Получаем нужные сервисы для VM
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                // Создаем ViewModel
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SearchText = comp.Name;

                // Создаем View и устанавливаем DataContext
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };
                selectView.ShowActivated = true;
                // Открываем окно модально
                selectView.ShowDialog();

                // Если окно закрыто с результатом OK и элемент выбран
                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    comp.AvaArticle = selectVm.SelectedArticle;
                }
                else
                {
                    _logger?.LogDebug("Окно выбора AvaArticle закрыто без выбора.");
                }
            }
            catch (Exception ex)
            {
                //_logger?.LogError(ex, "Ошибка при открытии окна выбора AvaArticle для компонента {PartNumber}", PartNumber);
            }

        }
        #endregion

        #region SetAvaTypeCommand
        private ICommand _SetAvaTypeCommand;
        public ICommand SetAvaTypeCommand => _SetAvaTypeCommand
            ??= new RelayCommand(OnSetAvaTypeCommandExecuted, CanSetAvaTypeCommandExecute);
        private bool CanSetAvaTypeCommandExecute(object p) => true;
        private void OnSetAvaTypeCommandExecuted(object p)
        {
            var _selectedComponents = Components.Where(x => x.IsSelected == true).Count() > 0 ?
                Components.Where(x => x.IsSelected == true)
                : SelectedComponents;

            foreach (var component in _selectedComponents)
            {

                switch (p.ToString())
                {
                    case "20021":
                    component.ComponentAvaType = AGR_AvaType_e.Purchased;
                    break;
                    case "3":
                    component.ComponentAvaType = AGR_AvaType_e.Production;
                    break;
                    case "5":
                    component.ComponentAvaType = AGR_AvaType_e.Component;
                    break;
                    case "50625":
                    component.ComponentAvaType = AGR_AvaType_e.VirtualComponent;
                    break;
                }
            }


            UpdateGroupedView();

        }
        #endregion 

        #endregion
    }
}
