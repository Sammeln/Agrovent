using System.Collections.ObjectModel;
using System.Xml.Linq;
using Agrovent.DAL.Entities;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Base;
using Agrovent.ViewModels.Base;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Data;
using Xarial.XCad.SolidWorks.Documents;

public class AGR_BaseComponent : BaseViewModel, IAGR_BaseComponent
{
    private ISwDocument3D mDocument;
    private ISwConfiguration mConfiguration;
    private ISwCustomPropertiesCollection mProperties;


    public string Name { get => mProperties[AGR_PropertyNames.Name].Value.ToString(); set => mProperties[AGR_PropertyNames.Name].Value = value; }
    public string PartNumber { get => mProperties[AGR_PropertyNames.Partnumber].Value.ToString(); set => mProperties[AGR_PropertyNames.Partnumber].Value = value; }
    public int Version { get => (Int32)mProperties[AGR_PropertyNames.Version].Value; set => mProperties[AGR_PropertyNames.Version].Value = value; }
    public int HashSum { get => (Int32)mProperties[AGR_PropertyNames.HashSum].Value; set => mProperties[AGR_PropertyNames.HashSum].Value = value; }
    public AvaArticleModel AvaArticle { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public AGR_ComponentType_e Type { get => mDocument.ComponentType(); set => throw new NotImplementedException(); }
    public ICollection<IXProperty> PropertiesCollection { get => new ObservableCollection<IXProperty>(mProperties); set => throw new NotImplementedException(); }

    public AGR_BaseComponent(ISwDocument3D swDocument3D)
    {
        mDocument = swDocument3D;
        mConfiguration = mDocument.Configurations.Active;
        mProperties = mConfiguration.Properties;
    }
}