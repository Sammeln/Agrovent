using System.IO;
using Agrovent.Infrastructure;
using Agrovent.Infrastructure.Interfaces.Components;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Base
{
    public class AGR_FileComponent : AGR_BaseComponent, IAGR_HasFile
    {

        public string CurrentModelFilePath  => mDocument.Path; 
        public string CurrentDrawFilePath => GetDrawFilePath();
        public string? StorageModelFilePath  => GetStorageModelFilePath();
        public string? StorageDrawFilePath  => GetStorageDrawFilePath();
        public string? ProductionModelFilePath  => GetProdModelFilePath();
        public string? ProductionDrawFilePath  => GetProdDrawFilePath();

        private string? GetDrawFilePath()
        {
            var drawPath = Path.ChangeExtension(mDocument.Path, "slddrw");
            if (File.Exists(drawPath))
            {
                return drawPath;
            }
            else
            {
                return null;
            }
        }
        private string? GetStorageModelFilePath()
        {
            var storageModelPath = Path.Combine(
                AGR_Options.StorageRootFolderPath,
                PartNumber,
                Version.ToString(),
                Path.GetFileName(CurrentModelFilePath)
                );
            if (File.Exists(storageModelPath))
            {
                return storageModelPath;
            }
            else
            {
                return null;
            }
        }
        private string? GetStorageDrawFilePath()
        {
            var drawPath = Path.ChangeExtension(StorageModelFilePath, "slddrw");
            if (File.Exists(drawPath))
            {
                return drawPath;
            }
            else
            {
                return null;
            }
        }
        private string? GetProdModelFilePath()
        {
            var prodFilePath = Path.Combine(
                AGR_Options.ProductionRootFolderPath,
                PartNumber,
                Path.GetFileName(CurrentModelFilePath)
                );
            if (File.Exists(prodFilePath))
            {
                return prodFilePath;
            }
            else
            {
                return null;
            }
        }
        private string? GetProdDrawFilePath()
        {
            var drawPath = Path.ChangeExtension(ProductionModelFilePath, "slddrw");
            if (File.Exists(drawPath))
            {
                return drawPath;
            }
            else
            {
                return null;
            }
        }
        public AGR_FileComponent(ISwDocument3D swDocument3D) : base(swDocument3D)
        {
        }

    }
}
