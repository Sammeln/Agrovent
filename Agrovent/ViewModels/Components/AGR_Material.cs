using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Shell.Interop;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Components
{
    public class AGR_Material : BaseViewModel, IAGR_Material
    {
        private string _name;

        public string Name
        {
            get => _name;
            set
            {
                Set(ref _name, value);
            }
        }
        private string _article;

        public string Article 
        { 
            get => _article; 
            set => _article = value; 
        }
        public string UOM { get; set; }

        private IAGR_AvaArticleModel avaModel;
        public IAGR_AvaArticleModel AvaModel
        {
            get => avaModel;
            set
            {
                if(Set(ref avaModel, value))
                {
                    Name = AvaModel.Name;
                    Article = AvaModel.Article.ToString();
                    UOM = AvaModel.MainUOM;
                }

            }

        }

        private void TryGetArticle()
        {
            try
            {

                using (var db = new DAL.DataContext())
                {
                    var material = db.AvaArticles.FirstOrDefault(x => x.Name == Name);
                    if (material != null)
                    {
                        AvaModel = material;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public AGR_Material(ISwDocument3D doc3D)
        {
            Name = doc3D.Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.Material).Value.ToString();
            TryGetArticle();
        }

        public AGR_Material(AvaArticleModel avaArticle)
        {
            AvaModel = avaArticle;
        }
    }
}
