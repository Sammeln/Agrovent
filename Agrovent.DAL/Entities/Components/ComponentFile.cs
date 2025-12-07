using Agrovent.DAL.Entities.Base;
using Agrovent.DAL.Infrastructure.Enums;


namespace Agrovent.DAL.Entities.Components
{
    public class ComponentFile : BaseEntity
    {
        public int ComponentVersionId { get; set; }
        public ComponentVersion ComponentVersion { get; set; }

        public AGR_FileType_e FileType { get; set; }
        public string FilePath { get; set; }
        public DateTime? LastModified { get; set; }
        public long? FileSize { get; set; }
    }
}
