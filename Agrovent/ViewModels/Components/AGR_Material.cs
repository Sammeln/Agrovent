using Agrovent.DAL;
using AgroventInfrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Base;
using AgroventInfrastructure.Entities.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shell.Interop;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Components
{
    public class AGR_Material : BaseViewModel, IAGR_Material
    {
        private readonly ILogger<AGR_Material>? _logger; // Добавим логгер (опционально)

        #region CTOR
        public AGR_Material(ISwDocument3D doc3D, ILogger<AGR_Material>? logger = null)
        {
            _logger = logger;
            // Инициализация Name из документа
            Name = doc3D.Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.Material).Value?.ToString() ?? string.Empty;
            // Article и UOM остаются пустыми или null до тех пор, пока AvaModel не будет установлен
        }

        public AGR_Material(AvaArticleModel avaArticle, ILogger<AGR_Material>? logger = null)
        {
            _logger = logger;
            AvaModel = avaArticle;
        }


        public AGR_Material(string materialName)
        {
            Name = materialName;
        }
        #endregion

        #region PROPS
        #region Name
        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                Set(ref _name, value);
            }
        }
        #endregion

        #region Article
        private string _article = "";
        public string Article
        {
            get => _article;
            set => Set(ref _article, value);
        }
        #endregion

        #region UOM
        private string _uom = "";
        public string UOM
        {
            get => _uom;
            set => Set(ref _uom, value);
        }
        #endregion

        #region AvaModel
        private AvaArticleModel? _avaModel;
        public AvaArticleModel? AvaModel
        {
            get => _avaModel;
            set
            {
                if (Set(ref _avaModel, value))
                {
                    // Обновляем Name, Article, UOM из AvaModel
                    if (value != null)
                    {
                        Name = value.Name ?? string.Empty;
                        Article = value.Article.ToString() ?? string.Empty; // Предполагаем, что Article может быть int
                        UOM = value.MainUOM ?? string.Empty; // Или UOM, в зависимости от структуры AvaArticleModel
                    }
                    else
                    {
                        // Если AvaModel сброшен, сбрасываем и производные свойства
                        Name = string.Empty;
                        Article = string.Empty;
                        UOM = string.Empty;
                    }
                }
            }
        }
        #endregion
        #endregion
    }

}
