using System.Collections.Generic;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.Infrastructure.Interfaces.Specification;

namespace Agrovent.Infrastructure.Interfaces.Components
{
    public interface IAGR_Assembly : IAGR_BaseComponent
    {
        IEnumerable<IAGR_SpecificationItem> GetChildComponents();
        //abstract IAGR_BasePropertiesCollection PropertiesCollection { get; set; }
    }
}