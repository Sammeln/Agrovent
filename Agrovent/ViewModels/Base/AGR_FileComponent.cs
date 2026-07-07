using System.IO;
using Agrovent.Infrastructure;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Base
{
    public class AGR_FileComponent : AGR_BaseComponent, IAGR_HasFile
    {
        public AGR_FileComponent(ISwDocument3D swDocument3D) : base(swDocument3D)
        {
            _CurrentModelFilePath = SwDocument.Path;
            _CurrentDrawFilePath = GetDrawFilePath();
            _StorageModelFilePath = GetStorageModelFilePath();
            _StorageDrawFilePath = GetStorageDrawFilePath();
            _ProductionModelFilePath = GetProdModelFilePath();
            _ProductionDrawFilePath = GetProdDrawFilePath();
        }

        #region Property - CurrentModelFilePath
        private string _CurrentModelFilePath = "";
        public string CurrentModelFilePath
        {
            get => _CurrentModelFilePath;
            set => Set(ref _CurrentModelFilePath, value);
        } 
        #endregion

        #region Property - CurrentDrawFilePath
        private string _CurrentDrawFilePath = "";
        public string CurrentDrawFilePath
        {
            get => _CurrentDrawFilePath;
            set => Set(ref _CurrentDrawFilePath, value);
        }
        #endregion 

        #region Property - StorageModelFilePath
        private string _StorageModelFilePath = "";
        public string StorageModelFilePath
        {
            get => _StorageModelFilePath;
            set => Set(ref _StorageModelFilePath, value);
        }
        #endregion

        public bool StorageModelFileIsExist => File.Exists(StorageModelFilePath);

        #region Property - StorageDrawFilePath
        private string _StorageDrawFilePath = "";
        public string StorageDrawFilePath
        {
            get => _StorageDrawFilePath;
            set => Set(ref _StorageDrawFilePath, value);
        }
        #endregion 
        public bool StorageDrawFileIsExist => File.Exists(StorageDrawFilePath);


        #region Property - ProductionModelFilePath
        private string _ProductionModelFilePath = "";
        public string ProductionModelFilePath
        {
            get => _ProductionModelFilePath;
            set => Set(ref _ProductionModelFilePath, value);
        }
        #endregion    
        public bool ProductionModelFileIsExist => File.Exists(ProductionModelFilePath);

        #region Property - ProductionDrawFilePath
        private string _ProductionDrawFilePath = "";
        public string ProductionDrawFilePath
        {
            get => _ProductionDrawFilePath;
            set => Set(ref _ProductionDrawFilePath, value);
        }
        #endregion 
        public bool ProductionDrawFileIsExist => File.Exists(ProductionDrawFilePath);

        public string? GetDrawFilePath()
        {
            var drawPath = Path.ChangeExtension(SwDocument.Path, "slddrw");
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


 
    }
}
