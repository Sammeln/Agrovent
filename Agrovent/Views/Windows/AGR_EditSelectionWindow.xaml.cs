using System.Windows;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// Окно выбора компонентов для «Взять на редактирование» / «Отменить редактирование».
    /// Вся логика в AGR_EditSelectionViewModel; результат в AGR_EditSelectionViewModel.DialogResult.
    /// </summary>
    public partial class AGR_EditSelectionWindow : Window
    {
        public AGR_EditSelectionWindow()
        {
            InitializeComponent();
        }
    }
}
