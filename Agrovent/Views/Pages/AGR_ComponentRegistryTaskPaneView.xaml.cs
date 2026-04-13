using System.ComponentModel;
using System.Windows.Controls;
using Agrovent.Properties;
using Xarial.XCad.Base.Attributes;

namespace Agrovent.Views.Pages
{
    /// <summary>
    /// Логика взаимодействия для AGR_ComponentRegistryTaskPaneView.xaml
    /// </summary>
    [Icon(typeof(Resources), nameof(Properties.Resources.FolderIcon32))]
    [Title("WPF Task Pane Example")]
    [Description("Example of WPF control hosted in SOLIDWORKS Task Pane control")]
    public partial class AGR_ComponentRegistryTaskPaneView : UserControl
    {
        public AGR_ComponentRegistryTaskPaneView()
        {
            InitializeComponent();
        }
    }
}
