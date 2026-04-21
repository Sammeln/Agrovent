// File: ViewModels/Windows/SaveProgressVM.cs
using System.Collections.ObjectModel;

namespace Agrovent.ViewModels.Windows
{
    public interface IAGR_SaveProgressVM
    {
        void AddLogMessage(string message);
    }
    
    public interface IAGR_SaveConfirmationVM
    {
        bool? ShowDialog();
    }
}