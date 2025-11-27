using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Interfaces.Properties
{
    internal interface AGR_IPartPropertiesCollection : AGR_IPropertiesCollection
    {
        abstract IXProperty Length { get; set; }
        abstract IXProperty Width { get; set; }
    }
}
