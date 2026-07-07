using System;
using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.Infrastructure.Enums;
using AgroventInfrastructure.Entities.Base;
using AgroventInfrastructure.Interfaces.Entities.Components;


namespace AgroventInfrastructure.Entities.Components
{
    public class ComponentFile : DateStampEntity
    {
        public int ComponentVersionId { get; set; }
        [ForeignKey("ComponentVersionId")]
        public ComponentVersion ComponentVersion { get; set; }

        public AGR_FileType_e FileType { get; set; }
        public string FilePath { get; set; }
        public DateTime? LastModified { get; set; }
        public long? FileSize { get; set; }
    }
}
