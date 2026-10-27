using System.IO;
using VDFParser.Models;

namespace UWPHook;

internal static class ShortcutIdentity
{
    internal static bool Matches(VDFEntry shortcut, string aumid, string currentLauncher)
    {
        if (string.IsNullOrWhiteSpace(shortcut.LaunchOptions)) return false;
        string originalAumid = shortcut.LaunchOptions.Split(' ', 2)[0];
        if (!string.Equals(originalAumid, aumid, StringComparison.Ordinal)) return false;
        string launcher = (shortcut.Exe ?? "").Trim('"');
        return string.Equals(launcher, currentLauncher.Trim('"'), StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(launcher), "UWPHook.exe", StringComparison.OrdinalIgnoreCase);
    }

    internal static void PreserveLibraryIdentity(VDFEntry existing, VDFEntry replacement)
    {
        replacement.Index = existing.Index;
        if (existing.appid != 0) replacement.appid = existing.appid;
        replacement.LastPlayTime = existing.LastPlayTime;
    }

    internal static string ArtworkFileName(string fileName, IReadOnlyDictionary<string, string> ids)
    {
        int length = 0;
        while (length < fileName.Length && char.IsAsciiDigit(fileName[length])) length++;
        string id = fileName[..length];
        return ids.TryGetValue(id, out var existingId) ? existingId + fileName[length..] : fileName;
    }
}
