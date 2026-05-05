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
        private readonly ObservableCollection<ComponentItemViewModel> _selectedComponents;

        #region CTOR

        public TechProcessEditorViewModel(
            ObservableCollection<ComponentItemViewModel> selectedComponents,
            DataContext dataContext,
            ILogger<TechProcessEditorViewModel>? logger,
            UnitOfWork unitOfWork)
        {
            _selectedComponents = selectedComponents ?? new ObservableCollection<ComponentItemViewModel>();
            _dataContext = dataContext;
            _logger = logger;
            _unitOfWork = unitOfWork;

            // Инициализируем коллекцию компонентов из выбранных
            foreach (var comp in selectedComponents)
            {
                Components.Add(comp);
            }

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
            operation.ParentComponent.OnPropertyChanged(nameof(ComponentItemViewModel.HasZeroTimeOperations));
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

        #endregion

        public event EventHandler? CloseRequested;
    }
}
