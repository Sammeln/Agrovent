using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Properties;

namespace Agrovent.Infrastructure.Interfaces.Components.Base
{
    public interface IAGR_BaseComponent : IAGR_BaseObject, IAGR_PageView
    {
        abstract string Name { get; }
        abstract string ConfigName { get; }
        abstract string PartNumber { get; set; }
        abstract int Version { get; set; }
        abstract int HashSum { get; set; }
        abstract IAGR_AvaArticleModel AvaArticle { get; set; }
        abstract AGR_ComponentType_e ComponentType { get; set; }
        abstract AGR_AvaType_e AvaType { get; set; }
        abstract IAGR_PropertiesCollection PropertiesCollection { get; set; }
        abstract bool IsInDatabase { get; set; }

    }
}
