using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.DAL.Entities;
using Agrovent.DAL.Infrastructure.Enums;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Properties;
using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Interfaces.Components
{
    public interface IAGR_BaseComponent
    {
        abstract string Name { get; set; }
        abstract string PartNumber { get; set; }
        abstract int Version { get; set; }
        abstract int HashSum { get; set; }
        abstract AvaArticleModel AvaArticle { get; set; }
        abstract AGR_ComponentType_e ComponentType { get; set; }
        abstract AvaType_e AvaType { get; set; }
        abstract AGR_IPropertiesCollection PropertiesCollection { get; set; }

    }
}
