using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.Infrastructure.Interfaces;

namespace Agrovent.DAL.Infrastructure.Interfaces
{
    public interface IAGR_MatchModel
    {
        public string PartName { get; set; }
        public string ConfigName { get; set; }
        public int ArticleModelId { get; set; }
        [ForeignKey("ArticleModelId")]
        public IAGR_AvaArticleModel ArticleModel { get; set; }
    }
}
