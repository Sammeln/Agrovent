using System.Drawing;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Specification;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.SolidWorks;

namespace Agrovent.ViewModels.Specification
{
    public class AGR_SpecificationItemVM : BaseViewModel, IAGR_SpecificationItem
    {
        private readonly IAGR_BaseComponent _component;
        public IAGR_BaseComponent Component => _component;
        private readonly int _quantity;

        public string Name => _component.Name;
        public string ConfigName => _component.ConfigName;
        public string PartNumber => _component.PartNumber;
        public int Quantity => _quantity;
        public AGR_ComponentType_e ComponentType => _component.ComponentType;

        // Общие свойства для деталей
        public string MaterialName
        {
            get
            {
                if (_component is AGR_PartComponentVM part)
                    return part.BaseMaterial?.Name;
                return null;
            }
        }

        public decimal? MaterialCount
        {
            get
            {
                if (_component is AGR_PartComponentVM part)
                    return part.BaseMaterialCount;
                return null;
            }
        }

        public string PaintName
        {
            get
            {
                if (_component is AGR_PartComponentVM part)
                    return part.Paint?.Name;
                if (_component is AGR_AssemblyComponentVM assembly)
                {
                    // Для сборки можно попробовать получить покраску
                    // Или вернуть null, если не применимо
                    return null;
                }
                return null;
            }
        }

        // Свойство для толщины (только для листовых деталей)
        public string SheetMetalThickness
        {
            get
            {
                if (_component.ComponentType == AGR_ComponentType_e.SheetMetallPart)
                {
                    // Получаем свойство толщины из коллекции свойств
                    var thicknessProp = _component.PropertiesCollection?.Properties?
                        .FirstOrDefault(p => p.Name.Contains("толщина", StringComparison.OrdinalIgnoreCase) ||
                                            p.Name.Contains("толщин", StringComparison.OrdinalIgnoreCase));

                    return thicknessProp?.Value?.ToString() ?? "N/A";
                }
                return null;
            }
        }

        // Свойство для артикула (только для покупных)
        public string Article
        {
            get
            {
                if (_component.ComponentType == AGR_ComponentType_e.Purchased)
                {
                    return _component.AvaArticle?.Article.ToString() ?? "N/A";
                }
                return null;
            }
        }

        // Свойства для форматированного отображения
        public string MaterialInfo => MaterialName != null ? $"{MaterialName} ({MaterialCount:F2})" : null;
        public string QuantityString => Quantity.ToString();

        public byte[] Preview
        {
            get
            {
                var app = AGR_ServiceContainer.GetService<ISwApplication>();

                string filePath = Component.FilePath;
                string activeConfig = Component.ConfigName;

                object com = app.Sw.GetPreviewBitmap(filePath, activeConfig);
                stdole.StdPicture pic = com as stdole.StdPicture;
                var bmp = Bitmap.FromHbitmap((IntPtr)pic.Handle);

                ImageConverter converter = new ImageConverter();
                return (byte[])converter.ConvertTo(bmp, typeof(byte[]));
            }
        }
        public AGR_SpecificationItemVM(IAGR_BaseComponent component, int quantity)
        {
            _component = component;
            _quantity = quantity;
        }
    }
}
