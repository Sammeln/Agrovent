using System.Windows;
using System.Windows.Input;
using Agrovent.Infrastructure.Commands;
using Agrovent.ViewModels.Base;

namespace Agrovent.ViewModels.PackNGo
{
    /// <summary>ViewModel небольшого диалога "Найти/Заменить" для окна Pack'n'Go.</summary>
    public class AGR_PackNGoFindReplaceVM : BaseViewModel
    {
        private string _findWhat = string.Empty;
        public string FindWhat
        {
            get => _findWhat;
            set => Set(ref _findWhat, value);
        }

        private string _replaceWith = string.Empty;
        public string ReplaceWith
        {
            get => _replaceWith;
            set => Set(ref _replaceWith, value);
        }

        private ICommand _okCommand;
        public ICommand OkCommand => _okCommand ??= new RelayCommand(p =>
        {
            if (p is Window window)
            {
                window.DialogResult = true;
                window.Close();
            }
        }, _ => !string.IsNullOrEmpty(FindWhat));

        private ICommand _cancelCommand;
        public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(p =>
        {
            if (p is Window window)
            {
                window.DialogResult = false;
                window.Close();
            }
        });
    }
}
