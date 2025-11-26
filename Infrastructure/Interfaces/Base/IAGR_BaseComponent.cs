using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.DAL.Entities;
using Agrovent.Infrastructure.Enums;
using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Interfaces.Base
{
    public interface IAGR_BaseComponent
    {
        abstract string Name { get; set; }
        abstract AvaArticleModel AvaArticle { get; set; }
        abstract string PartNumber { get; set; }
        abstract ComponentType_e Type { get; set; }
        abstract int Version { get; set; }
        abstract ICollection<IXProperty> PropertiesCollection { get; set; }
        abstract int HashSum { get; set; }

    }
}
