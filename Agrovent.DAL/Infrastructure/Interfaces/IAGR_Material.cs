using Agrovent.DAL.Entities.Components;

namespace Agrovent.DAL.Infrastructure.Interfaces;
public interface IAGR_Material
{
    abstract string Name { get; set; }
    abstract string Article { get; set; }
    abstract string UOM { get; set; }
    abstract AvaArticleModel AvaModel { get; set; }
}