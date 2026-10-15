using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroventInfrastructure.Enums
{
    public enum AGR_EditState_e
    {
        Unknown,        // не загружено
        Free,           // в БД есть, замка нет
        EditingByMe,
        EditingByOther
    }
}
