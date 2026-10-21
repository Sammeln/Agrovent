using System.IO;

namespace AgroventInfrastructure
{
    /// <summary>
    /// Пути в хранилище: StorageRoot\&lt;HashSum:D10&gt;\&lt;Version&gt;\файл.
    /// Хеш больше не уникален, поэтому папку версии определяет пара (хеш, версия).
    /// Папка версии неизменяема: файлы существующей версии не перезаписываются.
    /// </summary>
    public static class AGR_StoragePaths
    {
        public static string GetVersionFolder(int hashSum, int version)
            => Path.Combine(AGR_Options.StorageRootFolderPath, hashSum.ToString("D10"), version.ToString());

        public static string GetFilePath(int hashSum, int version, string fileName)
            => Path.Combine(GetVersionFolder(hashSum, version), Path.GetFileName(fileName));

        /// <summary>
        /// Путь для чтения существующего файла. Пока хранилище не переведено на новую схему,
        /// файла в папке версии может не быть: тогда берётся старый путь StorageRoot\&lt;HashSum:D10&gt;\файл.
        /// TODO после миграции хранилища: удалить fallback, оставить GetFilePath.
        /// </summary>
        public static string ResolveExistingFilePath(int hashSum, int version, string fileName)
        {
            var path = GetFilePath(hashSum, version, fileName);
            if (File.Exists(path)) return path;

            var legacy = Path.Combine(AGR_Options.StorageRootFolderPath, hashSum.ToString("D10"), Path.GetFileName(fileName));
            return File.Exists(legacy) ? legacy : path;
        }
    }
}
