using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components.Base;

namespace Agrovent.Infrastructure.Interfaces.Specification
{
    public interface IAGR_SpecificationItem
    {
        public string Name { get; }
        public string ConfigName { get; }
        public string PartNumber { get; }
        public int Quantity { get; }
        public IAGR_BaseComponent Component { get; }

        public AGR_ComponentType_e ComponentType { get; }

        // Свойства для материалов (только для деталей)
        public string? MaterialName { get; }
        public decimal? MaterialCount { get; }

        // Свойства для краски
        public string? PaintName { get; }
        public decimal? PaintCount { get; }

    }
}