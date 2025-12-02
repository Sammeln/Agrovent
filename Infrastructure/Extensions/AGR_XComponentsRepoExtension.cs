using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.Documents;
using Agrovent.Infrastructure.Enums;
using Xarial.XCad.Data;
using Agrovent.DAL.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components;

namespace Agrovent.Infrastructure.Extensions
{
    public static class AGR_XComponentsRepoExtension
    {
        public static IEnumerable<IXComponent> AGR_TryFlatten(this IXComponentRepository repo)
        {
            IEnumerator<IXComponent> enumer;

            try
            {
                enumer = repo.GetEnumerator();
            }
            catch
            {
                yield break;
            }

            while (true)
            {
                IXComponent comp;

                try
                {
                    if (!enumer.MoveNext())
                    {
                        break;
                    }

                    comp = enumer.Current;
                }
                catch
                {
                    break;
                }

                yield return comp;

                IXComponentRepository children = null;
                AvaType_e avaType = AvaType_e.Purchased;

                var state = comp.State;

                if (!state.HasFlag(ComponentState_e.Suppressed) &&
                    !state.HasFlag(ComponentState_e.SuppressedIdMismatch) &&
                    !state.HasFlag(ComponentState_e.ExcludedFromBom) &&
                    !state.HasFlag(ComponentState_e.Embedded)
                    )
                {
                    try
                    {
                        var avaTypeProp = comp.ReferencedConfiguration.Properties.GetOrPreCreate(AGR_PropertyNames.AvaType);
                        if (avaTypeProp.IsCommitted) avaType = (AvaType_e)Convert.ToInt32(avaTypeProp.Value);
                    }
                    catch (Exception ex)
                    {

                    }
                    if (avaType != AvaType_e.Purchased &&
                        avaType != AvaType_e.DontBuy)

                    {
                        try
                        {
                            children = comp.Children;
                        }
                        catch
                        {
                            children = null;
                        }
                    }

                }
                else
                {
                    children = null;
                }

                if (children != null)
                {
                    foreach (var subComp in AGR_TryFlatten(children))
                    {
                        yield return subComp;
                    }
                }
            }
        }
        public static IEnumerable<IXComponent> AGR_ActiveComponents(this IXComponentRepository repo)
        {
            IEnumerator<IXComponent> enumer;

            try
            {
                enumer = repo.GetEnumerator();
            }
            catch
            {
                yield break;
            }

            while (true)
            {
                IXComponent comp;

                try
                {
                    if (!enumer.MoveNext())
                    {
                        break;
                    }

                    comp = enumer.Current;
                }
                catch
                {
                    break;
                }


                IXComponentRepository children;
                AvaType_e avaType = AvaType_e.Purchased;

                var state = comp.State;
                if (!state.HasFlag(ComponentState_e.Suppressed) &&
                    !state.HasFlag(ComponentState_e.SuppressedIdMismatch) &&
                    !state.HasFlag(ComponentState_e.ExcludedFromBom) &&
                    !state.HasFlag(ComponentState_e.Embedded)
                    )
                {
                    yield return comp;

                }
                else
                {
                    children = null;
                }
            }
        }
        public static IEnumerable<IAGR_BaseComponent> AGR_BaseComponents(this IXComponentRepository repo)
        {
            foreach (var xComp in repo.AGR_TryFlatten())
            {
                var agrComp = xComp.AGR_BaseComponent();
                yield return agrComp;
            }
        }
        public static IEnumerable<IAGR_BaseComponent> AGR_BaseComponents(this IEnumerable<IXComponent> repo)
        {
            foreach (var xComp in repo)
            {
                var agrComp = xComp.AGR_BaseComponent();
                yield return agrComp;
            }
        }
    }
}
