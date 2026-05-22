using System;

namespace AgroventInfrastructure.Interfaces.Entities
{
    public interface IAGR_DateStampEntity : IAGR_BaseEntity
    {
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }
}