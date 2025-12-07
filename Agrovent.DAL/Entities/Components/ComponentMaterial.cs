using Agrovent.DAL.Entities.Base;


namespace Agrovent.DAL.Entities.Components
{
    public class ComponentMaterial : BaseEntity
    {
        // Связь с версией компонента
        public int ComponentVersionId { get; set; }
        public ComponentVersion ComponentVersion { get; set; }

        // Материал
        public string? BaseMaterial { get; set; }
        public decimal BaseMaterialCount { get; set; }

        // Покраска
        public string? Paint { get; set; }
        public decimal? PaintCount { get; set; }

        // Флаг, что материал заполнен
        public bool HasMaterial => !string.IsNullOrEmpty(BaseMaterial) && BaseMaterialCount > 0;
        public bool HasPaint => !string.IsNullOrEmpty(Paint) && PaintCount.HasValue;
    }
}
