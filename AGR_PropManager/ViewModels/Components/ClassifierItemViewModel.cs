// File: ViewModels/Components/ClassifierItemViewModel.cs
using AGR_PropManager.ViewModels.Base;
using System.Windows.Media.Imaging;

namespace AGR_PropManager.ViewModels.Components
{
    public class ClassifierItemViewModel : BaseViewModel
    {
        #region Properties

        #region Id
        private int _Id;
        public int Id
        {
            get => _Id;
            set => Set(ref _Id, value);
        }
        #endregion

        #region PartNumber
        private string _PartNumber = "";
        public string PartNumber
        {
            get => _PartNumber;
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

        #region SavedDate
        private DateTime _SavedDate;
        public DateTime SavedDate
        {
            get => _SavedDate;
            set => Set(ref _SavedDate, value);
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
    }
}
