using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Base;
using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Extensions
{
    public static class AGR_IXPropertyRepoExtension
    {
        public static IXProperty AGR_TryGetProp(this IXPropertyRepository repo, string propertyName)
        {
            IXProperty xProperty = repo.GetOrPreCreate(propertyName);
            if (!xProperty.IsCommitted) xProperty.Commit(CancellationToken.None);
            return xProperty;
        }
    }
}
