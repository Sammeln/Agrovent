using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Data;

namespace Agrovent.DAL.Infrastructure.Interfaces.Properties
{
    public interface AGR_IBasePropertiesCollection : AGR_IPropertiesCollection
    {
        abstract IXProperty Volume { get; set; }
        abstract IXProperty Mass { get; set; }
        abstract IXProperty SurfaceArea { get; set; }
    }
}
