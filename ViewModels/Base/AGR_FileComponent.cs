using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.Infrastructure;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Base
{
    public class AGR_FileComponent : AGR_BaseComponent, IHasFile
    {

        public string? CurrentModelFilePath { get => mDocument.Path; }
        public string? CurrentDrawFilePath => GetDrawFilePath();
        public string? StorageModelFilePath { get => GetStorageModelFilePath();}
        public string? StorageDrawFilePath { get => GetStorageDrawFilePath();}
        public string? ProductionModelFilePath { get => GetProdModelFilePath();}
        public string? ProductionDrawFilePath { get => GetProdDrawFilePath();}

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
