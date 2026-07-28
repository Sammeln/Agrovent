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
using System.Windows.Threading;
using Agrovent.ViewModels.Specification;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для AGR_SpecificationWindow.xaml
    /// </summary>
    public partial class AGR_SpecificationWindow : Window
    {
        public AGR_SpecificationWindow()
        {
            InitializeComponent();
        }
        // Окно уже показано (со своим прогресс-оверлеем в XAML, см. IsLoading/LoadingStatus) —
        // здесь запускаем фактическую асинхронную загрузку из БД для режима редактирования сборки.
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AGR_AssemblyEditVM vm)
            {
                await vm.InitializeAsync();
            }
            ResetColumnWidth();
        }
        private void ResetColumnWidth()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var column in ComponentsDataGrid.Columns)
                {
                    var width = column.Width;
                    column.Width = 0;
                    column.Width = width;
                }
            }), DispatcherPriority.Loaded);
        }
    }
}
