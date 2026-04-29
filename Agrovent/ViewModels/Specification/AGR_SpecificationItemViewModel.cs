using System.Drawing;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Specification;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.SolidWorks;
using System.Diagnostics;
using System.Windows.Input;
using AGR_PropManager.Infrastructure.Commands;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Interfaces;

namespace Agrovent.ViewModels.Specification
{
    [DebuggerDisplay("{" + nameof(Name) + "} - {" + nameof(Quantity) + "}")]
    public class AGR_SpecificationItemVM : BaseViewModel, IAGR_SpecificationItem
    {
        private readonly IAGR_BaseComponent _component;
        public IAGR_BaseComponent Component => _component;
        private readonly int _quantity;
        public AGR_SpecificationItemVM(IAGR_BaseComponent component, int quantity)
        {
            _component = component;
            _quantity = quantity;

            InitItem();
        }

        private void InitItem()
        {
            if (_component.ComponentType != AGR_ComponentType_e.Purchased)
            {
                if (_component is AGR_PartComponentVM part)
                {
                    MaterialName = part.BaseMaterial?.Name;
                    //PaintName = part.Paint?.Name;
                }
                if (_component is AGR_AssemblyComponentVM assembly)
                {
                    //PaintName = assembly.Paint?.Name;
                }
            }
            AvaArticle = _component.AvaArticle;
            ComponentAvaType = _component.AvaType;
        }

        #region Property - IsSelected
        private bool _IsSelected = false;
        public bool IsSelected
        {
            get => _IsSelected;
            set => Set(ref _IsSelected, value);
        }
        #endregion 
        public string Name => _component.Name;
        public string ConfigName => _component.ConfigName;
        public string PartNumber => Component.AvaType == AGR_AvaType_e.Purchased ? Component.AvaArticle?.Article.ToString() : _component.PartNumber;
        public int Quantity => _quantity;
        public AGR_ComponentType_e ComponentType
        {
            get
            {
                return Component.ComponentType;
            }
        }

        #region Property - ComponentAvaType
        private AGR_AvaType_e _ComponentAvaType;
        public AGR_AvaType_e ComponentAvaType
        {
            get => _ComponentAvaType;
            set
            {
                Set(ref _ComponentAvaType, value);
                Component.AvaType = value;

                OnPropertyChanged(nameof(ComponentType));
            }
        }
        #endregion 

        #region Property - IAGR_AvaArticleModel AvaArticle
        private IAGR_AvaArticleModel? _AvaArticle;
        public IAGR_AvaArticleModel? AvaArticle
        {
            get => _AvaArticle;
            set
            {
                Set(ref _AvaArticle, value);
                _component.AvaArticle = value;
                OnPropertyChanged(nameof(Article));
                OnPropertyChanged(nameof(PartnumberOrArticle));
            }
        }
        #endregion 

        // Общие свойства для деталей


        #region Property - MaterialAvaModel
        private AvaArticleModel? _MaterialAvaModel;
        public AvaArticleModel? MaterialAvaModel
        {
            get => _MaterialAvaModel;
            set
            {
                Set(ref _MaterialAvaModel, value);
                AGR_Material newMaterial = new AGR_Material(value);
                (Component as AGR_PartComponentVM).BaseMaterial = newMaterial;
                MaterialName = newMaterial.Name;
            }
        }
        #endregion 

        private string _MaterialName;
        public string MaterialName
        {
            get => _MaterialName;
            set
            {
                Set(ref _MaterialName, value);
            }
        }

        public decimal? MaterialCount
        {
            get
            {
                if (_component is AGR_PartComponentVM part && _component.ComponentType != AGR_ComponentType_e.Purchased)
                {
                    return part.BaseMaterialCount;
                }
                return null;
            }
        }


        #region Property - PaintAvaModel
        private AvaArticleModel? _PaintAvaModel;
        public AvaArticleModel? PaintAvaModel
        {
            get => _PaintAvaModel;
            set
            {
                Set(ref _PaintAvaModel, value);
                if (value != null)
                {
                    AGR_Material newMaterial = new AGR_Material(value);
                    if (Component is AGR_PartComponentVM partComp)
                    {
                        partComp.Paint = newMaterial;
                    }
                    if (Component is AGR_AssemblyComponentVM assemComp)
                    {
                        assemComp.Paint = newMaterial;
                    }
                }
                else
                {
                    (Component as AGR_PartComponentVM).Paint = null;
                }
                OnPropertyChanged(nameof(PaintName));
            }
        }
        #endregion 
        private string _PaintName;
        public string PaintName
        {
            get
            {
                if (PaintAvaModel is null)
                {
                    Set(ref _PaintName, "Без покрытия");
                    return _PaintName;
                }
                else
                {
                    Set(ref _PaintName, PaintAvaModel.Name);
                    return _PaintName;
                }
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
                    return AvaArticle?.Article.ToString() ?? "";
                }
                return null;
            }
        }


        #region Property - PartnumberOrArticle
        public string PartnumberOrArticle
        {
            get
            {
                if (_component.ComponentType == AGR_ComponentType_e.Purchased)
                {
                    return Article;
                }
                return PartNumber;
            }
        }
        #endregion 

        // Свойства для форматированного отображения
        public string MaterialInfo => MaterialName != null ? $"{MaterialName} ({MaterialCount:F2})" : null;
        public string QuantityString => Quantity.ToString();

        public int HashSum => Component.CalculateComponentHash();

        public byte[] Preview => Component.Preview;





    }
}
