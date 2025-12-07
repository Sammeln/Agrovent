using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agrovent.DAL.Infrastructure.Interfaces
{
    public interface IHasPaint
    {
        abstract IAGR_Material? Paint { get; set; }
        abstract decimal? PaintCount { get; set; }
    }
}
