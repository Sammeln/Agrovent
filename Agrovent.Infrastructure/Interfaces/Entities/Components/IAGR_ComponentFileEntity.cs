using System;
using AgroventInfrastructure.Enums;

namespace AgroventInfrastructure.Interfaces.Entities.Components
{
    public interface IAGR_ComponentFileEntity : IAGR_DateStampEntity
    {
        IAGR_ComponentVersionEntity ComponentVersion { get; set; }
        int ComponentVersionId { get; set; }
        string FilePath { get; set; }
        long? FileSize { get; set; }
        AGR_FileType_e FileType { get; set; }
        DateTime? LastModified { get; set; }
    }
}