using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Agrovent.Infrastructure.Behaviors
{
    public static class AGR_DataGridSelectedItemsBehavior
    {
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IList),
                typeof(AGR_DataGridSelectedItemsBehavior),
                new PropertyMetadata(null, OnSelectedItemsChanged));

        public static IList GetSelectedItems(DependencyObject obj) =>
            (IList)obj.GetValue(SelectedItemsProperty);

        public static void SetSelectedItems(DependencyObject obj, IList value) =>
            obj.SetValue(SelectedItemsProperty, value);

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid grid)
            {
                grid.SelectionChanged -= Grid_SelectionChanged;

                if (e.NewValue is IList newList)
                {
                    grid.SelectionChanged += Grid_SelectionChanged;
                    UpdateSelectedItems(grid, newList);
                }
            }
        }

        private static void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid grid && GetSelectedItems(grid) is IList selectedItems)
            {
                selectedItems.Clear();
                foreach (var item in grid.SelectedItems)
                    selectedItems.Add(item);
            }
        }

        private static void UpdateSelectedItems(DataGrid grid, IList selectedItems)
        {
            grid.SelectedItems.Clear();
            foreach (var item in selectedItems)
                grid.SelectedItems.Add(item);
        }
    }
}