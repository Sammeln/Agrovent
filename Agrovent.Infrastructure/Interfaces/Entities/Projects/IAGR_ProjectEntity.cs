using System.Collections.Generic;

namespace AgroventInfrastructure.Interfaces.Entities.Projects
{
    public interface IAGR_ProjectEntity
    {
        ICollection<IAGR_ProjectEntity> Children { get; set; }
        int Id { get; set; }
        string Name { get; set; }
        IAGR_ProjectEntity? Parent { get; set; }
        int? ParentId { get; set; }
        ICollection<IAGR_ProjectComponentEntity> ProjectComponents { get; set; }
    }
}