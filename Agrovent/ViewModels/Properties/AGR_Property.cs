using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.ViewModels.Base;
using Xarial.XCad.Data;

namespace Agrovent.ViewModels.Properties
{
    public class AGR_Property : BaseViewModel
    {

        #region Property - Value
        private string _Value;
        public string Value
        {
            get => _Value;
            set => Set(ref _Value, value);
        }
        #endregion

        #region Property - Expression
        private string _Expression;
        public string Expression
        {
            get => _Expression;
            set => Set(ref _Expression, value);
        }
        #endregion


        #region Property - Name
        private string _Name;
        public string Name
        {
            get => _Name;
            set => Set(ref _Name, value);
        }
        #endregion 
        public AGR_Property(IXProperty xProperty)
        {
            Name = xProperty.Name;
            Value = xProperty.Value.ToString();
            Expression = xProperty.Expression;

            xProperty.ValueChanged += XProperty_ValueChanged;
        }

        private void XProperty_ValueChanged(IXProperty prp, object newValue)
        {
            Value = newValue.ToString();
        }
    }
}
