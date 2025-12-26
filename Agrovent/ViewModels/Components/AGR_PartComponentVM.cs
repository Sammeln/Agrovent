using Agrovent.Infrastructure.Commands;
using System.Diagnostics;
using System.Windows.Input;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.Services;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Documents;
using System.IO;

namespace Agrovent.ViewModels.Components
{
    public class AGR_PartComponentVM : AGR_FileComponent, IAGR_HasMaterial, IAGR_HasPaint
    {
        public IAGR_Material BaseMaterial { get; set; }
        public decimal BaseMaterialCount { get; set; }
        public IAGR_Material? Paint { get; set; }
        public decimal? PaintCount { get; set; }

        #region COMMANDS

        #region OpenFoldefCommand
        private ICommand _OpenFolderCommand;
        public ICommand OpenFolderCommand => _OpenFolderCommand
            ??= new RelayCommand(OnOpenFolderCommandExecuted, CanOpenFolderCommandExecute);
        private bool CanOpenFolderCommandExecute(object p) => true;
        private void OnOpenFolderCommandExecuted(object p)
        {
            var filePath = p.ToString();
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                var folder = Path.GetDirectoryName(filePath);
                if (folder != null)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
            }
        }
        #endregion

        #endregion


        public AGR_PartComponentVM(ISwDocument3D swDocument3D) : base(swDocument3D)
        {
            if (ComponentType == AGR_ComponentType_e.Purchased) return;
         
            BaseMaterial = new AGR_Material(swDocument3D);
            Paint = new AGR_Paint(swDocument3D);

        }

    }
}
