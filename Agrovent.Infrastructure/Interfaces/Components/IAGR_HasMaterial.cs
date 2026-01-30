namespace Agrovent.Infrastructure.Interfaces.Components
{
    public interface IAGR_HasMaterial
    {
        abstract IAGR_Material BaseMaterial { get; }
        abstract decimal BaseMaterialCount { get; }
    }
}
