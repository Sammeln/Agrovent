namespace Agrovent.DAL.Infrastructure.Interfaces;
public interface IHasFile
{
    abstract string? CurrentModelFilePath { get; }
    abstract string? CurrentDrawFilePath { get; }
    abstract string? StorageModelFilePath { get; }
    abstract string? StorageDrawFilePath { get; }
    abstract string? ProductionModelFilePath { get; }
    abstract string? ProductionDrawFilePath { get; }
}