using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Properties
{
    internal class AGR_PartPropertiesCollection : AGR_BasePropertiesCollection, IAGR_PartPropertiesCollection
    {
        public IXProperty Length
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankLen);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankLen).Value = value;
                OnPropertyChanged(nameof(Length));
            }

        }
        public IXProperty Width
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankWid);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankWid).Value = value;
                OnPropertyChanged(nameof(Width));
            }
        }

        new public void UpdateProperties()
        {
            base.UpdateProperties();
            // Вычисление габаритных размеров

            try
            {
                var part = mDocument as ISwPart;

                var body = part.Bodies.FirstOrDefault();
                if (body != null)
                {
                    var longestEdge = body.Edges.Max(x => x.Length);
                    Length.Value = Math.Round(longestEdge * 1000, 3, MidpointRounding.ToPositiveInfinity).ToString();
                }

            }
            catch (Exception)
            {

            }
            //var boundingBox = mDocument.Evaluation.PreCreateBoundingBox();
            //boundingBox.Commit(CancellationToken.None);
            //var box = boundingBox.Box;
            //Length.Value = Math.Round(box.Length, 3, MidpointRounding.ToPositiveInfinity).ToString();
            //Width.Value = Math.Round(box.Width, 3, MidpointRounding.ToPositiveInfinity).ToString();
        }
        public AGR_PartPropertiesCollection(ISwDocument3D document3D) : base(document3D)
        {
            InitProperties();
            if (!string.IsNullOrEmpty(Length.Value.ToString())) Properties.Add(Length);
            if (!string.IsNullOrEmpty(Width.Value.ToString())) Properties.Add(Width);
        }
    }

}
