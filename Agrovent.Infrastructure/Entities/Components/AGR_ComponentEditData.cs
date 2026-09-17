using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgroventInfrastructure.Enums;

namespace AgroventInfrastructure.Entities.Components
{
    public class AGR_ComponentEditData
    {
        public int ComponentVersionId { get; set; }
        public AGR_AvaType_e AvaType { get; set; }
        public int? AvaArticleArticle { get; set; }
        public int? MaterialAvaArticleId { get; set; }
        public string? MaterialName { get; set; }
        public int? PaintAvaArticleId { get; set; }
        public string? PaintName { get; set; }
    }
}
