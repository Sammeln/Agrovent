using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;

namespace Agrovent.ViewModels.Specification
{
    public class SpecificationItemVM : BaseViewModel
    {
        private readonly IAGR_BaseComponent _component;
        private readonly int _quantity;

        public string Name => _component.Name;
        public string ConfigName => _component.ConfigName;
        public string PartNumber => _component.PartNumber;
        public int Quantity => _quantity;

        public AGR_ComponentType_e ComponentType => _component.ComponentType;
        // Свойства для материалов (только для деталей)
        public string? MaterialName => (_component as AGR_PartComponentVM)?.BaseMaterial?.Name;
        public decimal? MaterialCount => (_component as AGR_PartComponentVM)?.BaseMaterialCount;

        // Свойства для краски
        public string? PaintName => (_component as AGR_PartComponentVM)?.Paint?.Name;
        public decimal? PaintCount => (_component as AGR_PartComponentVM)?.PaintCount;

        public SpecificationItemVM(IAGR_BaseComponent component, int quantity)
        {
            _component = component;
            _quantity = quantity;
        }
    }
}
