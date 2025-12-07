using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Data;

namespace Agrovent.DAL.Infrastructure.Interfaces.Properties
{
    public interface AGR_IPropertiesCollection
    {
        abstract ICollection<IXProperty> Properties { get; set; }

    }
}
