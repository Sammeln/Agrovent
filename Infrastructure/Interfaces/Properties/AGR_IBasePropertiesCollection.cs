using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Interfaces.Properties
{
    internal interface AGR_IBasePropertiesCollection : AGR_IPropertiesCollection
    {
        abstract IXProperty Volume { get; set; }
        abstract IXProperty Mass { get; set; }
        abstract IXProperty SurfaceArea { get; set; }
    }
}
