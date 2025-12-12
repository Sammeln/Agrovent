using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Specification;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;

namespace Agrovent.ViewModels.Specification
{
    public class AGR_SpecificationItemVM : BaseViewModel, IAGR_SpecificationItem
    {
        private readonly IAGR_BaseComponent _component;
        private readonly int _quantity;

        public IAGR_BaseComponent Component => _component;
        public string Name => _component.Name;
        public string ConfigName => _component.ConfigName;
        public string PartNumber => Component.ComponentType==AGR_ComponentType_e.Purchased ? "" : _component.PartNumber;
        public int Quantity => _quantity;

        public AGR_ComponentType_e ComponentType => _component.ComponentType;
        // Свойства для материалов (только для деталей)
        public string? MaterialName => (_component as AGR_PartComponentVM)?.BaseMaterial?.Name;
        public decimal? MaterialCount => (_component as AGR_PartComponentVM)?.BaseMaterialCount;

        // Свойства для краски
        public string? PaintName => (_component as AGR_PartComponentVM)?.Paint?.Name;
        public decimal? PaintCount => (_component as AGR_PartComponentVM)?.PaintCount;

        public AGR_SpecificationItemVM(IAGR_BaseComponent component, int quantity)
        {
            _component = component;
            _quantity = quantity;
        }
    }
}
