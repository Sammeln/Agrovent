// File: ViewModels/Specification/AGR_AssemblyEditItemVM.cs
using Agrovent.DAL;
using System.Collections.ObjectModel;
using System.Windows.Data;
using AgroventInfrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using AgroventInfrastructure.Entities.Components;
using Microsoft.Extensions.Logging;
using AgroventInfrastructure.Interfaces;

namespace Agrovent.ViewModels.Specification
{
    /// <summary>
    /// Строка состава сборки в режиме редактирования уже сохранённых в БД данных.
    /// В отличие от AGR_SpecificationItemVM не требует живого IAGR_BaseComponent.
    /// </summary>
    public class AGR_AssemblyEditItemVM : BaseViewModel
    {
        private readonly ComponentVersion _entity;
        public ComponentVersion Entity => _entity;

        public AGR_AssemblyEditItemVM(ComponentVersion entity, int quantity)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            Quantity = quantity;

            _componentAvaType = entity.AvaType;
            _avaArticle = entity.AvaArticle;

            if (entity.Material?.HasMaterial == true)
            {
                _baseMaterial = entity.Material.MaterialAvaArticle != null
                    ? new AGR_Material(entity.Material.MaterialAvaArticle)
                    : new AGR_Material(entity.Material.BaseMaterial);
            }

            if (entity.Material?.HasPaint == true && entity.Material.PaintAvaArticle != null)
            {
                _basePaint = new AGR_Paint(entity.Material.PaintAvaArticle);
            }
        }

        #region IsSelected
        private bool _IsSelected;
        public bool IsSelected
        {
            get => _IsSelected;
            set => Set(ref _IsSelected, value);
        }
        #endregion

        public string Name => _entity.Name;
        public string ConfigName => _entity.ConfigName;
        public string PartNumber => _entity.Component?.PartNumber ?? string.Empty;
        public int Quantity { get; }
        public byte[] Preview => _entity.PreviewImage;

        public AGR_ComponentType_e ComponentType => _entity.ComponentType;

        #region ComponentAvaType
        private AGR_AvaType_e _componentAvaType;
        public AGR_AvaType_e ComponentAvaType
        {
            get => _componentAvaType;
            set
            {
                if (Set(ref _componentAvaType, value))
                {
                    _entity.AvaType = value;
                    OnPropertyChanged(nameof(ComponentType));
                }
            }
        }
        #endregion

        #region AvaArticle (для покупных)
        private IAGR_AvaArticleModel? _avaArticle;
        public IAGR_AvaArticleModel? AvaArticle
        {
            get => _avaArticle;
            set
            {
                Set(ref _avaArticle, value);
                if (value is AvaArticleModel model)
                {
                    _entity.AvaArticleArticle = model.Article;
                }
                OnPropertyChanged(nameof(ArticleName));
                OnPropertyChanged(nameof(PartnumberOrArticle));
            }
        }
        public string ArticleName => AvaArticle?.Name ?? string.Empty;
        #endregion

        #region BaseMaterial
        private IAGR_Material? _baseMaterial;
        public IAGR_Material? BaseMaterial
        {
            get => _baseMaterial;
            set
            {
                Set(ref _baseMaterial, value);
                OnPropertyChanged(nameof(MaterialName));
                OnPropertyChanged(nameof(MaterialArticle));
            }
        }
        public string MaterialArticle =>
            ComponentType == AGR_ComponentType_e.Purchased
                ? string.Empty
                : BaseMaterial?.AvaModel != null ? $"Артикул№{BaseMaterial.AvaModel.Article}" : string.Empty;
        public string MaterialName =>
            ComponentType == AGR_ComponentType_e.Purchased
                ? string.Empty
                : BaseMaterial?.Name ?? _entity.Material?.BaseMaterial ?? string.Empty;
        #endregion

        #region BasePaint
        private IAGR_Material? _basePaint;
        public IAGR_Material? BasePaint
        {
            get => _basePaint;
            set
            {
                Set(ref _basePaint, value);
                OnPropertyChanged(nameof(PaintName));
                OnPropertyChanged(nameof(PaintArticle));
            }
        }
        public string PaintArticle =>
            ComponentType == AGR_ComponentType_e.Purchased
                ? string.Empty
                : BasePaint?.AvaModel != null ? $"Артикул№{BasePaint.AvaModel.Article}" : string.Empty;
        public string PaintName =>
            ComponentType == AGR_ComponentType_e.Purchased
                ? string.Empty
                : BasePaint?.Name ?? _entity.Material?.Paint ?? string.Empty;
        #endregion

        public string SheetMetalThickness =>
            ComponentType == AGR_ComponentType_e.SheetMetallPart
                ? _entity.Properties?.FirstOrDefault(p =>
                        p.Name.Contains("толщина", StringComparison.OrdinalIgnoreCase))?.Value ?? "N/A"
                : null;

        public string PartnumberOrArticle
        {
            get
            {
                if (ComponentType == AGR_ComponentType_e.Purchased)
                {
                    return AvaArticle != null ? $"Артикул№{AvaArticle.Article}" : string.Empty;
                }

                return AvaArticle != null
                    ? $"p/n {PartNumber}\nАртикул№{AvaArticle.Article}"
                    : $"p/n {PartNumber}";
            }
        }
    }
}