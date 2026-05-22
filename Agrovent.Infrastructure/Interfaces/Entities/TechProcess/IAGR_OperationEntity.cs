namespace AgroventInfrastructure.Interfaces.Entities.TechProcess
{
    public interface IAGR_OperationEntity : IAGR_DateStampEntity
    {
        decimal CostPerHour { get; set; }
        string Name { get; set; }
        int SequenceNumber { get; set; }
        IAGR_TechnologicalProcessEntity TechnologicalProcess { get; set; }
        int TechnologicalProcessId { get; set; }
        string WorkstationName { get; set; }
    }
}