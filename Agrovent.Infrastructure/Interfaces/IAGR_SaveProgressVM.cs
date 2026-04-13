// File: ViewModels/Windows/SaveProgressVM.cs
using System.Collections.ObjectModel;

namespace Agrovent.ViewModels.Windows
{
    public interface IAGR_SaveProgressVM
    {
        abstract void AddLogMessage(string message);
    }
}