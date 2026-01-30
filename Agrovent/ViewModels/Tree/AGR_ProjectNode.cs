// File: ViewModels/Tree/ProjectNode.cs
using Agrovent.Infrastructure.Enums;
using Agrovent.ViewModels.Base;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Agrovent.ViewModels.Tree
{
    public class AGR_ProjectNode : BaseViewModel
    {
        public int? DatabaseId { get; set; }
        public AGR_ProjectNode(string name, AGR_ProjectNode? parent = null, int? databaseId = null)
        {
            Name = name;
            Parent = parent;
            DatabaseId = databaseId;
            Children = new ObservableCollection<object>(); // Смешиваем ProjectNode и ComponentNode
        }

        public string Name { get; set; }
        public AGR_ProjectNode? Parent { get; set; }
        public ObservableCollection<object> Children { get; }
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        public AGR_NodeType_e NodeType => AGR_NodeType_e.Project;

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