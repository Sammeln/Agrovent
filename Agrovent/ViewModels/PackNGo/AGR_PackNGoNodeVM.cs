using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Agrovent.Infrastructure.Commands;
using Agrovent.ViewModels.Base;

namespace Agrovent.ViewModels.PackNGo
{
    /// <summary>
    /// Один узел дерева сборки в диалоге Pack'n'Go - конкретная позиция конкретного компонента
    /// в структуре сборки (уровень вложенности, родитель, дочерние узлы). Если один и тот же файл
    /// используется в сборке несколько раз, для него будет создано несколько узлов - но все они
    /// будут ссылаться на один и тот же экземпляр AGR_PackNGoComponentVM (см. ComponentVM),
    /// поэтому редактирование в одной строке сразу видно в остальных.
    /// </summary>
    public class AGR_PackNGoNodeVM : BaseViewModel
    {
        public AGR_PackNGoComponentVM ComponentVM { get; }
        public AGR_PackNGoNodeVM? Parent { get; }
        public int Level { get; }

        public ObservableCollection<AGR_PackNGoNodeVM> Children { get; } = new ObservableCollection<AGR_PackNGoNodeVM>();

        public bool HasChildren => Children.Count > 0;

        /// <summary>Отступ строки в таблице (px) в зависимости от уровня вложенности.</summary>
        public double Indent => Level * 18;

        private int _quantity = 1;

        /// <summary>Количество одинаковых по имени/партномеру экземпляров этого компонента
        /// в составе НЕПОСРЕДСТВЕННОГО родителя (см. AGR_PackNGoVM.BuildChildrenFromRepo -
        /// такие экземпляры схлопываются в одну строку вместо N одинаковых). Не путать с
        /// вхождениями этого же файла в РАЗНЫХ подсборках - это остаются разные узлы (строки).</summary>
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (Set(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        /// <summary>Имя компонента для отображения в колонке "Компонент" - с количеством,
        /// если сгруппировано несколько одинаковых экземпляров, напр. "Болт М6х20 (×3)".</summary>
        public string DisplayName => Quantity > 1
            ? $"{ComponentVM.Name} (×{Quantity})"
            : ComponentVM.Name;

        public AGR_PackNGoNodeVM(AGR_PackNGoComponentVM componentVM, AGR_PackNGoNodeVM? parent, int level, int quantity = 1)
        {
            ComponentVM = componentVM ?? throw new ArgumentNullException(nameof(componentVM));
            Parent = parent;
            Level = level;
            _quantity = quantity;

            // Имя компонента может обновиться (в текущей реализации не меняется, но на всякий
            // случай синхронизируем DisplayName, если Name когда-либо станет уведомляемым).
            ComponentVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AGR_PackNGoComponentVM.Name))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            };
        }

        #region IsExpanded

        private bool _isExpanded = true;

        /// <summary>Развёрнут ли узел в таблице (показывать ли дочерние строки). Изменение
        /// поднимает событие ExpandedChanged, по которому AGR_PackNGoVM перестраивает список
        /// видимых строк.</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (Set(ref _isExpanded, value))
                {
                    ExpandedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? ExpandedChanged;

        #region ToggleExpandCommand

        private ICommand _toggleExpandCommand;
        public ICommand ToggleExpandCommand => _toggleExpandCommand
            ??= new RelayCommand(_ => IsExpanded = !IsExpanded, _ => HasChildren);

        #endregion

        #endregion
    }
}