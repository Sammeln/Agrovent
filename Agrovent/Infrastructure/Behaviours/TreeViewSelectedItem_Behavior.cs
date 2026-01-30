using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace Agrovent.Infrastructure.Behaviours
{
    class TreeViewSelectedItem_Behavior : Behavior<TreeView>
    {


        public object SelectedItem
        {
            get { return GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectedItem.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(object),
                typeof(TreeViewSelectedItem_Behavior), new PropertyMetadata(null, OnSelectedItemChanged));


        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectedItemChanged += AssociatedObject_SelectedItemChanged;
        }
        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.SelectedItemChanged -= AssociatedObject_SelectedItemChanged;
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //var item = e.NewValue as PRP_ComponentViewModel;
        }

        private void AssociatedObject_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedItem = e.NewValue;
        }
    }
}
