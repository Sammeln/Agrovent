using Agrovent.ViewModels.Tree;
using Agrovent.ViewModels.Windows;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для AGR_ProjectPickerView.xaml.
    /// Закрывается сама, когда VM выставляет IsDialogResultAccepted (и на OK, и на Cancel) -
    /// тот же паттерн, что и у AGR_SelectAvaArticleView.
    /// </summary>
    public partial class AGR_ProjectPickerView : Window
    {
        public AGR_ProjectPickerView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AGR_ProjectPickerVM oldVm) oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is AGR_ProjectPickerVM newVm) newVm.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AGR_ProjectPickerVM.IsDialogResultAccepted))
            {
                Close();
            }
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (DataContext is AGR_ProjectPickerVM vm && sender is TreeViewItem item)
            {
                vm.SelectedProject = item.DataContext as AGR_ProjectNode;
            }
            e.Handled = true; // не даём событию всплыть на родительский TreeViewItem
        }
    }
}
