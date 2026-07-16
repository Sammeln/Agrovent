using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Agrovent.ViewModels.Base
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        #region IsLoading / LoadingStatus (общий индикатор загрузки для окон вида TechProcessEditorWindow)
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        private string _loadingStatus = string.Empty;
        public string LoadingStatus
        {
            get => _loadingStatus;
            set => Set(ref _loadingStatus, value);
        }
        #endregion

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual bool Set(Action action, [CallerMemberName] string propertyName = null)
        {
            action.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }


    }
    
}
