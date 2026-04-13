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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell.Interop;
using Xarial.XCad.Base.Attributes;
using Agrovent.Properties;

namespace Agrovent.ViewModels.Base
{
    public class AGR_BaseComponent : BaseViewModel, IAGR_BaseComponent
    {
        #region FIELDS
        internal ISwDocument3D? mDocument;
        internal ISwConfiguration? mConfiguration;
        internal ISwCustomPropertiesCollection? mProperties;
        #endregion

        #region PROPS

        public ISwDocument3D SwDocument => mDocument;
        public string Name { get => Path.GetFileNameWithoutExtension(mDocument?.Title); }
        public string ConfigName { get => mConfiguration?.Name ?? ""; }
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
        public bool IsLoaded  { get; set; }

        #region Preview
        private byte[]? _Preview;
        public byte[]? Preview
        {
            get
            {
                if (_Preview == null)
                {
                    _Preview = ComputePreviewImageBytes();
                }
                return _Preview;
            }
            // Устанавливать можно только извне, если нужно переопределить
            protected set => _Preview = value;
        } 
        #endregion
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
        #endregion

        #region IsInDatabase
        private AGR_ComponentDatabaseState_e _isInDatabase = AGR_ComponentDatabaseState_e.NotLoaded;
        public AGR_ComponentDatabaseState_e IsInDatabase
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
                        PropertiesCollection.UpdateProperties();
                        OnPropertyChanged(nameof(PropertiesCollection));
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
                OnPropertyChanged(nameof(ComponentType));
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

                    foreach (var feat in part.SwDocument.Features)
                    {
                        
                    }

                    string str = string.Empty;
                    double sum = 0d;

                    var swPart = part.SwDocument as ISwPart;

                    var dimensions = swPart.Dimensions.ToList();

                    if (dimensions != null && dimensions?.Count > 0)
                    {
                        foreach (var dim in swPart.Dimensions)
                        {
                            hash += dim.Value.GetHashCode();
                        } 
                    }
                    foreach (var feat in swPart.Features)
                    {
                        str += feat.Name;
                    }
                    hash += HashString(str);
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
        protected virtual byte[]? ComputePreviewImageBytes()
        {
            try
            {
                var app = AGR_ServiceContainer.GetService<ISwApplication>();

                string? filePath = FilePath;
                string activeConfig = ConfigName ?? "Default"; // Используем "Default", если ConfigName null

                if (string.IsNullOrEmpty(filePath))
                {
                    //_logger?.LogWarning("ComputePreviewImageBytes: FilePath is null or empty.");
                    return null;
                }

                // Вызов GetPreviewBitmap из UI-потока
                object? com = app.Sw.GetPreviewBitmap(filePath, activeConfig);
                if (com == null)
                {
                    return Resources.NonePreview;
                    //_logger?.LogWarning($"ComputePreviewImageBytes: GetPreviewBitmap returned null for {filePath}, config: {activeConfig}");
                    //return null;
                }

                stdole.StdPicture? pic = com as stdole.StdPicture;
                if (pic == null)
                {
                    //_logger?.LogWarning($"ComputePreviewImageBytes: GetPreviewBitmap returned unexpected type: {com.GetType()}");
                    return null;
                }

                var bmp = Bitmap.FromHbitmap((IntPtr)pic.Handle);
                // Освобождаем handle, так как FromHbitmap создает копию
                // Не обязательно вызывать DeleteObject(pic.Handle) здесь, так как FromHbitmap уже скопировал данные


                ImageConverter converter = new ImageConverter();
                return (byte[])converter.ConvertTo(bmp, typeof(byte[]));

                //using var ms = new MemoryStream();
                //bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); // Или Jpeg, в зависимости от ваших предпочтений
                //bmp.Dispose(); // Уничтожаем Bitmap
                //return ms.ToArray();
            }
            catch (COMException comEx)
            {
                //_logger?.LogError(comEx, $"COM ошибка при получении превью для {FilePath}: {comEx.Message}");
                return null; // Возвращаем null в случае ошибки
            }
            catch (Exception ex)
            {
                //_logger?.LogError(ex, $"Ошибка при получении превью для {FilePath}: {ex.Message}");
                return null; // Возвращаем null в случае ошибки
            }
        }
        public void PrecomputePreview()
        {
            // Просто обращаемся к свойству, чтобы оно вычислилось в текущем потоке (ожидается UI-поток)
            _ = Preview;
        }
        public int HashString(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                {
                    hash = hash * 31 + c;
                }
                return hash;
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
