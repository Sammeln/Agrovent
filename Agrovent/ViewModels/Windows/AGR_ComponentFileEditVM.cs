// File: ViewModels/Windows/AGR_ComponentFileEditVM.cs
using System.IO;
using Agrovent.Infrastructure;
using Agrovent.Infrastructure.Enums;
using Agrovent.ViewModels.Base;
using AgroventInfrastructure.Entities.Components;

namespace Agrovent.ViewModels.Windows
{
    public class AGR_ComponentFileEditVM : BaseViewModel
    {
        private readonly ComponentFile _entity;
        private readonly int _hashSum;
        public ComponentFile Entity => _entity;

        public AGR_ComponentFileEditVM(ComponentFile entity, int hashSum)
        {
            _entity = entity;
            _hashSum = hashSum;
        }

        public string FileName => Path.GetFileName(_entity.FilePath);
        public AGR_FileType_e FileType => _entity.FileType;
        public string FullName => Path.GetDirectoryName(_entity.FilePath);
        public long? FileSize => _entity.FileSize;
        public DateTime? LastModified => _entity.LastModified;

        /// <summary>Реальный путь файла в хранилище: StorageRoot\HashSum\ИмяФайла</summary>
        public string ResolveStoragePath()
            => Path.Combine(AGR_Options.StorageRootFolderPath, _hashSum.ToString("D10"), FileName);

        public void UpdateFromSource(string sourceFilePath)
        {
            _entity.FilePath = sourceFilePath;
            _entity.FileSize = new FileInfo(sourceFilePath).Length;
            _entity.LastModified = File.GetLastWriteTime(sourceFilePath);
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(FileSize));
            OnPropertyChanged(nameof(LastModified));
        }
    }
}