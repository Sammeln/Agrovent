// File: Services/AGR_MarkerService.cs
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Xarial.XCad.Documents;
using Xarial.XCad.Data.Enums; // AccessType_e: те же using, что в рабочем тесте MarkerStorageXcadTest
using Xarial.XCad.Enums;

namespace Agrovent.Services
{
    /// <summary>Маркер модели: связь файла с компонентом и версией в БД.</summary>
    public class AGR_Marker
    {
        /// <summary>2 (этап 6): + StorageGuid, отпечаток свойств, версия алгоритма отпечатков. Маркер схемы 1 читается как повреждённый.</summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>Увеличить при любом изменении AGR_GeometryFingerprint или AGR_PropertiesFingerprint:
        /// отпечатки другой версии алгоритма не сравниваются.</summary>
        public const int CurrentFingerprintVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public int ComponentId { get; set; }
        public string PartNumber { get; set; } = "";
        public int Version { get; set; }
        /// <summary>Папка компонента в хранилище (Components.StorageGuid).</summary>
        public Guid StorageGuid { get; set; }
        /// <summary>Отпечаток геометрии на момент сохранения версии (AGR_GeometryFingerprint).</summary>
        public string GeometryFingerprint { get; set; } = "";
        /// <summary>Отпечаток свойств на момент сохранения версии (AGR_PropertiesFingerprint).</summary>
        public string PropertiesFingerprint { get; set; } = "";
        public int FingerprintVersion { get; set; } = CurrentFingerprintVersion;

        /// <summary>Отпечатки посчитаны текущим алгоритмом: их можно сравнивать.</summary>
        public bool FingerprintsComparable => FingerprintVersion == CurrentFingerprintVersion;
    }

    public enum AGR_MarkerReadResult_e
    {
        Ok,
        /// <summary>Потока нет или он пустой (новый файл, «очистить идентификаторы»).</summary>
        Missing,
        /// <summary>Поток есть, но подпись или формат не сходятся.</summary>
        Invalid
    }

    public interface IAGR_MarkerService
    {
        /// <summary>Читает маркер. Можно вызывать в любой момент, пока документ открыт.</summary>
        AGR_MarkerReadResult_e TryRead(IXDocument doc, out AGR_Marker? marker, out string? error);

        /// <summary>Сохраняет документ, записывая маркер в StreamWriteAvailable.
        /// null = записать пустой поток (обнулить маркер).</summary>
        bool SaveWithMarker(IXDocument doc, AGR_Marker? marker, out string? error);
    }

    public class AGR_MarkerService : IAGR_MarkerService
    {
        public const string StreamName = "Agrovent.Marker";

        // Подпись защищает от случайной порчи, не от умышленной подделки, поэтому ключ в коде.
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("Agrovent.Marker.v1/7f3c9a2e");

        private const string SigPrefix = "\nSig=";

        private readonly ILogger<AGR_MarkerService> _logger;

        public AGR_MarkerService(ILogger<AGR_MarkerService> logger)
        {
            _logger = logger;
        }

        public AGR_MarkerReadResult_e TryRead(IXDocument doc, out AGR_Marker? marker, out string? error)
        {
            marker = null; error = null;
            string text;
            try
            {
                using var s = doc.OpenStream(StreamName, AccessType_e.Read);
                if (s == null) return AGR_MarkerReadResult_e.Missing;
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                if (ms.Length == 0) return AGR_MarkerReadResult_e.Missing;
                text = Encoding.UTF8.GetString(ms.ToArray());
            }
            catch (Exception ex)
            {
                // Проверено на этапе 1: нет потока = исключение «Stream doesn't exist».
                // Отличить его от других ошибок по типу нельзя, поэтому любое исключение чтения = «маркера нет».
                _logger.LogDebug(ex, $"Маркер: поток не прочитан ({doc.Title}).");
                return AGR_MarkerReadResult_e.Missing;
            }

            if (!TryDeserialize(text, out marker, out error))
            {
                _logger.LogWarning($"Маркер повреждён ({doc.Title}): {error}");
                return AGR_MarkerReadResult_e.Invalid;
            }
            return AGR_MarkerReadResult_e.Ok;
        }

