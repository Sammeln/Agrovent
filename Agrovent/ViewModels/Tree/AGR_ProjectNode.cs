// File: ViewModels/Tree/ProjectNode.cs
using System.Collections.ObjectModel;
using Agrovent.ViewModels.Base;
using AgroventInfrastructure.Enums;

namespace Agrovent.ViewModels.Tree
{
    public class AGR_ProjectNode : BaseViewModel
    {
        /// <summary>Id проекта в БД. Null у ещё не сохранённого узла И у виртуального корня "Несортированное".</summary>
        public int? DatabaseId { get; set; }

        /// <summary>
        /// True только для единственного виртуального корня "Несортированное".
        /// Он не хранится в БД, не переименовывается/не удаляется, а его содержимое
        /// всегда перечитывается заново из репозитория (см. RefreshUnsortedNodeAsync в VM).
        /// </summary>
        public bool IsUnsortedRoot { get; }

        public AGR_ProjectNode(string name, AGR_ProjectNode? parent = null, int? databaseId = null, bool isUnsortedRoot = false)
        {
            Name = name;
            Parent = parent;
            DatabaseId = databaseId;
            IsUnsortedRoot = isUnsortedRoot;
            Children = new ObservableCollection<object>(); // Смешиваем ProjectNode и ComponentNode
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        public AGR_ProjectNode? Parent { get; set; }
        public ObservableCollection<object> Children { get; }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => Set(ref _isExpanded, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public AGR_NodeType_e NodeType => AGR_NodeType_e.Project;

        /// <summary>Только вложенные подпроекты, без компонентов. Используется, например,
        /// диалогом выбора проекта (AGR_ProjectPickerView), где сборки показывать не нужно.</summary>
        public IEnumerable<AGR_ProjectNode> ChildProjects => Children.OfType<AGR_ProjectNode>();

        // Вспомогательные методы для работы с потомками
        public void AddChild(object child)
        {
            if (child is AGR_ProjectNode projNode) projNode.Parent = this;
            if (child is AGR_ComponentNode compNode) compNode.Parent = this;
            Children.Add(child);
        }

        public void RemoveChild(object child)
        {
            if (child is AGR_ProjectNode projNode) projNode.Parent = null;
            if (child is AGR_ComponentNode compNode) compNode.Parent = null;
            Children.Remove(child);
        }
    }
}
