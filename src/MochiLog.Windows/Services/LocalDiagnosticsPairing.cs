using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
namespace MochiLog_Windows.Services;
public static class LocalDiagnosticsPairing
{
    public static byte[] Control(PairedPhone phone)
    {
        static byte[] Unavailable() => JsonSerializer.SerializeToUtf8Bytes(new { type = "local-diagnostics-pairing", version = 1, unavailable = true });
        try {
            if (!System.Text.RegularExpressions.Regex.IsMatch(phone.Udid, "^[A-Fa-f0-9-]{16,64}$")) return Unavailable();
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".pymobiledevice3", "remote_" + phone.Udid + ".plist");
            if (!File.Exists(path) || new FileInfo(path).Length > 65536) return Unavailable();
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null, MaxCharactersInDocument = 65536 });
            var parts = XDocument.Load(reader).Root?.Element("dict")?.Elements().ToArray() ?? [];
            var values = new Dictionary<string, XElement>();
            for (var i = 0; i + 1 < parts.Length; i += 2) {
                if (parts[i].Name != "key") return Unavailable();
                values[parts[i].Value] = parts[i + 1];
            }
            if (!values.TryGetValue("public_key", out var pub) || pub.Name != "data" ||
                !values.TryGetValue("private_key", out var priv) || priv.Name != "data" ||
                Convert.FromBase64String(pub.Value).Length != 32 || Convert.FromBase64String(priv.Value).Length != 32) return Unavailable();
            // UUID v3 is upstream's host identifier, not a password/hash scheme.
            var ns = Convert.FromHexString("6ba7b8109dad11d180b400c04fd430c8");
            var digest = MD5.HashData(ns.Concat(Encoding.UTF8.GetBytes(Environment.MachineName)).ToArray());
            digest[6] = (byte)((digest[6] & 15) | 0x30); digest[8] = (byte)((digest[8] & 63) | 0x80);
            var id = new Guid(digest, bigEndian: true).ToString("D").ToUpperInvariant();
            foreach (var key in new[] { "identifier", "host_identifier" }) {
                if (values.TryGetValue(key, out var identifier) && identifier.Name == "string") { id = identifier.Value; break; }
            }
            var pair = new XElement("dict", new XElement("key", "public_key"), new XElement(pub),
                new XElement("key", "private_key"), new XElement(priv), new XElement("key", "identifier"), new XElement("string", id));
            if ((values.GetValueOrDefault("alt_irk") ?? values.GetValueOrDefault("peer_alt_irk")) is { Name.LocalName: "data" } irk) {
                pair.Add(new XElement("key", "alt_irk"), new XElement(irk));
            }
            var bytes = Encoding.UTF8.GetBytes(new XDocument(new XElement("plist", new XAttribute("version", "1.0"), pair)).ToString());
            return JsonSerializer.SerializeToUtf8Bytes(new { type = "local-diagnostics-pairing", version = 1,
                expectedUDID = phone.Udid, physicalDeviceID = phone.PhysicalDeviceId.ToString("D").ToUpperInvariant(), pairing = Convert.ToBase64String(bytes) });
        } catch (Exception e) when (e is IOException or XmlException or FormatException or UnauthorizedAccessException) { return Unavailable(); }
    }
}