        public bool SaveWithMarker(IXDocument doc, AGR_Marker? marker, out string? error)
        {
            error = null;
            string? writeError = null;
            bool written = false;
            byte[] bytes = marker == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(Serialize(marker));

            void OnWrite(IXDocument d)
            {
                d.StreamWriteAvailable -= OnWrite;
                try
                {
                    using var s = d.OpenStream(StreamName, AccessType_e.Write);
                    s.SetLength(0);
                    s.Write(bytes, 0, bytes.Length);
                    written = true;
                }
                catch (Exception ex) { writeError = ex.GetType().Name + ": " + ex.Message; }
            }

            doc.StreamWriteAvailable += OnWrite;
            try
            {
                doc.Save();
            }
            catch (Exception ex)
            {
                error = "Save(): " + ex.Message;
                return false;
            }
            finally
            {
                doc.StreamWriteAvailable -= OnWrite; // если событие не пришло
            }

            if (!written)
            {
                error = writeError ?? "событие StreamWriteAvailable не пришло";
                _logger.LogError($"Маркер не записан ({doc.Title}): {error}");
                return false;
            }
            _logger.LogInformation(marker == null
                ? $"Маркер обнулён: {doc.Title}"
                : $"Маркер записан: {doc.Title} -> {marker.PartNumber} v{marker.Version} (Id {marker.ComponentId})");
            return true;
        }

        #region Формат

        private static string Sign(string body)
        {
            using var hmac = new HMACSHA256(Key);
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
        }

        private static string Serialize(AGR_Marker m)
        {
            var body = $"SchemaVersion={m.SchemaVersion}\nComponentId={m.ComponentId}\nPartNumber={m.PartNumber}" +
                       $"\nVersion={m.Version}\nStorageGuid={m.StorageGuid:D}" +
                       $"\nGeometryFingerprint={m.GeometryFingerprint}\nPropertiesFingerprint={m.PropertiesFingerprint}" +
                       $"\nFingerprintVersion={m.FingerprintVersion}";
            return body + SigPrefix + Sign(body);
        }

        private static bool TryDeserialize(string text, out AGR_Marker? marker, out string? error)
        {
            marker = null; error = null;
            int idx = text.LastIndexOf(SigPrefix, StringComparison.Ordinal);
            if (idx < 0) { error = "нет подписи"; return false; }

            var body = text.Substring(0, idx);
            if (Sign(body) != text.Substring(idx + SigPrefix.Length).Trim()) { error = "подпись не совпала"; return false; }

            var m = new AGR_Marker { SchemaVersion = 0, FingerprintVersion = 0 };
            foreach (var line in body.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                var k = line.Substring(0, eq);
                var v = line.Substring(eq + 1);
                switch (k)
                {
                    case "SchemaVersion": m.SchemaVersion = int.TryParse(v, out var sv) ? sv : 0; break;
                    case "ComponentId": m.ComponentId = int.TryParse(v, out var id) ? id : 0; break;
                    case "PartNumber": m.PartNumber = v; break;
                    case "Version": m.Version = int.TryParse(v, out var ver) ? ver : 0; break;
                    case "StorageGuid": m.StorageGuid = Guid.TryParse(v, out var g) ? g : Guid.Empty; break;
                    case "GeometryFingerprint": m.GeometryFingerprint = v; break;
                    case "PropertiesFingerprint": m.PropertiesFingerprint = v; break;
                    case "FingerprintVersion": m.FingerprintVersion = int.TryParse(v, out var fv) ? fv : 0; break;
                }
            }

            if (m.SchemaVersion < 1) { error = "нет SchemaVersion (тестовый или чужой маркер)"; return false; }
            if (m.SchemaVersion < AGR_Marker.CurrentSchemaVersion) { error = $"маркер старого формата (схема {m.SchemaVersion}), обнулите его"; return false; }
            if (m.SchemaVersion > AGR_Marker.CurrentSchemaVersion) { error = $"неизвестная SchemaVersion {m.SchemaVersion}"; return false; }
            if (m.ComponentId <= 0 || m.Version <= 0 || string.IsNullOrEmpty(m.PartNumber) || m.StorageGuid == Guid.Empty)
            { error = "неполные данные"; return false; }

            marker = m;
            return true;
        }

        #endregion
    }
}
