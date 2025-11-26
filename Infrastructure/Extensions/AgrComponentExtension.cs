using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Agrovent.Infrastructure.Enums;
using SolidWorks.Interop.sldworks;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Infrastructure.Extensions
{
    public static class AgrComponentExtension
    {
        public static AvaType_e AvaType(this ISwDocument3D xDoc)
        {
            var avaType = Convert.ToInt32(xDoc.Configurations.Active.Properties[AGR_PropertyNames.AvaType].Value);
            if ((AvaType_e)avaType != null)
            {
                return (AvaType_e)avaType;
            }
            return AvaType_e.Component;
        }
        public static AGR_ComponentType_e ComponentType(this ISwDocument3D xDoc)
        {
            var avaType = xDoc.AvaType();
            if (avaType is AvaType_e.Purchased)
            {
                return AGR_ComponentType_e.Purchased;
            }

            else
            {
                if (xDoc is ISwPart part)
                {
                    if (part.Model.GetBendState() != 0)
                    {
                        return AGR_ComponentType_e.SheetMetallPart;
                    }
                    else return AGR_ComponentType_e.Part;

                }
                else if (xDoc is ISwAssembly)
                {
                    return AGR_ComponentType_e.Assembly;
                }
            }

            return AGR_ComponentType_e.NA;
            //Assembly,
            //Part,
            //SheetMetallPart,
            //Purchased
        }
    }
}
