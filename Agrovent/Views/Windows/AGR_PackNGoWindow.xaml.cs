using System.Windows;
using Agrovent.ViewModels.PackNGo;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// Окно "Pack'n'Go". Вся логика в AGR_PackNGoVM, окно - чистое представление.
    /// После закрытия результат доступен через AGR_PackNGoVM.DialogResult (Save/Cancel
    /// сами закрывают окно и выставляют это значение, поэтому DialogResult самого окна
    /// не используется напрямую).
    /// </summary>
    public partial class AGR_PackNGoWindow : Window
    {
        public AGR_PackNGoWindow()
        {
            InitializeComponent();
        }

        public AGR_PackNGoVM ViewModel => DataContext as AGR_PackNGoVM;
    }
}
