using Agrovent.DAL.Entities.Components;
using Agrovent.DAL.Infrastructure.Enums;
using Agrovent.DAL.Infrastructure.Interfaces.Base;
using Agrovent.DAL.Infrastructure.Interfaces.Properties;

namespace Agrovent.DAL.Infrastructure.Interfaces.Components
{
    public interface IAGR_BaseComponent : IAGR_BaseObject, IAGR_PageView
    {
        abstract string Name { get; }
        abstract string ConfigName { get; }
        abstract string PartNumber { get; set; }
        abstract int Version { get; set; }
        abstract int HashSum { get; set; }
        abstract AvaArticleModel AvaArticle { get; set; }
        abstract AGR_ComponentType_e ComponentType { get; set; }
        abstract AvaType_e AvaType { get; set; }
        abstract AGR_IPropertiesCollection PropertiesCollection { get; set; }

    }
}
