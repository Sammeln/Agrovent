using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agrovent.DAL.Infrastructure.Interfaces
{
    public interface IBaseEntity
    {
        abstract int Id { get; set; }
    }
}
