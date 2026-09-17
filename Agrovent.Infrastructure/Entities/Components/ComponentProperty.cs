using System.ComponentModel.DataAnnotations.Schema;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Entities.Base;
using AgroventInfrastructure.Interfaces.Entities.Components;

namespace AgroventInfrastructure.Entities.Components
{
    public class ComponentProperty : DateStampEntity
    {
        public int ComponentVersionId { get; set; }

        [ForeignKey("ComponentVersionId")]
        public ComponentVersion ComponentVersion { get; set; }

        public string Name { get; set; }
        public string Value { get; set; }
    }
}
