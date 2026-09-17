using Agrovent.Infrastructure.Interfaces;

namespace AgroventInfrastructure.Interfaces.Components
{
    public interface IAGR_HasPaint
    {
        abstract IAGR_Material? Paint { get; set; }
        abstract decimal? PaintCount { get; set; }
    }
}

