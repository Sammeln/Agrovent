using System.Windows;
using System.Windows.Controls;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для TaskPaneView.xaml
    /// </summary>
    public partial class AGR_TaskPaneView : UserControl
    {
        public AGR_TaskPaneView()
        {
            var _ = new Microsoft.Xaml.Behaviors.DefaultTriggerAttribute(typeof(Trigger), typeof(Microsoft.Xaml.Behaviors.TriggerBase), null);
            InitializeComponent();
        }
    }
}
