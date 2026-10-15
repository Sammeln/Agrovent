namespace Agrovent.ViewModels.Windows
{
    using System.Collections.ObjectModel;
    using System.Windows.Input;

    public class AGR_EditNodeVM : System.ComponentModel.INotifyPropertyChanged
    {
        public int ComponentId { get; init; }
        public string PartNumber { get; init; } = "";
        public string Name { get; init; } = "";
        public int Version { get; init; }
        public bool IsPurchased { get; init; }                       // заблокирован
        public AgroventInfrastructure.Enums.AGR_EditState_e State { get; init; }
        public string? LockedByUser { get; init; }
        public AGR_EditNodeVM? Parent { get; set; }
        public ObservableCollection<AGR_EditNodeVM> Children { get; } = new();

        // Можно ли менять отметку: не покупной и не занят другим. Свой замок: отмечен и не снимается здесь.
        public bool CanToggle => !IsPurchased
            && State != AgroventInfrastructure.Enums.AGR_EditState_e.EditingByOther
            && State != AgroventInfrastructure.Enums.AGR_EditState_e.EditingByMe;

        private bool _IsChecked;
        public bool IsChecked
        {
            get => _IsChecked;
            set { _IsChecked = value; PropertyChanged?.Invoke(this, new(nameof(IsChecked))); }
        }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    public class AGR_EditSelectionViewModel
    {
        public ObservableCollection<AGR_EditNodeVM> Roots { get; } = new();
        public string TargetFolder { get; set; } = "";      // по умолчанию LocalWorkFolder + имя компонента
        public bool? DialogResult { get; private set; }

        /// <summary>Отметка узла: отмечаем все его вхождения по ComponentId и всех их родителей.</summary>
        public void SetChecked(AGR_EditNodeVM node, bool value)
        {
            foreach (var n in Flatten().Where(n => n.ComponentId == node.ComponentId && n.CanToggle))
            {
                n.IsChecked = value;
                for (var p = n.Parent; p != null; p = p.Parent)
                    if (p.CanToggle) p.IsChecked = value ? true : p.Children.Any(c => c.IsChecked);
            }
        }

        public System.Collections.Generic.IEnumerable<AGR_EditNodeVM> Flatten()
        {
            var stack = new Stack<AGR_EditNodeVM>(Roots);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                yield return n;
                foreach (var c in n.Children) stack.Push(c);
            }
        }

        #region COMMANDS

        #region ConfirmEditSelectionCommand
        private ICommand _ConfirmEditSelectionCommand;
        public ICommand ConfirmEditSelectionCommand => _ConfirmEditSelectionCommand
            ??= new Agrovent.Infrastructure.Commands.RelayCommand(OnConfirmEditSelectionCommandExecuted, CanConfirmEditSelectionCommandExecute);
        private bool CanConfirmEditSelectionCommandExecute(object p)
            => !string.IsNullOrWhiteSpace(TargetFolder) && Flatten().Any(n => n.IsChecked && n.State != AgroventInfrastructure.Enums.AGR_EditState_e.EditingByMe);
        private void OnConfirmEditSelectionCommandExecuted(object p)
        {
            DialogResult = true;
        }
        #endregion

        #region CancelEditSelectionCommand
        private ICommand _CancelEditSelectionCommand;
        public ICommand CancelEditSelectionCommand => _CancelEditSelectionCommand
            ??= new Agrovent.Infrastructure.Commands.RelayCommand(OnCancelEditSelectionCommandExecuted, CanCancelEditSelectionCommandExecute);
        private bool CanCancelEditSelectionCommandExecute(object p) => true;
        private void OnCancelEditSelectionCommandExecuted(object p)
        {
            DialogResult = false;
        }
        #endregion

        #endregion
    }
}