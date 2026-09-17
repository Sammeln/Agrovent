using Agrovent.ViewModels.Tree;
using Agrovent.ViewModels.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для AGR_ProjectExplorerView.xaml.
    ///
    /// Drag&amp;drop реализован вручную поверх стандартного WPF DragDrop:
    ///  - обычное перетаскивание -> MoveNodeCommand (переносит узел/связь)
    ///  - перетаскивание с зажатым Ctrl -> CopyComponentToProjectCommand
    ///    (доступно только для сборок/деталей - копирование папки-проекта не имеет смысла)
    /// </summary>
    public partial class AGR_ProjectExplorerView : Window
    {
        private const string DragFormat = "AGR_ProjectExplorer_Node";

        private Point _dragStartPoint;
        private object? _draggedNode;

        public AGR_ProjectExplorerView()
        {
            InitializeComponent();
        }

        private AGR_ProjectExplorerVM? VM => DataContext as AGR_ProjectExplorerVM;

        #region Drag start

        private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedNode = (sender as TreeViewItem)?.DataContext;
        }

        private void TreeViewItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedNode == null) return;

            var currentPosition = e.GetPosition(null);
            var diff = _dragStartPoint - currentPosition;

            if (System.Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                System.Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var treeViewItem = sender as TreeViewItem;
            if (treeViewItem == null) return;

            var effects = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                ? DragDropEffects.Copy
                : DragDropEffects.Move;

            // Копирование доступно только для сборок/деталей
            if (effects == DragDropEffects.Copy && _draggedNode is not AGR_ComponentNode)
            {
                effects = DragDropEffects.Move;
            }

            var data = new DataObject(DragFormat, _draggedNode);
            DragDrop.DoDragDrop(treeViewItem, data, effects);
            _draggedNode = null;
        }

        #endregion

        #region Drop

        private void TreeViewItem_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = GetEffect(sender, e);
            e.Handled = true;
        }

        private void TreeViewItem_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            var effect = GetEffect(sender, e);
            if (effect == DragDropEffects.None) return;

            var targetNode = (sender as TreeViewItem)?.DataContext as AGR_ProjectNode;
            if (!e.Data.GetDataPresent(DragFormat)) return;
            var sourceNode = e.Data.GetData(DragFormat);
            if (targetNode == null || sourceNode == null || VM == null) return;

            if (effect == DragDropEffects.Copy && sourceNode is AGR_ComponentNode compNode)
            {
                VM.CopyComponentToProjectCommand.Execute((compNode, targetNode));
            }
            else
            {
                VM.MoveNodeCommand.Execute((sourceNode, targetNode));
            }
        }

        /// <summary>Определяет, разрешён ли drop в данную точку, и каким будет эффект (Move/Copy/None).</summary>
        private DragDropEffects GetEffect(object sender, DragEventArgs e)
        {
            var targetNode = (sender as TreeViewItem)?.DataContext as AGR_ProjectNode;
            if (!e.Data.GetDataPresent(DragFormat)) return DragDropEffects.None;
            var sourceNode = e.Data.GetData(DragFormat);
            if (targetNode == null || sourceNode == null || VM == null) return DragDropEffects.None;

            var wantsCopy = e.KeyStates.HasFlag(DragDropKeyStates.ControlKey) && sourceNode is AGR_ComponentNode;

            if (wantsCopy)
            {
                var canCopy = VM.CopyComponentToProjectCommand.CanExecute(((AGR_ComponentNode)sourceNode, targetNode));
                return canCopy ? DragDropEffects.Copy : DragDropEffects.None;
            }

            var canMove = VM.MoveNodeCommand.CanExecute((sourceNode, targetNode));
            return canMove ? DragDropEffects.Move : DragDropEffects.None;
        }

        #endregion

        #region Double click - открыть компонент

        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var node = (sender as TreeViewItem)?.DataContext as AGR_ComponentNode;
            if (node == null || VM == null) return;

            if (VM.OpenComponentCommand.CanExecute(node))
            {
                VM.OpenComponentCommand.Execute(node);
                e.Handled = true;
            }
        }

        #endregion
    }
}
