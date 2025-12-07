namespace Agrovent.Infrastructure.Interfaces.Components
{

public interface IAGR_HasFile
{
    abstract string? CurrentModelFilePath { get; }
    abstract string? CurrentDrawFilePath { get; }
    abstract string? StorageModelFilePath { get; }
    abstract string? StorageDrawFilePath { get; }
    abstract string? ProductionModelFilePath { get; }
    abstract string? ProductionDrawFilePath { get; }
    }
}