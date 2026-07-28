using System.Windows;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// Окно "Подобрать артикулы". Вся логика в AGR_MatchArticlesVM,
    /// это окно - чистое представление без собственного состояния.
    /// </summary>
    public partial class AGR_MatchArticlesWindow : Window
    {
        public AGR_MatchArticlesWindow()
        {
            InitializeComponent();
        }
    }
}
