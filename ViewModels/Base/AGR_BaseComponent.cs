using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Linq;
using Agrovent.DAL.Entities;
using Agrovent.DAL.Infrastructure.Enums;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Data;
using Xarial.XCad.SolidWorks.Documents;

public class AGR_BaseComponent : BaseViewModel, IAGR_BaseComponent
{
    internal ISwDocument3D mDocument;
    internal ISwConfiguration mConfiguration;
    internal ISwCustomPropertiesCollection mProperties;


    public string Name { get => Path.GetFileNameWithoutExtension(mDocument.Title); }
    public string ConfigName { get => mConfiguration.Name; }
    public string PartNumber 
    {
        get => mProperties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value.ToString(); 
        set => mProperties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value = value; 
    }
    public int Version
    {
        get;
        set;
        //get => Convert.ToInt32(mProperties.AGR_TryGetProp(AGR_PropertyNames.Version).Value);
        //set => mProperties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value = value;
    }
    //public int HashSum { get => Convert.ToInt32(mProperties[AGR_PropertyNames.HashSum].Value); set => mProperties[AGR_PropertyNames.HashSum].Value = value; }
    public int HashSum
    {
        get
        {
            var value = mProperties.AGR_TryGetProp(AGR_PropertyNames.HashSum).Value;
            if (!string.IsNullOrEmpty(value.ToString()))
            {
                return Convert.ToInt32(value);
            }
            return 0;
        }

        set => mProperties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value = value;
    }
    public AvaArticleModel AvaArticle { get; set; }

    public AGR_ComponentType_e ComponentType
    {
        get => mDocument.ComponentType();
        set
        {
            OnPropertyChanged(nameof(ComponentType));
            switch (value)
            {
                case AGR_ComponentType_e.Assembly:
                    PropertiesCollection = new AGR_BasePropertiesCollection(mDocument);
                    break;
                case AGR_ComponentType_e.Part:
                    PropertiesCollection = new AGR_PartPropertiesCollection(mDocument);
                    break;
                case AGR_ComponentType_e.SheetMetallPart:
                    PropertiesCollection = new AGR_SheetPartPropertiesCollection(mDocument);
                    break;
                case AGR_ComponentType_e.Purchased:
                    PropertiesCollection?.Properties.Clear();
                    break;
                case AGR_ComponentType_e.NA:
                    PropertiesCollection = new AGR_BasePropertiesCollection(mDocument);
                    break;
                default:
                    break;
            }
        }
    }
    public AvaType_e AvaType
    {
        get
        {
            var val = mProperties.AGR_TryGetProp(AGR_PropertyNames.AvaType).Value;
            if (!string.IsNullOrEmpty(val.ToString()))
            {
                return (AvaType_e)Convert.ToInt32(val);
            }
            return AvaType_e.Component;
        }

        set
        {
            mProperties.AGR_TryGetProp(AGR_PropertyNames.AvaType).Value = (int)value;

            if (value == AvaType_e.Purchased)
            {
                ComponentType = AGR_ComponentType_e.Purchased;
            }
            else
            {
                ComponentType = mDocument.ComponentType();
            }
        }
    }

    private AGR_IPropertiesCollection? _PropertiesCollection;
    public AGR_IPropertiesCollection? PropertiesCollection 
    {
        get => _PropertiesCollection;
        set => Set(ref _PropertiesCollection, value);
    }

    public AGR_BaseComponent(ISwDocument3D swDocument3D)
    {
        mDocument = swDocument3D;
        mConfiguration = mDocument.Configurations.Active;
        mProperties = mConfiguration.Properties;

        ComponentType = mDocument.ComponentType();
        
    }
}
