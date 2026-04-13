using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.Base;
using Agrovent.Infrastructure.Enums;

namespace Agrovent.DAL.Entities.Components
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
