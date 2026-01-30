// File: ViewModels/Tree/ComponentNode.cs
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;
using Agrovent.ViewModels.Base;

namespace Agrovent.ViewModels.Tree
{
    public class AGR_ComponentNode : BaseViewModel
    {
        public AGR_ComponentNode(ComponentVersion componentVersion, AGR_ProjectNode? parent = null)
        {
            ComponentVersion = componentVersion;
            Name = componentVersion.Name; // Или какое-то другое имя для отображения
            Parent = parent;
        }

        public ComponentVersion ComponentVersion { get; }
        public string Name { get; set; }
        public AGR_ProjectNode? Parent { get; set; }
        public bool IsSelected { get; set; }

        public AGR_NodeType_e NodeType => AGR_NodeType_e.Component;
    }
}