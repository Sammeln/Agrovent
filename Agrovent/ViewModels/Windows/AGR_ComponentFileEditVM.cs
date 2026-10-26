// File: ViewModels/Windows/AGR_ComponentFileEditVM.cs
using System.IO;
using Agrovent.ViewModels.Base;
using AgroventInfrastructure;
using AgroventInfrastructure.Entities.Components;
using AgroventInfrastructure.Enums;

namespace Agrovent.ViewModels.Windows
{
    public class AGR_ComponentFileEditVM : BaseViewModel
    {
        private readonly ComponentFile _entity;
        private readonly Guid _storageGuid;
        private readonly string _recordedPath; // путь из ComponentFiles на момент загрузки (до миграции - старая схема)
        private readonly int _version;
        public ComponentFile Entity => _entity;

        public AGR_ComponentFileEditVM(ComponentFile entity, Guid storageGuid, int version)
        {
            _entity = entity;
            _storageGuid = storageGuid;
            _recordedPath = entity.FilePath;
            _version = version;
        }

        public string FileName => Path.GetFileName(_entity.FilePath);
        public AGR_FileType_e FileType => _entity.FileType;
        public string FullName => Path.GetDirectoryName(_entity.FilePath);
        public long? FileSize => _entity.FileSize;
        public DateTime? LastModified => _entity.LastModified;

        /// <summary>Реальный путь файла в хранилище: StorageRoot\StorageGuid\Version\ИмяФайла</summary>
        public string ResolveStoragePath()
            => AGR_StoragePaths.ResolveExistingFilePath(_storageGuid, _version, FileName, _recordedPath);

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