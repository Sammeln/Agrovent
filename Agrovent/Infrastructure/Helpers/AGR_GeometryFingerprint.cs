// File: Infrastructure/Helpers/AGR_GeometryFingerprint.cs
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Infrastructure.Helpers
{
    /// <summary>
    /// Отпечаток геометрии для защиты неотмеченных компонентов (этап 4, вариант Б).
    /// Не решает, нужна ли новая версия: это делает замок. Здесь ложная тревога лучше пропуска.
    /// Деталь: элементы дерева по порядку (имя, тип, погашение) и их размеры (полное имя, значение).
    /// Сборка: то же для элементов сборки (сопряжения, вырезы) + состав верхнего уровня
    /// (файл, конфигурация, погашение, исключение из спецификации, конверт, количество).
    /// Свойства файла, материал, внешний вид в отпечаток не входят. Считается только активная конфигурация.
    /// </summary>
    public static class AGR_GeometryFingerprint
    {
        // Типы элементов, которые не описывают геометрию или пересчитываются сами из других элементов.
        // CHECK: имена типов по GetTypeName2 сверить на реальных моделях (лог canonical в Debug).
        private static readonly HashSet<string> SkippedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "MaterialFolder",      // материал допустимо менять без взятия
            "CommentsFolder", "FavoriteFolder", "HistoryFolder", "SelectionSetFolder",
            "SensorFolder", "DocsFolder", "DetailCabinet", "EqnFolder",
            "SolidBodyFolder", "SurfaceBodyFolder", "CutListFolder", "SubWeldFolder", "SubAtomFolder",
            "AmbientLight", "DirectionLight", "PointLight", "SpotLight", "CameraFolder",
            "Reference",           // компоненты сборки: учитываются отдельно в составе
        };

        /// <summary>Возвращает отпечаток (hex) и каноническую строку, из которой он посчитан.</summary>
        public static string Compute(ISwDocument3D doc, out string canonical)
        {
            var model = doc.Model;
            var sb = new StringBuilder();

            AppendFeatures(model, sb);

            if (model is IAssemblyDoc assembly)
            {
                AppendComposition(assembly, sb);
            }

            canonical = sb.ToString();
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(bytes, 0, 16);
        }

        private static void AppendFeatures(IModelDoc2 model, StringBuilder sb)
        {
            var feat = model.FirstFeature() as Feature;
            while (feat != null)
            {
                AppendFeature(feat, sb, 0);
                feat = feat.GetNextFeature() as Feature;
            }
        }

        private static void AppendFeature(Feature feat, StringBuilder sb, int level)
        {
            var type = feat.GetTypeName2() ?? "";
            if (SkippedTypes.Contains(type)) return;

            sb.Append('F').Append(level).Append('|')
              .Append(feat.Name).Append('|')
              .Append(type).Append('|')
              .Append(feat.IsSuppressed() ? 'S' : 'U')
              .Append('\n');

            AppendDimensions(feat, sb);

            var sub = feat.GetFirstSubFeature() as Feature;
            while (sub != null)
            {
                AppendFeature(sub, sb, level + 1);
                sub = sub.GetNextSubFeature() as Feature;
            }
        }

        private static void AppendDimensions(Feature feat, StringBuilder sb)
        {
            var disp = feat.GetFirstDisplayDimension() as DisplayDimension;
            while (disp != null)
            {
                if (disp.GetDimension2(0) is Dimension dim)
                {
                    // CHECK: GetSystemValue3 возвращает массив значений по конфигурациям
                    var values = dim.GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null) as double[];
                    var value = values != null && values.Length > 0 ? values[0] : dim.SystemValue;

                    sb.Append("D|")
                      .Append(dim.FullName).Append('|')
                      .Append(Math.Round(value, 9).ToString("R", CultureInfo.InvariantCulture))
                      .Append('\n');
                }
                disp = feat.GetNextDisplayDimension(disp) as DisplayDimension;
            }
        }

        private static void AppendComposition(IAssemblyDoc assembly, StringBuilder sb)
        {
            // true = только верхний уровень: состав подсборок хранится в их собственных файлах
            var comps = (assembly.GetComponents(true) as object[]) ?? Array.Empty<object>();

            var lines = comps
                .OfType<Component2>()
                .Select(c =>
                {
                    // Имя файла без папки: у локальной копии и у файла в хранилище папки разные
                    var file = Path.GetFileName(c.GetPathName() ?? "").ToLowerInvariant();
                    return $"C|{file}|{c.ReferencedConfiguration}|" +
                           $"{(c.IsSuppressed() ? 'S' : 'U')}{(c.ExcludeFromBOM ? 'X' : '-')}{(c.IsEnvelope() ? 'E' : '-')}";
                })
                .GroupBy(l => l)
                .Select(g => $"{g.Key}|{g.Count()}")
                .OrderBy(l => l, StringComparer.Ordinal); // порядок в дереве изменением не считаем

            foreach (var line in lines)
            {
                sb.Append(line).Append('\n');
            }
        }
    }
}
