using AGR_PropManager.ViewModels.Base;
using AGR_PropManager.ViewModels.Components;
using System.Collections.ObjectModel;
using Agrovent.DAL;

namespace AGR_PropManager.ViewModels.Windows
{
    public abstract class TabItemViewModel : BaseViewModel, IDisposable
    {
        private string _tabHeader;
        private bool _isClassifierTab;

        public string TabHeader
        {
            get => _tabHeader;
            set => Set(ref _tabHeader, value);
        }

        public bool IsClassifierTab
        {
            get => _isClassifierTab;
            set => Set(ref _isClassifierTab, value);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public class ClassifierTabViewModel : TabItemViewModel
    {
        public ClassifierTabViewModel()
        {
            TabHeader = "Классификатор";
            IsClassifierTab = true;
            ClassifierItems = new ObservableCollection<ClassifierItemViewModel>();
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                {
                    RefreshFilter();
                }
            }
        }

        private ObservableCollection<ClassifierItemViewModel> _classifierItems;
        public ObservableCollection<ClassifierItemViewModel> ClassifierItems
        {
            get => _classifierItems;
            set => Set(ref _classifierItems, value);
        }

        private System.ComponentModel.ICollectionView _classifierItemsView;
        public System.ComponentModel.ICollectionView ClassifierItemsView
        {
            get => _classifierItemsView;
            set => Set(ref _classifierItemsView, value);
        }

        public void InitializeView()
        {
            if (ClassifierItems == null)
            {
                ClassifierItems = new ObservableCollection<ClassifierItemViewModel>();
            }

            var cvs = new System.Windows.Data.CollectionViewSource();
            cvs.Source = ClassifierItems;
            cvs.Filter += ClassifierItems_Filter;
            ClassifierItemsView = cvs.View;
        }

        private void ClassifierItems_Filter(object sender, System.Windows.Data.FilterEventArgs e)
        {
            if (e.Item is not ClassifierItemViewModel item)
            {
                e.Accepted = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                e.Accepted = true;
                return;
            }

            e.Accepted = item.PartNumber?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) == true ||
                         item.Name?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) == true;
        }

        private void RefreshFilter()
        {
            ClassifierItemsView?.Refresh();
        }


    }

    public class ClassifierItemViewModel : BaseViewModel
    {
        private int _id;
        private string _partNumber;
        private string _name;
        private System.DateTime _savedDate;
        private System.Windows.Media.Imaging.BitmapImage _previewImage;

        public int Id { get => _id; set => Set(ref _id, value); }
        public string PartNumber { get => _partNumber; set => Set(ref _partNumber, value); }
        public string Name { get => _name; set => Set(ref _name, value); }
        public System.DateTime SavedDate { get => _savedDate; set => Set(ref _savedDate, value); }
        public System.Windows.Media.Imaging.BitmapImage PreviewImage { get => _previewImage; set => Set(ref _previewImage, value); }
    }

    public class TechProcessEditorTabViewModel : TabItemViewModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Microsoft.Extensions.Logging.ILogger? _logger;
        private bool _isDisposed = false;

        public TechProcessEditorTabViewModel(ComponentItemViewModel component, 
                                             UnitOfWork unitOfWork,
                                             Microsoft.Extensions.Logging.ILogger? logger = null)
        {
            Component = component;
            _unitOfWork = unitOfWork;
            _logger = logger;
            TabHeader = $"Редактирование: {component.PartNumber}";
            IsClassifierTab = false;
            ValidationErrors = new ObservableCollection<string>();
            InitializeView();
        }

        public ComponentItemViewModel Component { get; }

        private ObservableCollection<string> _validationErrors;
        public ObservableCollection<string> ValidationErrors
        {
            get => _validationErrors;
            set => Set(ref _validationErrors, value);
        }

        private void InitializeView()
        {
            if (_isDisposed) return;

            Component.PropertyChanged += Component_PropertyChanged;
            if (Component.Operations != null)
            {
                ((System.Collections.Specialized.INotifyCollectionChanged)Component.Operations).CollectionChanged += Operations_CollectionChanged;
            }
            Validate();
        }
        private void Component_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!_isDisposed) Validate();
        }

        private void Operations_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!_isDisposed) Validate();
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            Component.PropertyChanged -= Component_PropertyChanged;
            if (Component.Operations != null)
            {
                ((System.Collections.Specialized.INotifyCollectionChanged)Component.Operations).CollectionChanged -= Operations_CollectionChanged;
            }
        }
        public void Validate()
        {
            ValidationErrors.Clear();

            if (Component.ComponentType != Agrovent.Infrastructure.Enums.AGR_ComponentType_e.Purchased)
            {
                if (Component.TechnologicalProcessModel == null)
                {
                    ValidationErrors.Add("У производимого компонента нет техпроцесса");
                }
                else if (Component.TechnologicalProcessModel.Operations == null || 
                         Component.TechnologicalProcessModel.Operations.Count == 0)
                {
                    ValidationErrors.Add("Техпроцесс пустой");
                }
                else
                {
                    foreach (var op in Component.TechnologicalProcessModel.Operations)
                    {
                        if (op.CostPerHour <= 0)
                        {
                            ValidationErrors.Add($"Операция '{op.Name}' (№{op.SequenceNumber}): трудоемкость не заполнена или равна нулю");
                        }
                    }
                }
            }

            if (Component.ComponentType == Agrovent.Infrastructure.Enums.AGR_ComponentType_e.Purchased)
            {
                if (Component.AvaArticle == null || string.IsNullOrWhiteSpace(Component.AvaArticle?.Article.ToString()))
                {
                    ValidationErrors.Add("У покупного компонента не заполнен AvaArticle");
                }
            }

            if (Component.Quantity <= 0)
            {
                ValidationErrors.Add($"Количество компонента '{Component.Name}' равно нулю или не заполнено");
            }

            OnPropertyChanged(nameof(ValidationErrors));
        }
    }
}
