using Agrovent.DAL.Entities.Base;
using Agrovent.DAL.Infrastructure.Enums;




namespace Agrovent.DAL.Entities.Components
{
    public class ComponentProperty : BaseEntity
    {
        public int ComponentVersionId { get; set; }
        public ComponentVersion ComponentVersion { get; set; }

        public string Name { get; set; }
        public string Value { get; set; }
        public AGR_PropertyType_e Type { get; set; }
    }
}
