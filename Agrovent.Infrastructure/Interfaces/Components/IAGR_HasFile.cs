namespace AgroventInfrastructure.Interfaces.Components
{

public interface IAGR_HasFile
{
    abstract string? CurrentModelFilePath { get; set; }
    abstract string? CurrentDrawFilePath { get; set; }
    abstract string? StorageModelFilePath { get; set; }
    abstract string? StorageDrawFilePath { get; set; }
    abstract string? ProductionModelFilePath { get; set; }
    abstract string? ProductionDrawFilePath { get; set; }
    }
}