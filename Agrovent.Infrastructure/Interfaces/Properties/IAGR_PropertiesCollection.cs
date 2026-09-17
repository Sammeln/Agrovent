using System.Collections.Generic;
using Xarial.XCad.Data;

namespace AgroventInfrastructure.Interfaces.Properties
{
    public interface IAGR_PropertiesCollection
    {
        abstract ICollection<IXProperty> Properties { get; set; }

        abstract void UpdateProperties();

    }
}
