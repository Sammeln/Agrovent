using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.Infrastructure.Interfaces.Services;
using Agrovent.Services;
using Agrovent.ViewModels.TaskPane;

namespace Agrovent.ViewModels
{
    public class ViewModelLocator
    {
        public AGR_TaskPaneViewModel TaskPaneVM => AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();

        public void RefreshViewModelCache()
        {
            var cache = AGR_ServiceContainer.GetService<IAGR_ComponentViewModelCache>();
            cache.Clear();
        }
    }
}
