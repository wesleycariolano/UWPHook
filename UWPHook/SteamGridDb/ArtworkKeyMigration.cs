using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace UWPHook.SteamGridDb;

internal static class ArtworkKeyMigration
{
    internal static string? ResolveKey(string currentKey, string companyFolder) =>
        !string.IsNullOrWhiteSpace(currentKey) ? currentKey : FindPreviousKey(companyFolder);

    // .NET gives each executable path its own settings directory. Moving the
    // application or installing a fork therefore does not carry its key over.
    internal static string? FindPreviousKey(string companyFolder)
    {
        if (!Directory.Exists(companyFolder)) return null;
        var candidates = Directory.EnumerateDirectories(companyFolder, "UWPHook_*")
            .SelectMany(folder => Directory.EnumerateDirectories(folder))
            .Select(folder => Path.Combine(folder, "user.config"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc);

        foreach (string file in candidates)
        {
            try
            {
                using var reader = XmlReader.Create(file, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
                var section = XDocument.Load(reader).Root?.Element("userSettings")
                    ?.Element("UWPHook.Properties.Settings");
                var key = section?.Elements("setting")
                    .FirstOrDefault(setting => (string?)setting.Attribute("name") == "SteamGridDbApiKey")
                    ?.Element("value")?.Value.Trim();
                if (!string.IsNullOrWhiteSpace(key)) return key;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
            {
                // Ignore damaged/inaccessible versions. Never log configuration
                // contents, file exceptions or the key itself.
            }
        }
        return null;
    }
}
