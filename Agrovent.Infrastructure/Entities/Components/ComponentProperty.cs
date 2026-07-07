using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.Infrastructure.Enums;
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
