using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.Services;

namespace Agrovent.ViewModels
{
    public class ViewModelLocator
    {
        public AGR_TaskPaneViewModel TaskPaneVM => AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();
    }
}
