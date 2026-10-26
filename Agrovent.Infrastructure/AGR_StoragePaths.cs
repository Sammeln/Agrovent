using System;
using System.IO;

namespace AgroventInfrastructure
{
    /// <summary>
    /// Пути в хранилище: StorageRoot\&lt;StorageGuid&gt;\&lt;Version&gt;\файл.
    /// StorageGuid один на компонент (Components.StorageGuid), выдаётся при первом сохранении.
    /// В папке версии лежат все файлы модели (модель, чертёж, pdf, dxf...).
    /// </summary>
    public static class AGR_StoragePaths
    {
        public static string GetComponentFolder(Guid storageGuid)
            => Path.Combine(AGR_Options.StorageRootFolderPath, storageGuid.ToString("D"));

        public static string GetVersionFolder(Guid storageGuid, int version)
            => Path.Combine(GetComponentFolder(storageGuid), version.ToString());

        public static string GetFilePath(Guid storageGuid, int version, string fileName)
            => Path.Combine(GetVersionFolder(storageGuid, version), Path.GetFileName(fileName));

        /// <summary>
        /// Путь для чтения существующего файла версии: по схеме guid\версия, иначе путь из ComponentFiles
        /// (recordedPath; до миграции хранилища файлы лежат по старой схеме). Нет ни того, ни другого - путь по схеме.
        /// TODO после миграции хранилища: recordedPath не нужен.
        /// </summary>
        public static string ResolveExistingFilePath(Guid? storageGuid, int version, string fileName, string? recordedPath = null)
        {
            string? path = storageGuid.HasValue ? GetFilePath(storageGuid.Value, version, fileName) : null;
            if (path != null && File.Exists(path)) return path;
            if (!string.IsNullOrEmpty(recordedPath) && File.Exists(recordedPath)) return recordedPath;
            return path ?? recordedPath ?? "";
        }
    }
}
