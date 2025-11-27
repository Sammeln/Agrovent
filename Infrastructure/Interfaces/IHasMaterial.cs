using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agrovent.Infrastructure.Interfaces
{
    internal interface IHasMaterial
    {
        abstract IAGR_Material BaseMaterial { get; set; }
        abstract decimal BaseMaterialCount { get; set; }
    }
}
