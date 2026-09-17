// File: ViewModels/Tree/ComponentNode.cs
using Agrovent.Infrastructure;
using AgroventInfrastructure.Enums;
using Agrovent.ViewModels.Base;
using AgroventInfrastructure.Entities.Components;
using System;
using System.IO;
using System.Linq;
using AgroventInfrastructure;

namespace Agrovent.ViewModels.Tree
{
    public class AGR_ComponentNode : BaseViewModel
    {
        public AGR_ComponentNode(ComponentVersion componentVersion, AGR_ProjectNode? parent = null)
        {
            ComponentVersion = componentVersion ?? throw new ArgumentNullException(nameof(componentVersion));
            Parent = parent;
        }

        public ComponentVersion ComponentVersion { get; }

        /// <summary>Имя для отображения в дереве. Берётся напрямую из версии компонента.</summary>
        public string Name => ComponentVersion.Name;

        public string PartNumber => ComponentVersion.Component?.PartNumber ?? string.Empty;
        public int Version => ComponentVersion.Version;

        public AGR_ProjectNode? Parent { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public AGR_NodeType_e NodeType => AGR_NodeType_e.Component;

        /// <summary>
        /// Путь к главному файлу сборки/детали в хранилище (StorageRoot\HashSum\Имя файла).
        /// Тот же принцип, что и в AGR_ComponentRegistryItemVM.StoragePath.
        /// Требует, чтобы у ComponentVersion была подгружена коллекция Files (см. Include в репозитории/VM).
        /// </summary>
        public string? StoragePath
        {
            get
            {
                var file = ComponentVersion.Files?.FirstOrDefault(f =>
                    f.FilePath.EndsWith("sldasm", StringComparison.OrdinalIgnoreCase) ||
                    f.FilePath.EndsWith("sldprt", StringComparison.OrdinalIgnoreCase));

                if (file == null || string.IsNullOrEmpty(file.FilePath)) return null;

                var fileName = Path.GetFileName(file.FilePath);
                var hashFolder = ComponentVersion.HashSum.ToString("D10");
                return Path.Combine(AGR_Options.StorageRootFolderPath, hashFolder, fileName);
            }
        }
    }
}
