using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.Base;
using Agrovent.Infrastructure.Interfaces;
using AgroventInfrastructure.Interfaces.Entities.Components;


namespace Agrovent.DAL.Entities.Components
{
    public class ComponentMaterial : DateStampEntity
    {
        // Связь с версией компонента
        public int ComponentVersionId { get; set; }
        [ForeignKey("ComponentVersionId")]
        public ComponentVersion ComponentVersion { get; set; }

        // Материал
        public string? BaseMaterial { get; set; }
        public decimal BaseMaterialCount { get; set; }
        public int? MaterialAvaArticleID { get; set; }
        [ForeignKey("MaterialAvaArticleID")]
        public AvaArticleModel? MaterialAvaArticle { get; set; }

        // Покраска
        public string? Paint { get; set; }
        public decimal? PaintCount { get; set; }
        public int? PaintAvaArticleID { get; set; }
        [ForeignKey("PaintAvaArticleID")]
        public AvaArticleModel? PaintAvaArticle { get; set; }

        // Флаг, что материал заполнен
        public bool HasMaterial => !string.IsNullOrEmpty(BaseMaterial) && BaseMaterialCount > 0;
        public bool HasPaint => !string.IsNullOrEmpty(Paint) && PaintCount.HasValue;
    }
}
