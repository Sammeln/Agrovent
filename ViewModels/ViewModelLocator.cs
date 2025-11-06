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
        public TaskPaneVM TaskPaneVM => AGR_ServiceContainer.GetService<TaskPaneVM>();
    }
}
