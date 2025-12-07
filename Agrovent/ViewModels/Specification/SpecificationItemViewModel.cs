using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.DAL.Infrastructure.Enums;
using Agrovent.DAL.Infrastructure.Interfaces.Components;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;

namespace Agrovent.ViewModels.Specification
{
    public class SpecificationItemVM : BaseViewModel, IAGR_SpecificationItem
    {
        private readonly IAGR_BaseComponent _component;
        private readonly int _quantity;

        public IAGR_BaseComponent Component => _component;
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
