using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Xarial.XCad;
using Xarial.XCad.Data;
using Xarial.XCad.Documents;
using Xarial.XCad.Enums;           
using AgroventInfrastructure.Enums;
using Xarial.XCad.Data.Enums; // AGR_PropertyNames.Partnumber

namespace Agrovent.Tests
{
    public class XcadMarker
    {
        public string PartNumber { get; set; }
        public int Version { get; set; }
        public string Hash { get; set; }
        public int ComponentId { get; set; }
    }

    public static class TestLog
    {
        public static readonly string FilePath = Path.Combine(Path.GetTempPath(), "marker_test.log");
        public static void Write(string text)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff}  {text}";
            Debug.WriteLine(line);
            try { File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8); } catch { }
        }
    }

    public static class XcadMarkerStorage
    {
        public const string StreamName = "Agrovent.Marker";
        public static bool CorruptSignature; // для сценария 8

        // Для теста ключ в коде. В боевой версии - не в открытом виде.
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("TEST-KEY-CHANGE-ME");

        private static string Sign(string body)
        {
            using (var hmac = new HMACSHA256(Key))
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
        }

        public static string Serialize(XcadMarker m)
        {
            string body = $"v1\nPartNumber={m.PartNumber}\nVersion={m.Version}\nHash={m.Hash}\nComponentId={m.ComponentId}";
            string sig = Sign(body);
            if (CorruptSignature) sig = "X" + sig;
            return body + "\nSig=" + sig;
        }

        public static bool TryDeserialize(string text, out XcadMarker marker, out string error)
        {
            marker = null; error = null;
            int idx = text.LastIndexOf("\nSig=", StringComparison.Ordinal);
            if (idx < 0) { error = "нет подписи"; return false; }
            string body = text.Substring(0, idx);
            if (Sign(body) != text.Substring(idx + 5).Trim()) { error = "подпись не совпала (данные изменены)"; return false; }

            var m = new XcadMarker();
            foreach (string line in body.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                switch (k)
                {
                    case "PartNumber": m.PartNumber = v; break;
                    case "Version": int.TryParse(v, out int ver); m.Version = ver; break;
                    case "Hash": m.Hash = v; break;
                    case "ComponentId": int.TryParse(v, out int id); m.ComponentId = id; break;
                }
            }
            marker = m;
            return true;
        }

        public static bool TryWrite(IXDocument doc, XcadMarker marker, out string error)
        {
            error = null;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(Serialize(marker));
                using (Stream s = doc.OpenStream(StreamName, AccessType_e.Write))
                {
                    s.SetLength(0);
                    s.Write(bytes, 0, bytes.Length);
                }
                return true;
            }
            catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; return false; }
        }

        public static bool TryRead(IXDocument doc, out XcadMarker marker, out string error)
        {
            marker = null; error = null;
            try
            {
                using (Stream s = doc.OpenStream(StreamName, AccessType_e.Read))
                {
                    if (s == null) { error = "поток отсутствует (null)"; return false; }
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        if (ms.Length == 0) { error = "поток пустой"; return false; }
                        return TryDeserialize(Encoding.UTF8.GetString(ms.ToArray()), out marker, out error);
                    }
                }
            }
            catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; return false; }
        }
    }

    // ------------------------------------------------------------------
    // Фрагмент ViewModel. Добавьте в существующий класс VM taskpane
    // (или оставьте как отдельный класс и подключите к тестовой панели).
    // Нужно поле: private IXApplication _app;  (то, что у вас уже есть)
    // ------------------------------------------------------------------
    public partial class MarkerTestViewModel
    {
       
    }
}
