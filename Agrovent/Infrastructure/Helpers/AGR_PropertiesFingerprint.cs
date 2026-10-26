// File: Infrastructure/Helpers/AGR_PropertiesFingerprint.cs
using System.Security.Cryptography;
using System.Text;
using AgroventInfrastructure.Enums;
using SolidWorks.Interop.sldworks;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Infrastructure.Helpers
{
    /// <summary>
    /// Отпечаток свойств файла (этап 6): все «Заготовка - …», «Масса», «Материал» (свойство, не MaterialFolder),
    /// «Цвет покраски», «Обозначение», «Артикул», «Признак». «Наименование» не входит.
    /// У неотмеченного компонента изменение отпечатка = пользователь поправил свойства: их пишут в версию без новой версии.
    /// Значение свойства: активная конфигурация, если там непусто, иначе вкладка Custom файла.
    /// Берётся текст значения как записан (не вычисленный): формулы стабильны, а от геометрии защищает отпечаток геометрии.
    /// </summary>
    public static class AGR_PropertiesFingerprint
    {
        private const string BlankPrefix = "Заготовка";

        private static readonly string[] FixedNames =
        {
            AGR_PropertyNames.BlankMass,
            AGR_PropertyNames.Material,
            AGR_PropertyNames.Color,
            AGR_PropertyNames.Partnumber,
            AGR_PropertyNames.Article,
            AGR_PropertyNames.AvaType,
        };

        /// <summary>Возвращает отпечаток (hex) и каноническую строку «имя=значение» по строке на свойство.</summary>
        public static string Compute(ISwDocument3D doc, out string canonical)
        {
            var values = ReadEffective(doc);

            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in FixedNames) names.Add(n);
            foreach (var n in values.Keys)
                if (n.StartsWith(BlankPrefix, StringComparison.OrdinalIgnoreCase)) names.Add(n);

            var sb = new StringBuilder();
            foreach (var name in names)
            {
                var value = values.TryGetValue(name, out var v) ? v : "";
                sb.Append(name).Append('=').Append(value.Trim()).Append('\n');
            }

            canonical = sb.ToString();
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(bytes, 0, 16);
        }

        /// <summary>Все свойства файла, как их видит отпечаток: активная конфигурация, если там непусто, иначе Custom файла.
        /// Читается прямо из документа (VM дочернего компонента может хранить устаревшие значения).</summary>
        public static Dictionary<string, string> ReadEffective(ISwDocument3D doc)
        {
            var model = doc.Model;
            var activeConfig = (model.GetActiveConfiguration() as IConfiguration)?.Name ?? "";

            var result = ReadAll(model, "");
            if (activeConfig.Length > 0)
                foreach (var (name, value) in ReadAll(model, activeConfig))
                    if (!string.IsNullOrWhiteSpace(value) || !result.ContainsKey(name)) result[name] = value;
            return result;
        }

        /// <summary>Все свойства вкладки ("" = Custom файла, иначе конфигурация) без создания новых.</summary>
        private static Dictionary<string, string> ReadAll(IModelDoc2 model, string config)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var mgr = model.Extension.get_CustomPropertyManager(config);
            if (mgr?.GetNames() is not object[] names) return result;

            foreach (var name in names.OfType<string>())
            {
                mgr.Get5(name, false, out string value, out _, out _);
                result[name] = value ?? "";
            }
            return result;
        }
    }
}
