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
using Agrovent.ViewModels.Windows;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для SaveConfirmationView.xaml
    /// </summary>
    public partial class SaveConfirmationView : Window
    {
        public SaveConfirmationView()
        {
            InitializeComponent();
        }

        // Окно уже показано (со своим прогресс-оверлеем в XAML, см. IsLoading/LoadingStatus) —
        // здесь запускаем фактическую асинхронную загрузку из БД для режима редактирования.
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AGR_ComponentEditVM vm)
            {
                await vm.InitializeAsync();
            }
        }
    }
}
