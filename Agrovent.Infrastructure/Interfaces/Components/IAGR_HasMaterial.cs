namespace Agrovent.Infrastructure.Interfaces.Components
{
    public interface IAGR_HasMaterial
    {
        abstract IAGR_Material BaseMaterial { get; set; }
        abstract decimal BaseMaterialCount { get; set; }
    }
}
