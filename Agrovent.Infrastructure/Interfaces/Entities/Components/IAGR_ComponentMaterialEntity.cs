using Agrovent.Infrastructure.Interfaces;

namespace AgroventInfrastructure.Interfaces.Entities.Components
{
    public interface IAGR_ComponentMaterialEntity : IAGR_DateStampEntity
    {
        string? BaseMaterial { get; set; }
        decimal BaseMaterialCount { get; set; }
        IAGR_ComponentVersionEntity ComponentVersion { get; set; }
        int ComponentVersionId { get; set; }
        bool HasMaterial { get; }
        bool HasPaint { get; }
        IAGR_AvaArticleModel? MaterialAvaArticle { get; set; }
        int? MaterialAvaArticleID { get; set; }
        string? Paint { get; set; }
        IAGR_AvaArticleModel? PaintAvaArticle { get; set; }
        int? PaintAvaArticleID { get; set; }
        decimal? PaintCount { get; set; }
    }
}