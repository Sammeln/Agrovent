using Agrovent.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agrovent.DAL.Entities.Components
{
    public class AvaArticleModel : IAGR_AvaArticleModel
    {
        public int Article { get; set; }
        public string? PartNumber { get; set; }
        public string Name { get; set; }
        public decimal? Count { get; set; }
        public string UOM { get; set; }
        public string Type { get; set; }
        public string Folder { get; set; }
        public string Brand { get; set; }
        public string Company { get; set; }

    }
}
  