using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;


namespace Agrovent.Infrastructure.Behaviours
{
    public static class AutoScrollBehavior
    {
        public static readonly DependencyProperty AutoScrollProperty =
            DependencyProperty.RegisterAttached(
                "AutoScroll",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new FrameworkPropertyMetadata(defaultValue: false, propertyChangedCallback: OnAutoScrollChanged));

        public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);
        public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);

        private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListView listView)
            {
                if ((bool)e.NewValue)
                {
                    // При загрузке контрола подписываемся на изменения коллекции
                    listView.Loaded += ListViewOnLoaded;
                }
                else
                {
                    listView.Loaded -= ListViewOnLoaded;
                    UnsubscribeFromCollection(listView);
                }
            }
        }

        private static void ListViewOnLoaded(object sender, RoutedEventArgs e)
        {
            var listView = (ListView)sender;
            SubscribeToCollection(listView);
        }

        private static void SubscribeToCollection(ListView listView)
        {
            if (listView.ItemsSource is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += (s, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Add)
                    {
                        listView.ScrollIntoView(args.NewItems[args.NewItems.Count - 1]);
                    }
                };
            }
        }

        private static void UnsubscribeFromCollection(ListView listView)
        {
            if (listView.ItemsSource is INotifyCollectionChanged incc)
            {
                // Если нужно отписаться — делай это здесь, если используется WeakEvent
            }
        }
    }
}
