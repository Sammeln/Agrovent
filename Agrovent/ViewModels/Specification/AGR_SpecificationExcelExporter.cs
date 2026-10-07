using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Agrovent.ViewModels.Components;
using AgroventInfrastructure.Enums;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace Agrovent.ViewModels.Specification
{
    /// <summary>
    /// Сохранение спецификации (окно AGR_SpecificationWindow) в Excel через NPOI.
    /// Структура листа повторяет окно: блок данных главной сборки, затем таблица
    /// с разделами (сборочные единицы / детали / листовые детали / покупное).
    /// Без превью, чекбоксов и блока ошибок.
    /// </summary>
    public static class AGR_SpecificationExcelExporter
    {
        // ── Порядок и названия разделов. Чтобы поменять порядок — переставьте строки. ──
        private static readonly (AGR_ComponentType_e Type, string Title)[] Sections =
        {
            (AGR_ComponentType_e.Assembly,        "Сборочные единицы"),
            (AGR_ComponentType_e.Part,            "Детали"),
            (AGR_ComponentType_e.SheetMetallPart, "Листовые детали"),
            (AGR_ComponentType_e.Purchased,       "Покупное"),
        };

        private const string OtherSectionTitle = "Прочее";

        private static readonly string[] ColumnHeaders =
            { "Наименование", "PartNumber / Артикул", "Количество", "Толщина", "Материал", "Краска" };

        // Ширина столбцов в символах
        private static readonly int[] ColumnWidths = { 52, 24, 12, 10, 50, 32 };

        private const int ColumnCount = 6;
        private const float LineHeight = 15f;

        public static void Export(
            string filePath,
            string name,
            string configName,
            string partNumber,
            string articleText,
            IEnumerable<AGR_SpecificationItemVM> items)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Не указан путь к файлу.", nameof(filePath));

            var all = (items ?? Enumerable.Empty<AGR_SpecificationItemVM>()).Where(i => i != null).ToList();

            var wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("Спецификация");
            var st = new Styles(wb);

            for (int c = 0; c < ColumnCount; c++)
                sheet.SetColumnWidth(c, ColumnWidths[c] * 256);

            int r = 0;

            // ── Данные главной сборки ──
            r = WriteInfoRow(sheet, st, r, "Наименование:", name);
            if (!string.IsNullOrWhiteSpace(configName))
                r = WriteInfoRow(sheet, st, r, "Конфигурация:", configName);
            r = WriteInfoRow(sheet, st, r, "Partnumber:", partNumber);
            r = WriteInfoRow(sheet, st, r, "Артикул:", articleText);
            r++; // пустая строка-разделитель

            // ── Заголовок таблицы ──
            var headerRow = sheet.CreateRow(r);
            headerRow.HeightInPoints = 20;
            for (int c = 0; c < ColumnCount; c++)
            {
                var cell = headerRow.CreateCell(c);
                cell.CellStyle = st.ColumnHeader;
                cell.SetCellValue(ColumnHeaders[c]);
            }
            r++;
            sheet.CreateFreezePane(0, r); // шапка таблицы остаётся на месте при прокрутке

            // ── Разделы ──
            var knownTypes = new HashSet<AGR_ComponentType_e>(Sections.Select(s => s.Type));

            foreach (var section in Sections)
            {
                var group = all.Where(i => i.ComponentType == section.Type).ToList();
                r = WriteSection(sheet, st, r, section.Title, group);
            }

            var others = all.Where(i => !knownTypes.Contains(i.ComponentType)).ToList();
            r = WriteSection(sheet, st, r, OtherSectionTitle, others);

            // ── Печать: альбомная, по ширине страницы ──
            sheet.PrintSetup.Landscape = true;
            sheet.FitToPage = true;
            sheet.PrintSetup.FitWidth = 1;
            sheet.PrintSetup.FitHeight = 0;

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                wb.Write(fs);
            }
        }

        #region Блоки листа

        private static int WriteInfoRow(ISheet sheet, Styles st, int rowIndex, string label, string value)
        {
            var row = sheet.CreateRow(rowIndex);
            row.HeightInPoints = 20;

            for (int c = 0; c < ColumnCount; c++)
            {
                var cell = row.CreateCell(c);
                cell.CellStyle = st.Info;
            }

            var rt = new XSSFRichTextString(label + " " + (value ?? string.Empty));
            rt.ApplyFont(0, label.Length, st.FontBold);
            if (!string.IsNullOrEmpty(value))
                rt.ApplyFont(label.Length, label.Length + 1 + value.Length, st.FontNormal);
            row.GetCell(0).SetCellValue(rt);

            sheet.AddMergedRegion(new CellRangeAddress(rowIndex, rowIndex, 0, ColumnCount - 1));
            return rowIndex + 1;
        }

        private static int WriteSection(ISheet sheet, Styles st, int rowIndex, string title, List<AGR_SpecificationItemVM> group)
        {
            if (group.Count == 0) return rowIndex;

            // Строка раздела: "Сборочные единицы (3)"
            var groupRow = sheet.CreateRow(rowIndex);
            groupRow.HeightInPoints = 21;
            for (int c = 0; c < ColumnCount; c++)
            {
                var cell = groupRow.CreateCell(c);
                cell.CellStyle = st.Group;
            }
            groupRow.GetCell(0).SetCellValue($"{title} ({group.Count})");
            sheet.AddMergedRegion(new CellRangeAddress(rowIndex, rowIndex, 0, ColumnCount - 1));
            rowIndex++;

            // Как в окне: сортировка по имени внутри раздела
            foreach (var item in group.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                rowIndex = WriteItemRow(sheet, st, rowIndex, item);
            }

            return rowIndex;
        }

        private static int WriteItemRow(ISheet sheet, Styles st, int rowIndex, AGR_SpecificationItemVM item)
        {
            var row = sheet.CreateRow(rowIndex);

            var nameMain = Str(item.Name);
            var nameSub = Str(item.ArticleName);
            var pnText = Str(item.PartnumberOrArticle);
            var matMain = Str(item.MaterialArticle);
            var matSub = Str(item.MaterialName);
            var paintMain = Str(item.PaintArticle);
            var paintSub = Str(item.PaintName);

            // 0: Наименование (вторая строка — артикул, фиолетовым)
            var c0 = row.CreateCell(0);
            c0.CellStyle = st.CellLeft;
            SetTwoLine(c0, nameMain, nameSub, st);

            // 1: PartNumber / Артикул
            var c1 = row.CreateCell(1);
            c1.CellStyle = st.CellLeft;
            c1.SetCellValue(pnText);

            // 2: Количество
            var c2 = row.CreateCell(2);
            c2.CellStyle = st.CellCenter;
            SetValue(c2, item.Quantity);

            // 3: Толщина
            var c3 = row.CreateCell(3);
            c3.CellStyle = st.CellCenter;
            SetValue(c3, item.SheetMetalThickness);

            // 4: Материал (артикул + название фиолетовым)
            var c4 = row.CreateCell(4);
            c4.CellStyle = st.CellLeft;
            SetTwoLine(c4, matMain, matSub, st);

            // 5: Краска
            var c5 = row.CreateCell(5);
            c5.CellStyle = st.CellLeft;
            SetTwoLine(c5, paintMain, paintSub, st);

            // Высота строки по максимальному числу строк в ячейках (автоподбор для rich-text ненадёжен)
            int lines = new[]
            {
                CountLines(nameMain, nameSub, ColumnWidths[0]),
                CountLines(pnText, null, ColumnWidths[1]),
                CountLines(matMain, matSub, ColumnWidths[4]),
                CountLines(paintMain, paintSub, ColumnWidths[5]),
            }.Max();
            row.HeightInPoints = Math.Max(20f, lines * LineHeight + 4f);

            return rowIndex + 1;
        }

        #endregion

        #region Помощники

        private static string Str(object value) => value?.ToString()?.Trim() ?? string.Empty;

        /// <summary>Числа пишем как числа, остальное — как текст.</summary>
        private static void SetValue(ICell cell, object value)
        {
            switch (value)
            {
                case null:
                    return;
                case double d:
                    cell.SetCellValue(d);
                    return;
                case float f:
                    cell.SetCellValue(f);
                    return;
                case int i:
                    cell.SetCellValue(i);
                    return;
                case long l:
                    cell.SetCellValue(l);
                    return;
                case decimal m:
                    cell.SetCellValue((double)m);
                    return;
                default:
                    var s = value.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        cell.SetCellValue(s.Trim());
                    return;
            }
        }

        /// <summary>
        /// Две строки в одной ячейке: первая обычным цветом, вторая — BlueViolet (как в окне).
        /// </summary>
        private static void SetTwoLine(ICell cell, string primary, string secondary, Styles st)
        {
            bool hasPrimary = !string.IsNullOrEmpty(primary);
            bool hasSecondary = !string.IsNullOrEmpty(secondary);

            if (!hasPrimary && !hasSecondary) return;

            if (hasPrimary && !hasSecondary)
            {
                cell.SetCellValue(primary);
                return;
            }

            var parts = new List<(string Text, IFont Font)>();
            if (hasPrimary) parts.Add((primary, st.FontNormal));
            if (hasSecondary) parts.Add((secondary, st.FontViolet));

            var rt = new XSSFRichTextString(string.Join("\n", parts.Select(p => p.Text)));
            int pos = 0;
            foreach (var part in parts)
            {
                rt.ApplyFont(pos, pos + part.Text.Length, part.Font);
                pos += part.Text.Length + 1; // +1 за символ переноса
            }
            cell.SetCellValue(rt);
        }

        private static int CountLines(string first, string second, int widthChars)
        {
            int effective = Math.Max(1, (int)(widthChars * 0.9));
            int Count(string text)
            {
                if (string.IsNullOrEmpty(text)) return 0;
                return text.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.TrimEnd('\r').Length / (double)effective)));
            }
            return Math.Max(1, Count(first) + Count(second));
        }

        #endregion

        #region Стили

        private sealed class Styles
        {
            public readonly IFont FontNormal;
            public readonly IFont FontBold;
            public readonly IFont FontViolet;

            public readonly ICellStyle Info;
            public readonly ICellStyle ColumnHeader;
            public readonly ICellStyle Group;
            public readonly ICellStyle CellLeft;
            public readonly ICellStyle CellCenter;

            public Styles(XSSFWorkbook wb)
            {
                var gray = Rgb(192, 192, 192);

                FontNormal = CreateFont(wb, false, null, 10);
                FontBold = CreateFont(wb, true, null, 10);
                FontViolet = CreateFont(wb, false, Rgb(138, 43, 226), 10); // BlueViolet

                Info = CreateStyle(wb, FontNormal, Rgb(245, 245, 245),
                    HorizontalAlignment.Left, wrap: false, border: null);

                ColumnHeader = CreateStyle(wb, FontBold, Rgb(196, 181, 253),
                    HorizontalAlignment.Center, wrap: true, border: gray);

                Group = CreateStyle(wb, CreateFont(wb, true, null, 11), Rgb(232, 240, 224),
                    HorizontalAlignment.Left, wrap: false, border: gray);

                CellLeft = CreateStyle(wb, FontNormal, null,
                    HorizontalAlignment.Left, wrap: true, border: gray);

                CellCenter = CreateStyle(wb, FontNormal, null,
                    HorizontalAlignment.Center, wrap: true, border: gray);
            }

            private static XSSFColor Rgb(byte r, byte g, byte b) => new XSSFColor(new[] { r, g, b }, null);

            private static IFont CreateFont(XSSFWorkbook wb, bool bold, XSSFColor color, short size)
            {
                var font = (XSSFFont)wb.CreateFont();
                font.FontName = "Calibri";
                font.FontHeightInPoints = size;
                font.IsBold = bold;
                if (color != null) font.SetColor(color);
                return font;
            }

            private static ICellStyle CreateStyle(
                XSSFWorkbook wb, IFont font, XSSFColor fill,
                HorizontalAlignment h, bool wrap, XSSFColor border)
            {
                var s = (XSSFCellStyle)wb.CreateCellStyle();
                s.SetFont(font);
                s.Alignment = h;
                s.VerticalAlignment = VerticalAlignment.Center;
                s.WrapText = wrap;

                if (fill != null)
                {
                    s.SetFillForegroundColor(fill);
                    s.FillPattern = FillPattern.SolidForeground;
                }

                if (border != null)
                {
                    s.BorderTop = BorderStyle.Thin;
                    s.BorderBottom = BorderStyle.Thin;
                    s.BorderLeft = BorderStyle.Thin;
                    s.BorderRight = BorderStyle.Thin;
                    s.SetTopBorderColor(border);
                    s.SetBottomBorderColor(border);
                    s.SetLeftBorderColor(border);
                    s.SetRightBorderColor(border);
                }

                return s;
            }
        }

        #endregion
    }
}
