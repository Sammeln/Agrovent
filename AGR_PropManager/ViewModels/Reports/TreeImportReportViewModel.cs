// File: ViewModels/Reports/TreeImportReportViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AGR_PropManager.Infrastructure.Commands;
using AGR_PropManager.ViewModels.Base;
using AGR_PropManager.ViewModels.Components;
using Agrovent.DAL;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums; // Assuming AGR_ComponentType_e is here
using Microsoft.Win32; // For SaveFileDialog
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace AGR_PropManager.ViewModels.Reports
{
    public class TreeImportReportItem : INotifyPropertyChanged
    {
        private bool _hasErrors;
        private string _errorTooltip = "";

        public string MainArtName { get; set; }
        public string MainPartNumber { get; set; }
        public string ChildPartNumber { get; set; }
        public string ChildName { get; set; }
        public string MainArticleAVA { get; set; }
        public string ChildArticleAVA { get; set; }
        public double Quantity { get; set; }
        // Пустые столбцы 7-10 (индексы 7-10)
        public string ChildUnit { get; set; }
        // Пустые столбцы 12-13 (индексы 12-13)
        public string ChildType { get; set; }
        // Пустые столбцы 15-24 (индексы 15-24)
        public string ChildURL { get; set; }

        // Свойства для отображения ошибок
        public bool HasErrors
        {
            get => _hasErrors;
            set { _hasErrors = value; OnPropertyChanged(nameof(HasErrors)); }
        }

        public string ErrorTooltip
        {
            get => _errorTooltip;
            set { _errorTooltip = value; OnPropertyChanged(nameof(ErrorTooltip)); }
        }

        // Метод для проверки ошибок на основе данных и типов
        public void Validate(ComponentVersion mainComponent, ComponentVersion childComponent, double quantity)
        {
            HasErrors = false;
            ErrorTooltip = "";

            string errors = "";

            // Проверка Part Number главного артикула
            if (mainComponent.ComponentType != AGR_ComponentType_e.Purchased && string.IsNullOrEmpty(mainComponent.Component.PartNumber))
            {
                errors += "Пустой partnumber главной позиции; ";
                HasErrors = true;
            }

            // Проверка Part Number child
            if (childComponent.ComponentType != AGR_ComponentType_e.Purchased && string.IsNullOrEmpty(childComponent.Component.PartNumber))
            {
                errors += "Пустой partnumber подчиненной позиции; ";
                HasErrors = true;
            }

            // Проверка Article для Purchased
            if (childComponent.ComponentType == AGR_ComponentType_e.Purchased && string.IsNullOrEmpty(childComponent.AvaArticleArticle?.ToString()))
            {
                errors += "Пустой артикул AVA для покупного компонента; ";
                HasErrors = true;
            }

            // Проверка Quantity
            if (quantity <= 0)
            {
                errors += "Количество должно быть больше 0; ";
                HasErrors = true;
            }

            if (!string.IsNullOrEmpty(errors))
            {
                ErrorTooltip = errors.TrimEnd(' ', ';');
            }
        }


        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TreeImportReportViewModel : BaseViewModel
    {
        #region Fields
        private readonly UnitOfWork _unitOfWork;
        private readonly ComponentItemViewModel _mainComponent;
        private readonly string _mainProductName; // Name for the filename
        private string _statusMessage;
        private bool _isGenerating;
        private string FilePath = string.Empty;

        #endregion

        #region Constructor

        public TreeImportReportViewModel(ComponentItemViewModel mainComponent, UnitOfWork unitOfWork)
        {
            _mainComponent = mainComponent ?? throw new ArgumentNullException(nameof(mainComponent));
            _mainProductName = _mainComponent.Name ?? "Неизвестное_изделие";
            _unitOfWork = unitOfWork;


            ReportData = new ObservableCollection<TreeImportReportItem>();
            LoadReportDataAsync(); // Загружаем асинхронно

        }

        #endregion

        #region Properties

        public ObservableCollection<TreeImportReportItem> ReportData { get; }
        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }
        public bool IsGenerating
        {
            get => _isGenerating;
            set => Set(ref _isGenerating, value);
        }

        #endregion

        #region Commands


        #region CanExportToExcelCommand
        private ICommand _ExportToExcelCommand;
        public ICommand ExportToExcelCommand => _ExportToExcelCommand
            ??= new RelayCommand(OnExportToExcelCommandExecuted, CanExportToExcelCommandExecute);
        private bool CanExportToExcelCommandExecute(object p) => true;
        private void OnExportToExcelCommandExecuted(object p)
        {
            GenerateAndSaveExcel();
            CloseWindow();
        }
        #endregion 

        #endregion

        #region Helpers

        private async void LoadReportDataAsync()
        {
            StatusMessage = "Загрузка данных отчета...";
            IsGenerating = true; // Используем IsGenerating как индикатор загрузки тоже
            try
            {
                // Предполагаем, что _mainComponent.PartNumber и Version установлены корректно
                var structureEntries = await _unitOfWork.ComponentRepository.GetAssemblyStructureRecursive(_mainComponent.PartNumber, _mainComponent.Version); // Используем DataService или UnitOfWork из _mainComponent

                // Очищаем старые данные
                ReportData.Clear();

                // Найдем версию сборки по PartNumber (берем последнюю по версии)
                //var assemblyVersion = await _unitOfWork.ComponentRepository.GetLatestComponentVersion(_mainComponent.PartNumber);

                // Проходим по структуре и формируем строки отчета
                foreach (var entry in structureEntries)
                {
                    var parent = entry.ParentComponentVersion;
                    var child = entry.ChildComponentVersion;

                    var reportItem = new TreeImportReportItem
                    {
                        MainArtName = parent.Name,
                        MainPartNumber = parent.Component.PartNumber,
                        ChildPartNumber = child.ComponentType == AGR_ComponentType_e.Purchased ? "" : child.Component.PartNumber,
                        ChildName = child.Name,
                        MainArticleAVA = parent.AvaArticle?.Article.ToString() ?? "",
                        ChildArticleAVA = child.AvaArticle?.Article.ToString() ?? "",
                        Quantity = entry.Quantity, // Quantity из AssemblyStructure
                        ChildUnit = "шт",//DetermineUnit(childComponentVM.ComponentType), // Определяем ЕИ
                        ChildType = "Комплектующие", //DetermineType(childComponentVM.ComponentType), // Определяем Тип
                        ChildURL = $@"\\192.168.10.1\kd\Listogib\TestRootFolder\{child.Component.PartNumber}" // Формируем URL
                    };

                    // Проверяем на ошибки
                    reportItem.Validate(parent, child, entry.Quantity);

                    ReportData.Add(reportItem);
                }

                // Добавляем строки для материалов, связанных с деталями в структуре
                // Здесь нужно будет пройтись по структуре снова и найти детали (Part, SheetMetalPart),
                // затем получить их материалы и добавить строки.

                // Псевдокод для демонстрации:
                // foreach (var entry in structureEntries)
                // {
                //     var childComponent = entry.ChildComponentVersion.Component;
                //     if (childComponent.ComponentType == AGR_ComponentType_e.Part || childComponent.ComponentType == AGR_ComponentType_e.SheetMetallPart)
                //     {
                //         // Получить материал для детали (например, через DataService)
                //         var materialComponent = await _mainComponent.DataService.GetMaterialForPart(childComponent.PartNumber);
                //         if (materialComponent != null)
                //         {
                //              var materialComponentVM = new ComponentItemViewModel(materialComponent);
                //              var matReportItem = new TreeImportReportItem
                //              {
                //                  MainArtName = childComponentVM.Name, // Имя детали
                //                  MainPartNumber = childComponentVM.PartNumber,
                //                  ChildPartNumber = materialComponentVM.PartNumber,
                //                  ChildName = materialComponentVM.Name,
                //                  MainArticleAVA = childComponentVM.Article ?? "",
                //                  ChildArticleAVA = materialComponentVM.Article ?? "",
                //                  Quantity = CalculateMaterialQuantity(...), // Необходимо рассчитать
                //                  ChildUnit = "м2", // Для материала
                // "", // Для материала
                //                  ChildURL = $@"\\192.168.10.1\kd\Listogib\TestRootFolder\{materialComponentVM.PartNumber}"
                //              };
                //              matReportItem.Validate(childComponentVM, materialComponentVM, calculatedQuantity); // Проверить
                //              ReportData.Add(matReportItem);
                //         }
                //         // Также можно добавить покраску, если она есть
                //         var paintComponent = await _mainComponent.DataService.GetPaintForPart(childComponent.PartNumber);
                //         if (paintComponent != null)
                //         {
                //              var paintComponentVM = new ComponentItemViewModel(paintComponent);
                //              var paintReportItem = new TreeImportReportItem
                //              {
                //                  MainArtName = childComponentVM.Name, // Имя детали
                //                  MainPartNumber = childComponentVM.PartNumber,
                //                  ChildPartNumber = paintComponentVM.PartNumber,
                //                  ChildName = paintComponentVM.Name,
                //                  MainArticleAVA = childComponentVM.Article ?? "",
                //                  ChildArticleAVA = paintComponentVM.Article ?? "",
                //                  Quantity = 1, // Пример
                //                  ChildUnit = "шт", // Или другая ЕИ для покраски
                //                  ChildType = "", // Для покраски
                //                  ChildURL = $@"\\192.168.10.1\kd\Listogib\TestRootFolder\{paintComponentVM.PartNumber}"
                //              };
                //              paintReportItem.Validate(childComponentVM, paintComponentVM, 1); // Проверить
                //              ReportData.Add(paintReportItem);
                //         }
                //     }
                // }

                StatusMessage = $"Загружено {ReportData.Count} строк.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка при загрузке данных: {ex.Message}";
                MessageBox.Show(StatusMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsGenerating = false; // Завершаем индикатор загрузки
            }
        }
        private void CloseWindow()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private string DetermineUnit(AGR_ComponentType_e type)
        {
            return type switch
            {
                AGR_ComponentType_e.Part or AGR_ComponentType_e.SheetMetallPart or AGR_ComponentType_e.Assembly => "шт",
                //AGR_ComponentType_e.Material => "м2",
                _ => "м2"
            };
        }

        private string DetermineType(AGR_ComponentType_e type)
        {
            return type switch
            {
                AGR_ComponentType_e.Part or AGR_ComponentType_e.SheetMetallPart or AGR_ComponentType_e.Assembly => "Комплектующие",
                _ => ""
            };
        }


        private void GenerateAndSaveExcel()
        {
            if (IsGenerating) return;
            IsGenerating = true;
            StatusMessage = "Генерация Excel...";

            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                    FileName = $"{_mainProductName}_Отчет_импорта_дерева.xlsx",
                    DefaultExt = ".xlsx",
                    AddExtension = true,
                    OverwritePrompt = true,
                    CheckPathExists = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    StatusMessage = "Создание файла...";

                    var filePath = saveFileDialog.FileName;
                    FilePath = filePath;

                    using (var workbook = new XSSFWorkbook())
                    {
                        ISheet sheet = workbook.CreateSheet("Отчет импорта дерева");

                        // Create header row
                        IRow headerRow = sheet.CreateRow(0);
                        headerRow.CreateCell(0).SetCellValue("Наименование главного артикула");
                        headerRow.CreateCell(1).SetCellValue("Part Number главного артикула");
                        headerRow.CreateCell(2).SetCellValue("Part Number child");
                        headerRow.CreateCell(3).SetCellValue("Наименование child");
                        headerRow.CreateCell(4).SetCellValue("Артикул AVA гл.артикула");
                        headerRow.CreateCell(5).SetCellValue("Артикул AVA child");
                        headerRow.CreateCell(6).SetCellValue("Кол-во");
                        // Columns 7-10: Пусто
                        headerRow.CreateCell(7).SetCellValue("");
                        headerRow.CreateCell(8).SetCellValue("");
                        headerRow.CreateCell(9).SetCellValue("");
                        headerRow.CreateCell(10).SetCellValue("");
                        headerRow.CreateCell(11).SetCellValue("ЕИ child");
                        // Columns 12-13: Пусто
                        headerRow.CreateCell(12).SetCellValue("");
                        headerRow.CreateCell(13).SetCellValue("");
                        headerRow.CreateCell(14).SetCellValue("Тип child");
                        
                        headerRow.CreateCell(25).SetCellValue("URL child");

                        // Style for PartNumber columns to preserve leading zeros
                        ICellStyle partNumberStyle = workbook.CreateCellStyle();
                        partNumberStyle.DataFormat = HSSFDataFormat.GetBuiltinFormat("@");

                        int rowIndex = 1;
                        foreach (var item in ReportData)
                        {
                            IRow row = sheet.CreateRow(rowIndex++);

                            row.CreateCell(0).SetCellValue(item.MainArtName);
                            var mainPNCell = row.CreateCell(1);
                            mainPNCell.SetCellValue(item.MainPartNumber);
                            mainPNCell.CellStyle = partNumberStyle;

                            var childPNCell = row.CreateCell(2);
                            childPNCell.SetCellValue(item.ChildPartNumber);
                            childPNCell.CellStyle = partNumberStyle;

                            row.CreateCell(3).SetCellValue(item.ChildName);
                            row.CreateCell(4).SetCellValue(item.MainArticleAVA);
                            row.CreateCell(5).SetCellValue(item.ChildArticleAVA);
                            row.CreateCell(6).SetCellValue(item.Quantity);

                            // Columns 7-10: Пусто
                            row.CreateCell(7).SetCellValue("");
                            row.CreateCell(8).SetCellValue("");
                            row.CreateCell(9).SetCellValue("");
                            row.CreateCell(10).SetCellValue("");

                            row.CreateCell(11).SetCellValue(item.ChildUnit);

                            // Columns 12-13: Пусто
                            row.CreateCell(12).SetCellValue("");
                            row.CreateCell(13).SetCellValue("");

                            row.CreateCell(14).SetCellValue(item.ChildType);

                            // Columns 15-24: Пусто
                            for (int i = 15; i <= 24; i++)
                            {
                                row.CreateCell(i).SetCellValue("");
                            }

                            row.CreateCell(25).SetCellValue(item.ChildURL);
                        }

                        // Optional: Auto-size columns after populating data
                        for (int i = 0; i < 26; i++) // 26 columns (0-25)
                        {
                            sheet.AutoSizeColumn(i);
                        }

                        StatusMessage = "Сохранение файла...";
                        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                        {
                            workbook.Write(fileStream);
                        }

                        StatusMessage = $"Файл успешно сохранен: {filePath}";
                    }
                }
                else
                {
                    StatusMessage = "Операция сохранения отменена.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка при создании или сохранении Excel: {ex.Message}";
                MessageBox.Show(StatusMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsGenerating = false;
            }
        }

        #endregion

        public event EventHandler? CloseRequested;
    }
}