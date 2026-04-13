using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.DAL.Entities.Components;
using System.Windows.Media.Imaging;
using Agrovent.ViewModels.Base;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows;
using Agrovent.Infrastructure.Commands;

namespace Agrovent.ViewModels.Components
{
    public class AGR_ComponentRegistryItemVM : BaseViewModel
    {
        // Приватные поля для хранения данных
        private readonly ComponentVersion _entity; // Хранит оригинальную сущность из БД
        private readonly string _storageRootFolder; // Путь к корню хранилища

        public AGR_ComponentRegistryItemVM(ComponentVersion entity, string storageRootFolder)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _storageRootFolder = storageRootFolder ?? throw new ArgumentNullException(nameof(storageRootFolder));
        }

        // Свойства для отображения в DataGrid
        public int Id => _entity.Component.Id;
        public BitmapImage? Preview
        {
            get
            {
                if (_entity.PreviewImage != null && _entity.PreviewImage.Length > 0)
                {
                    try
                    {
                        using var stream = new MemoryStream(_entity.PreviewImage);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad; // Важно для отображения в UI
                        bitmap.EndInit();
                        bitmap.Freeze(); // Оптимизация для потокобезопасности
                        return bitmap;
                    }
                    catch
                    {
                        // Логирование ошибки
                        return null;
                    }
                }
                return null;
            }
        }
        public string Name => _entity.Name ?? "N/A";
        //public string PartNumber => _entity.Component?.PartNumber ?? "N/A";

        public string PartNumber
        {
            get
            {
                if (_entity?.AvaType == Infrastructure.Enums.AGR_AvaType_e.Component
                    || _entity?.AvaType == Infrastructure.Enums.AGR_AvaType_e.Production) return _entity.Component.PartNumber;
                return "";
            }
        }

        public DateTime CreatedAt => _entity.CreatedAt;
        public string ComponentTypeDisplay => GetDisplayString(_entity.ComponentType);
        public string AvaTypeDisplay => GetDisplayString(_entity.AvaType);
        public int Version => _entity.Version;

        public ComponentVersion ComponentVersion;
        // Свойство для ссылки на файл в хранилище
        public string StoragePath
        {
            get
            {
                // Предполагаем, что файл хранится как Model.FileName (например, Cube.SLDPRT)
                // и путь формируется как StorageRootFolder + HashSum + FileName
                if (!string.IsNullOrEmpty(_entity.Files.First
                        (x => x.FilePath.EndsWith("prt",StringComparison.OrdinalIgnoreCase)
                        || x.FilePath.EndsWith("asm", StringComparison.OrdinalIgnoreCase)
                    ).FilePath))
                {
                    var path = _entity.Files.First(x => x.FilePath.EndsWith("prt", StringComparison.OrdinalIgnoreCase)
                        || x.FilePath.EndsWith("asm", StringComparison.OrdinalIgnoreCase)
                    ).FilePath;

                    var fileName = Path.GetFileName(path);

                    var hashFolder = _entity.HashSum.ToString("D10");
                    var fullPath = Path.Combine(_storageRootFolder, hashFolder, fileName);
                    return fullPath;
                }
                return "N/A"; // Или пустая строка
            }
        }

        // Вспомогательный метод для получения строкового представления Enum
        private static string GetDisplayString(Enum enumValue)
        {
            if (enumValue == null) return "N/A";
            // Можно использовать DescriptionAttribute или просто ToString()
            // Пример с простым ToString():
            return enumValue.ToString(); // Возвращает строку вроде "Assembly", "Part", "Standard", "Purchased" и т.д.
            // Если нужны другие псевдонимы, можно использовать switch/case или словарь.
            /*
            switch (enumValue)
            {
                case AGR_ComponentType_e.Assembly:
                    return "Сборка";
                case AGR_ComponentType_e.Part:
                    return "Деталь";
                // ... другие ...
                default:
                    return enumValue.ToString();
            }
            */
        }
    }
}
