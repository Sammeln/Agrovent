using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для AGR_ProjectExplorerView.xaml
    /// </summary>
    public partial class AGR_ProjectExplorerView : Window
    {
        public AGR_ProjectExplorerView()
        {
            InitializeComponent();
        }
        private void ProjectsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Можно обновить свойства в главном окне или выполнить другие действия при выборе узла
            // Лучше использовать Command и SelectedItem в VM, но для простоты пока оставим так
        }
    }
}
