using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.ViewModels.Components;
using Xarial.XCad.Data;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Infrastructure.Extensions
{
    public static class AGR_ComponentExtension
    {
        public static AGR_AvaType_e AvaType(this ISwDocument3D xDoc)
        {
            var prop = xDoc.Configurations.Active.Properties.GetOrPreCreate(AGR_PropertyNames.AvaType);
            if (!prop.IsCommitted) prop.Commit(CancellationToken.None);
            if (!string.IsNullOrEmpty(prop.Value.ToString()))
            {
                var avaType = Convert.ToInt32(xDoc.Configurations.Active.Properties[AGR_PropertyNames.AvaType].Value);
            if ((AGR_AvaType_e)avaType != null)
            {
                return (AGR_AvaType_e)avaType;
            }
        }
            return AGR_AvaType_e.Component;
        }
        public static AGR_ComponentType_e ComponentType(this ISwDocument3D xDoc)
        {
            var avaType = xDoc.AvaType();
            if (avaType is AGR_AvaType_e.Purchased)
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

        public static IAGR_BaseComponent AGR_BaseComponent(this IXComponent xComp)
        {
            var xDoc = xComp.ReferencedDocument as ISwDocument3D;
            var componentType = xDoc.ComponentType();
            switch (componentType)
            {
                case AGR_ComponentType_e.Assembly:
                    return new AGR_AssemblyComponentVM(xDoc);
                case AGR_ComponentType_e.Part:
                case AGR_ComponentType_e.SheetMetallPart:
                case AGR_ComponentType_e.Purchased:
                    return new AGR_PartComponentVM(xDoc);
                default:
                    throw new NotImplementedException($"Component type {componentType} is not implemented.");
            }
        }
        public static IAGR_BaseComponent AGR_BaseComponent(this IXDocument xDoc)
        {
            var swDoc = xDoc as ISwDocument3D;
            var componentType = swDoc.ComponentType();
            switch (componentType)
            {
                case AGR_ComponentType_e.Assembly:
                    return new AGR_AssemblyComponentVM(swDoc);
                case AGR_ComponentType_e.Part:
                case AGR_ComponentType_e.SheetMetallPart:
                case AGR_ComponentType_e.Purchased:
                    return new AGR_PartComponentVM(swDoc);
                default:
                    throw new NotImplementedException($"Component type {componentType} is not implemented.");
            }
        }
    }
}
