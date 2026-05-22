using AgroventInfrastructure.Interfaces.Entities.Components;

namespace AgroventInfrastructure.Interfaces.Entities.Projects
{
    public interface IAGR_ProjectComponentEntity
    {
        IAGR_ComponentVersionEntity ComponentVersion { get; set; }
        int ComponentVersionId { get; set; }
        int Id { get; set; }
        IAGR_ProjectEntity Project { get; set; }
        int ProjectId { get; set; }
    }
}