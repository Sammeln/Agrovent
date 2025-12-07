using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Data;

namespace Agrovent.DAL.Infrastructure.Interfaces.Properties
{
    public interface AGR_IPartPropertiesCollection : AGR_IPropertiesCollection
    {
        abstract IXProperty Length { get; set; }
        abstract IXProperty Width { get; set; }
    }
}
