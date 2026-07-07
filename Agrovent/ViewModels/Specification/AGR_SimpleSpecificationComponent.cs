using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.ViewModels.Base;
using Xarial.XCad.Documents;

namespace Agrovent.ViewModels.Specification
{
    public class AGR_SimpleSpecificationComponent : BaseViewModel
    {
        private IXComponent _xComponent;

        public AGR_SimpleSpecificationComponent()
        {
                
        }
        public AGR_SimpleSpecificationComponent(IXComponent xComponent, int quantity)
        {
            _xComponent = xComponent;
            _Name = Path.GetFileNameWithoutExtension(_xComponent.ReferencedDocument.Path);
            _ConfigName = _xComponent.ReferencedConfiguration.Name;
            _PartNumber = _xComponent.ReferencedDocument.Properties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value.ToString() ?? "";
            _Quantity = quantity;
        }

        #region Property - Name
        private string _Name;
		public string Name
		{
			get => _Name;
			set => Set(ref _Name, value);
		}
        #endregion

        #region Property - ConfigName
        private string _ConfigName;
        public string ConfigName
        {
            get => _ConfigName;
            set => Set(ref _ConfigName, value);
        }
        #endregion

        #region Property - PartNumber
        private string _PartNumber;
        public string PartNumber
        {
            get => _PartNumber;
            set => Set(ref _PartNumber, value);
        }
        #endregion

        #region Property - Quantity
        private int _Quantity;
        public int Quantity
        {
            get => _Quantity;
            set => Set(ref _Quantity, value);
        }
        #endregion 
    }
}
