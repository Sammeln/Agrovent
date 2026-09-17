// File: ViewModels/Windows/ProjectPickerVM.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using Agrovent.Infrastructure.Commands;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Tree;

namespace Agrovent.ViewModels.Windows
{
    /// <summary>
    /// Диалог выбора проекта из дерева, которое уже загружено в AGR_ProjectExplorerVM -
    /// отдельного обращения к БД не делает. Используется, например, командой
    /// "Скопировать в проект..." из контекстного меню проводника.
    ///
    /// Паттерн закрытия такой же, как у AGR_SelectAvaArticleVM: IsDialogResultAccepted
    /// выставляется в true (OK) или false (Cancel), а View подписывается на изменение
    /// этого свойства и закрывается сама - в обоих случаях.
    /// </summary>
    public class AGR_ProjectPickerVM : BaseViewModel
    {
        // Конструктор для дизайнера XAML
        public AGR_ProjectPickerVM()
        {
            Projects = new ObservableCollection<AGR_ProjectNode>();
        }

        public AGR_ProjectPickerVM(IEnumerable<AGR_ProjectNode> projectRoots)
        {
            Projects = new ObservableCollection<AGR_ProjectNode>(projectRoots);
        }

        public ObservableCollection<AGR_ProjectNode> Projects { get; }

        private AGR_ProjectNode? _selectedProject;
        public AGR_ProjectNode? SelectedProject
        {
            get => _selectedProject;
            set => Set(ref _selectedProject, value);
        }

        private bool? _isDialogResultAccepted;
        public bool? IsDialogResultAccepted
        {
            get => _isDialogResultAccepted;
            set => Set(ref _isDialogResultAccepted, value);
        }

        #region AcceptCommand
        private ICommand? _AcceptCommand;
        public ICommand AcceptCommand => _AcceptCommand
            ??= new RelayCommand(_ => IsDialogResultAccepted = true, _ => SelectedProject != null);
        #endregion

        #region CancelCommand
        private ICommand? _CancelCommand;
        public ICommand CancelCommand => _CancelCommand
            ??= new RelayCommand(_ =>
            {
                SelectedProject = null;
                IsDialogResultAccepted = false;
            });
        #endregion
    }
}
