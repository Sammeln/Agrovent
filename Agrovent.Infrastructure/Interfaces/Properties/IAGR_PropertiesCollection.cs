using System.Collections.Generic;
using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Interfaces.Properties
{
    public interface IAGR_PropertiesCollection
    {
        abstract ICollection<IXProperty> Properties { get; set; }

        abstract void UpdateProperties();

    }
}
