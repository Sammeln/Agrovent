using System.Collections.Generic;
using AgroventInfrastructure.Interfaces.Components.Base;
using AgroventInfrastructure.Interfaces.Properties;
using AgroventInfrastructure.Interfaces.Specification;

namespace AgroventInfrastructure.Interfaces.Components
{
    public interface IAGR_Assembly : IAGR_BaseComponent
    {
        IEnumerable<IAGR_SpecificationItem> GetChildComponents();
        //abstract IAGR_BasePropertiesCollection PropertiesCollection { get; set; }
    }
}