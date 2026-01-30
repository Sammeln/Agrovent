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
using System.Drawing;
using Xarial.XCad.SolidWorks;
using Agrovent.ViewModels.Components;

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

        public ISwDocument3D SwDocument => mDocument;
        public string Name { get => Path.GetFileNameWithoutExtension(mDocument?.Title); }
        public string ConfigName { get => mConfiguration.Name; }
        public string PartNumber
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value.ToString();
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value = value;
        }
        public string Article
        {
            get
            {
                return AvaArticle != null ? AvaArticle.Article.ToString() : mProperties.AGR_TryGetProp(AGR_PropertyNames.Article).Value.ToString();
            }
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.Article).Value = value;
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
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.Version).Value = value;
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

            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.HashSum).Value = value;
        }
        public int CalcHash { get => CalculateComponentHash(); }
        public byte[] Preview
        {
            get
            {
                var app = AGR_ServiceContainer.GetService<ISwApplication>();

                string filePath = FilePath;
                string activeConfig = ConfigName;

                object com = app.Sw.GetPreviewBitmap(filePath, activeConfig);
                stdole.StdPicture pic = com as stdole.StdPicture;
                var bmp = Bitmap.FromHbitmap((IntPtr)pic.Handle);

                ImageConverter converter = new ImageConverter();
                return (byte[])converter.ConvertTo(bmp, typeof(byte[]));
            }
        }
        public string FilePath => mDocument.Path;

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

        private bool _isInDatabase = false;
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
        public int CalculateComponentHash()
        {
            // Вычисляем хеш на основе важных свойств
            unchecked
            {
                int hash = 17;

                if (this is AGR_PartComponentVM part)
                {
                    //hash = hash + (component.Name?.GetHashCode(StringComparison.Ordinal) ?? 0);
                    string str = string.Empty;
                    double sum = 0d;

                    var swPart = part.SwDocument as ISwPart;

                    foreach (var dim in swPart.Dimensions)
                    {
                        hash += dim.Value.GetHashCode();
                    }
                    foreach (var feat in swPart.Features)
                    {
                        hash += feat.Name.GetHashCode();
                    }
                }
                if (this is AGR_AssemblyComponentVM assembly)
                {
                    foreach (var item in assembly.GetChildComponents())
                    {
                        hash += item.Component.CalculateComponentHash();
                    }
                }

                return hash;


                //int hash = 17;
                //hash = hash * 23 + (component.Name?.GetHashCode(StringComparison.Ordinal) ?? 0);
                //hash = hash * 23 + (component.ConfigName?.GetHashCode(StringComparison.Ordinal) ?? 0);
                //hash = hash * 23 + (component.PartNumber?.GetHashCode(StringComparison.Ordinal) ?? 0);
                //hash = hash * 23 + component.ComponentType.GetHashCode();
                //hash = hash * 23 + component.AvaType.GetHashCode();
                //return hash;
            }
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
