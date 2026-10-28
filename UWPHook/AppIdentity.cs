using System.IO;

namespace UWPHook;

internal static class AppIdentity
{
    internal const string Name = "sGs-app";
    internal static string Version => typeof(AppIdentity).Assembly.GetName().Version!.ToString(3);
    // Keep existing selections, backups and launch identities across the rename.
    internal static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook");
}
