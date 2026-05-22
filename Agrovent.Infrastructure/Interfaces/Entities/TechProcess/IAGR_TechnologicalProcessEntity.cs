using System.Collections.Generic;
using AgroventInfrastructure.Interfaces.Entities.Components;

namespace AgroventInfrastructure.Interfaces.Entities.TechProcess
{
    public interface IAGR_TechnologicalProcessEntity : IAGR_DateStampEntity
    {
        IAGR_ComponentEntity Component { get; set; }
        ICollection<IAGR_OperationEntity> Operations { get; set; }
        string PartNumber { get; set; }
    }
}