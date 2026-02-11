using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Agrovent.ViewModels.Components;

namespace Agrovent.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для AGR_ComponentRegistryView.xaml
    /// </summary>
    public partial class AGR_ComponentRegistryView : Window
    {
        public AGR_ComponentRegistryView()
        {
            InitializeComponent();
        }


        private void ListViewItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var item = sender as ListViewItem;
                if (item != null && item.IsSelected) // Убедимся, что элемент выделен
                {
                    var dataContext = item.DataContext as AGR_ComponentRegistryItemVM;
                    if (dataContext != null)
                    {
                        var filePath = dataContext.StoragePath;
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            // ВАЖНО: Создаем IDataObject с форматом CF_HDROP
                            //var dataObject = PrepareDataObjectWithHDROP(filePath);
                            var dataObject = new FileInfo(filePath);

                            // Используем DragDrop.DoDragDrop
                            DragDrop.DoDragDrop(item, dataObject, DragDropEffects.Copy | DragDropEffects.Link);
                        }
                        else
                        {
                            // Логирование, если файл не найден
                            // System.Diagnostics.Debug.WriteLine($"Файл не найден для перетаскивания: {filePath}");
                        }
                    }
                }
            }
        }

        // --- НОВЫЙ МЕТОД: Подготовка IDataObject с CF_HDROP ---
        private IDataObject PrepareDataObjectWithHDROP(string filePath)
        {
            var dataObject = new DataObject();

            // 1. Подготовить байтовый массив для CF_HDROP
            var dropFilesData = CreateDropFilesData(filePath);

            // 2. Установить этот массив как данные в формате CF_HDROP
            // Используем формат, определенный в Win32 API
            var hDropFormat = DataFormats.GetDataFormat("CF_HDROP").Name;
            dataObject.SetData(hDropFormat, dropFilesData);

            // 3. (Опционально) Установить текстовый формат для совместимости
            dataObject.SetText(filePath);

            // 4. (Опционально) Установить FileDrop формат (это может быть проще, но не всегда надежно)
            // dataObject.SetFileDropList(new System.Collections.Specialized.StringCollection { filePath });

            return dataObject;
        }

        // --- МЕТОД: Создание данных CF_HDROP ---
        // https://learn.microsoft.com/en-us/windows/winfx/2006/winprogGuide/clipboard-formats#cf_hdrop
        private byte[] CreateDropFilesData(string filePath)
        {
            // 1. Получить байты пути (Unicode)
            var pathBytes = Encoding.Unicode.GetBytes(filePath + '\0'); // Null-terminated строка

            // 2. Подготовить структуру DROPFILES
            var dropFilesStruct = new DROPFILES
            {
                pFiles = (uint)Marshal.SizeOf(typeof(DROPFILES)), // Смещение до списка файлов (после структуры)
                pt = new POINT { x = 0, y = 0 },                 // Позиция (не используется при DragDrop)
                fNC = false,                                      // Не клиентская область
                fWide = true                                      // Пути в формате Unicode
            };

            // 3. Сериализовать структуру DROPFILES в байты
            var structBytes = StructToBytes(dropFilesStruct);

            // 4. Рассчитать общий размер
            var totalLength = structBytes.Length + pathBytes.Length + 2; // +2 для окончательного \0\0 (двойной null terminator для списка)

            // 5. Создать итоговый массив
            var result = new byte[totalLength];

            // 6. Скопировать байты структуры
            Buffer.BlockCopy(structBytes, 0, result, 0, structBytes.Length);

            // 7. Скопировать байты пути
            Buffer.BlockCopy(pathBytes, 0, result, structBytes.Length, pathBytes.Length);

            // 8. Остальные байты (result[result.Length - 2] и result[result.Length - 1]) остаются 0 из-за инициализации массива

            return result;
        }

        // --- ВСПОМОГАТЕЛЬНЫЙ МЕТОД: Сериализация структуры ---
        private byte[] StructToBytes<T>(T obj) where T : struct
        {
            var size = Marshal.SizeOf(obj);
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        // --- СТРУКТУРЫ: DROPFILES и POINT ---
        [StructLayout(LayoutKind.Sequential)]
        private struct DROPFILES
        {
            public uint pFiles; // Смещение до списка файлов (в байтах от начала структуры)
            public POINT pt;    // Позиция (x, y) в клиентских координатах (не используется при DragDrop)
            public bool fNC;    // true, если pt содержит не клиентские координаты
            public bool fWide;  // true, если пути в формате Unicode (false для ANSI)
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
    }
}
