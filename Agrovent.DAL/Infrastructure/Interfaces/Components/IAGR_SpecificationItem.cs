using Agrovent.DAL.Infrastructure.Enums;

namespace Agrovent.ViewModels.Specification
{
    public interface IAGR_SpecificationItem
    {
        public string Name { get; }
        public string ConfigName { get; }
        public string PartNumber { get; }
        public int Quantity { get; }

        public AGR_ComponentType_e ComponentType { get; }
        // Свойства для материалов (только для деталей)
        public string? MaterialName { get; }
        public decimal? MaterialCount { get; }

        // Свойства для краски
        public string? PaintName { get; }
        public decimal? PaintCount { get; }

    }
}