using System.Collections.Generic;
using AgroventInfrastructure.Interfaces.Entities.Components;

namespace AgroventInfrastructure.Interfaces.Entities
{
    public interface IAGR_UserEntity : IAGR_BaseEntity
    {
        string FirstName { get; set; }
        string FullName { get; }
        string Initials { get; }
        string LastName { get; set; }
        string Patronymic { get; set; }
        ICollection<IAGR_ComponentVersionEntity> SavedComponentVersions { get; set; }
    }
}