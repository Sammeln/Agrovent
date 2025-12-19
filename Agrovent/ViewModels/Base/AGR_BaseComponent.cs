using System.IO;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.ViewModels.Properties;
using Xarial.XCad.SolidWorks.Data;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.Infrastructure.Interfaces;

namespace Agrovent.ViewModels.Base
{
    public class AGR_BaseComponent : BaseViewModel, IAGR_BaseComponent
    {
        #region FIELDS
        internal ISwDocument3D mDocument;
        internal ISwConfiguration mConfiguration;
        internal ISwCustomPropertiesCollection mProperties; 
        #endregion

        #region PROPS
        public string Name { get => Path.GetFileNameWithoutExtension(mDocument.Title); }
        public string ConfigName { get => mConfiguration.Name; }
        public string PartNumber
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value.ToString();
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value = value;
        }
        public int Version
        {
            get
            {
                var propValue = mProperties.AGR_TryGetProp(AGR_PropertyNames.Version).Value;
                try
                {
                    return Convert.ToInt32(propValue);
                }
                catch (Exception)
                {

                    return 0;
                }
            }
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value = value;
        }
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

        #region Property - IAGR_AvaArticleModel _AvaArticle
        private IAGR_AvaArticleModel _AvaArticle;
        public IAGR_AvaArticleModel AvaArticle
        {
            get => _AvaArticle;
            set
            {
                Set(ref _AvaArticle, value);
            }
        }

        public bool HasAvaArticle => _AvaArticle != null;

        private bool _isInDatabase;
        public bool IsInDatabase
        {
            get => _isInDatabase;
            set => Set(ref _isInDatabase, value);
        }

        #endregion 
        public IAGR_PropertiesCollection PropertiesCollection { get; set; }
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
        public AGR_AvaType_e AvaType
        {
            get
            {
                var val = mProperties.AGR_TryGetProp(AGR_PropertyNames.AvaType).Value;
                if (!string.IsNullOrEmpty(val.ToString()))
                {
                    return (AGR_AvaType_e)Convert.ToInt32(val);
                }
                return AGR_AvaType_e.Component;
            }

            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.AvaType).Value = (int)value;

                if (value == AGR_AvaType_e.Purchased)
                {
                    ComponentType = AGR_ComponentType_e.Purchased;
                }
                else
                {
                    ComponentType = mDocument.ComponentType();
                }
            }
        } 
        #endregion

        #region METHODS
        public int GetHashSum()
        {
            int hash = 17;
            if (mDocument is ISwPart part)
            {
                foreach (var feat in part.Features)
                {
                    hash += feat.Name.GetHashCode();
                }
                foreach (var dim in part.Dimensions)
                {
                    hash += dim.Value.GetHashCode();
                }
            }
            return hash;
        }
        #endregion

        #region CTOR

        public AGR_BaseComponent(ISwDocument3D swDocument3D)
        {
            mDocument = swDocument3D;
            mConfiguration = mDocument.Configurations.Active;
            mProperties = mConfiguration.Properties;

            ComponentType = mDocument.ComponentType();

        } 
        #endregion

    }
}
