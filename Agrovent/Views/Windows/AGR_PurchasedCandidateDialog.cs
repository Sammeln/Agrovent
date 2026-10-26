// File: Views/Windows/AGR_PurchasedCandidateDialog.cs
using System.Windows;
using System.Windows.Controls;
using Agrovent.DAL.Services;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// 6.5: для покупного файла без маркера в БД нашлось несколько подходящих компонентов.
    /// Пользователь выбирает существующий или создаёт новый. Простой диалог без своей VM.
    /// </summary>
    public static class AGR_PurchasedCandidateDialog
    {
        /// <returns>Выбранный компонент; null - создать новый.</returns>
        public static AGR_PurchasedCandidate? Show(string fileName, IReadOnlyList<AGR_PurchasedCandidate> candidates)
        {
            var list = new ListBox { ItemsSource = candidates, SelectedIndex = 0, Margin = new Thickness(0, 8, 0, 8), MinHeight = 120 };
            var useButton = new Button { Content = "Использовать выбранный", IsDefault = true, Padding = new Thickness(12, 4, 12, 4) };
            var newButton = new Button { Content = "Создать новый", IsCancel = true, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0) };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(useButton);
            buttons.Children.Add(newButton);

            var root = new DockPanel { Margin = new Thickness(12) };
            var header = new TextBlock
            {
                Text = $"Покупной компонент «{fileName}» без обозначения. В базе найдено несколько подходящих:",
                TextWrapping = TextWrapping.Wrap
            };
            DockPanel.SetDock(header, Dock.Top);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(header);
            root.Children.Add(buttons);
            root.Children.Add(list);

            var window = new Window
            {
                Title = "Покупной компонент",
                Content = root,
                Width = 560,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                Topmost = true
            };

            AGR_PurchasedCandidate? result = null;
            useButton.Click += (_, _) =>
            {
                result = list.SelectedItem as AGR_PurchasedCandidate;
                window.DialogResult = result != null;
            };
            list.MouseDoubleClick += (_, _) =>
            {
                result = list.SelectedItem as AGR_PurchasedCandidate;
                if (result != null) window.DialogResult = true;
            };

            return window.ShowDialog() == true ? result : null;
        }
    }
}
