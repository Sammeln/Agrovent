using System.Collections.Generic;
using AgroventInfrastructure.Interfaces.Entities.TechProcess;

namespace AgroventInfrastructure.Interfaces.Entities.Components
{
    public interface IAGR_ComponentEntity : IAGR_DateStampEntity
    {
        string PartNumber { get; set; }
        IAGR_TechnologicalProcessEntity? TechnologicalProcess { get; set; }
        abstract ICollection<IAGR_ComponentVersionEntity> Versions { get; set; }

        IAGR_ComponentVersionEntity? GetLatestVersion();
    }
}