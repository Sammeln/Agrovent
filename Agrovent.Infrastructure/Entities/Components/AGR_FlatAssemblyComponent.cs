using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroventInfrastructure.Entities.Components
{
    /// <summary>
    /// Итоговое количество одной уникальной версии компонента во всей сборке,
    /// с учётом того, что компонент может входить в несколько разных подсборок,
    /// а сами подсборки могут повторяться на верхних уровнях.
    /// </summary>
    public class AGR_FlatAssemblyComponent
    {
        public ComponentVersion Entity { get; set; } = null!;
        public int TotalQuantity { get; set; }
    }
}
