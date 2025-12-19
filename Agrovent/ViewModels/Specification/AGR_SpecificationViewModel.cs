using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Specification;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;

namespace Agrovent.ViewModels.Specification
{
    public class AGR_SpecificationViewModel : BaseViewModel
    {
        private readonly AGR_AssemblyComponentVM _baseComponent;
        private CollectionViewSource _groupedComponentsView;

        #region Публичные свойства
        public ICollectionView GroupedComponentsView => _groupedComponentsView?.View;

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

        #region Команды
        private RelayCommand _closeCommand;
        public RelayCommand CloseCommand => _closeCommand ??= new RelayCommand(
            _ => CloseWindow?.Invoke(),
            _ => true);
        #endregion

        #region События
        public event Action CloseWindow;
        #endregion

        public AGR_SpecificationViewModel(AGR_AssemblyComponentVM baseComponent)
        {
            _baseComponent = baseComponent;
            WindowTitle = $"Спецификация: {baseComponent.Name} ({baseComponent.PartNumber})";
            Initialize();
        }

        private void Initialize()
        {
            _groupedComponentsView = new CollectionViewSource();

            // Инициализируем пустую коллекцию
            Components = new ObservableCollection<AGR_SpecificationItemVM>();

            // Загружаем компоненты асинхронно
            LoadComponents();
        }

        private void LoadComponents()
        {
            if (_baseComponent?.AGR_TopComponents == null)
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

        private void UpdateGroupedView()
        {
            _groupedComponentsView.Source = Components;

            // Настраиваем группировку по типу компонента
            _groupedComponentsView.GroupDescriptions.Clear();
            _groupedComponentsView.GroupDescriptions.Add(
                new PropertyGroupDescription("ComponentType"));

            // Сортируем группы по заданному порядку
            _groupedComponentsView.SortDescriptions.Clear();
            _groupedComponentsView.SortDescriptions.Add(
                new SortDescription("ComponentType", ListSortDirection.Ascending));

            OnPropertyChanged(nameof(GroupedComponentsView));
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
            LoadComponents();
        }
    }
}
