namespace Agrovent.Infrastructure.Interfaces.Components
{
    public interface IAGR_HasPaint
    {
        abstract IAGR_Material? Paint { get; set; }
        abstract decimal? PaintCount { get; set; }
    }
}

