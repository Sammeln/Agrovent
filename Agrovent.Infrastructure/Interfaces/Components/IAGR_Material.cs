
namespace Agrovent.Infrastructure.Interfaces
{
    public interface IAGR_Material
    {
        abstract string Name { get; set; }
        abstract string Article { get; set; }
        abstract string UOM { get; set; }
        abstract IAGR_AvaArticleModel AvaModel { get; set; }
    }
}