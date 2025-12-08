using System.Collections.Generic;
using Agrovent.Infrastructure.Interfaces.Specification;

namespace Agrovent.Infrastructure.Interfaces.Components
{
    public interface IAGR_Assembly
    {
        IEnumerable<IAGR_SpecificationItem> GetChildComponents();
    }
}