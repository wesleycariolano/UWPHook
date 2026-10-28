using System.IO;
using VDFParser.Models;

namespace UWPHook.LibraryDetails;

internal static class DetailsLinker
{
    internal static void Apply(IEnumerable<VDFEntry> shortcuts, IEnumerable<AppEntry> apps)
    {
        var mappings = new Dictionary<uint, uint>();
        foreach (var app in apps)
        {
            var selection = DetailsStore.Default.Find(app.Aumid);
            if (selection == null) continue;
            foreach (var shortcut in shortcuts.Where(shortcut => ShortcutIdentity.Matches(shortcut, app.Aumid, Environment.ProcessPath ?? "UWPHook.exe")))
                mappings[unchecked((uint)shortcut.appid)] = selection.Id;
        }
        DetailsStore.Link(DetailsStore.CompanionState, mappings);
    }

    internal static bool ApplyExisting(AppEntry app)
    {
        string steam = SteamManager.GetSteamFolder();
        if (!Directory.Exists(steam)) return false;
        bool found = false;
        foreach (string user in SteamManager.GetUsers(steam))
        {
            var shortcuts = SteamManager.ReadShortcuts(user);
            found |= shortcuts.Any(shortcut => ShortcutIdentity.Matches(shortcut, app.Aumid, Environment.ProcessPath ?? "UWPHook.exe"));
            Apply(shortcuts, [app]);
        }
        return found;
    }
}
