namespace AgroventInfrastructure.Interfaces.Entities.Components
{
    public interface IAGR_ComponentPropertyEntity : IAGR_DateStampEntity
    {
        IAGR_ComponentVersionEntity ComponentVersion { get; set; }
        int ComponentVersionId { get; set; }
        string Name { get; set; }
        string Value { get; set; }
    }
}