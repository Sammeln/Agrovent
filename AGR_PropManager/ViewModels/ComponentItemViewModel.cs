// File: ViewModels/ComponentItemViewModel.cs
using AGR_PropManager.ViewModels.Base; // BaseViewModel
using System.Windows.Media.Imaging; // BitmapImage
using System.Windows.Input; // ICommand
using AGR_PropManager.Infrastructure.Commands; // RelayCommand

namespace AGR_PropManager.ViewModels
{
    public class ComponentItemViewModel : BaseViewModel
    {
        #region Properties

        #region IsSelected
        private bool _IsSelected = false;
        
        public bool IsSelected
        {
            get => _IsSelected;
            set
            {
                if (Set(ref _IsSelected, value))
                {
                    // Уведомляем родительский ViewModel о смене выделения
                    // Это может быть реализовано через событие или через вызов метода в MainWindowViewModel
                    // Пока оставим как есть, предполагая, что MainWindowViewModel подписывается на PropertyChanged
                }
            }
        }
        #endregion

        #region PartNumber
        private string _PartNumber = "";
        public string PartNumber
        {
            get
            {
                return _PartNumber;
            }

            set => Set(ref _PartNumber, value);
        }
        #endregion

        #region Name
        private string _Name = "";
        public string Name
        {
            get => _Name;
            set => Set(ref _Name, value);
        }
        #endregion

        #region Quantity
        private int _Quantity;
        public int Quantity
        {
            get => _Quantity;
            set => Set(ref _Quantity, value);
        }
        #endregion

        #region Material
        private string _Material = "";
        public string Material
        {
            get => _Material;
            set => Set(ref _Material, value);
        }
        #endregion

        #region Paint
        private string _Paint = "";
        public string Paint
        {
            get => _Paint;
            set => Set(ref _Paint, value);
        }
        #endregion

        #region BendCount
        private int _BendCount;
        public int BendCount
        {
            get => _BendCount;
            set => Set(ref _BendCount, value);
        }
        #endregion

        #region ContourLength
        private decimal _ContourLength;
        public decimal ContourLength
        {
            get => _ContourLength;
            set => Set(ref _ContourLength, value);
        }
        #endregion

        #region ComponentTypeDisplay
        private string _ComponentTypeDisplay = "";
        public string ComponentTypeDisplay
        {
            get => _ComponentTypeDisplay;
            set => Set(ref _ComponentTypeDisplay, value);
        }
        #endregion

        #region TechProcessSummary
        private string _TechProcessSummary = "";
        public string TechProcessSummary
        {
            get => _TechProcessSummary;
            set => Set(ref _TechProcessSummary, value);
        }
        #endregion

        #region PreviewImage
        private BitmapImage? _PreviewImage;
        public BitmapImage? PreviewImage
        {
            get => _PreviewImage;
            set => Set(ref _PreviewImage, value);
        }
        #endregion

        #endregion

        // Свойство для подсветки строк с нулевым временем
        public bool HasZeroTimeOperations { get; set; } = false;

        // Массив операций (для отображения summary и подсветки)
        public List<ComponentTechProcessOperationViewModel> Operations { get; set; } = new();

        public ComponentItemViewModel()
        {
            // Конструктор для Design-time
        }
    }

    // Вспомогательная VM для операции (новая сущность)
    public class ComponentTechProcessOperationViewModel
    {
        public string OperationName { get; set; } = "";
        public decimal TotalTime { get; set; }
        public int SequenceNumber { get; set; }
        public bool IsZeroTime => TotalTime == 0;
    }
}