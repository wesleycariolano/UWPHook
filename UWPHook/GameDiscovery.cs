using System.IO;
using System.Reflection;
using System.Text.Json;

namespace UWPHook;

internal static class GameDiscovery
{
    private static readonly string[] ClassicGames = ["Microsoft.Minecraft", "Microsoft.MicrosoftSolitaireCollection", "Microsoft.MicrosoftMahjong",
        "Microsoft.MicrosoftMinesweeper", "Microsoft.MicrosoftJigsaw", "Microsoft.MicrosoftSudoku", "Microsoft.MicrosoftUltimateWordGames"];

    internal static bool IsGame(AppEntry app)
    {
        string name = app.PackageName;
        if (new[] { "Microsoft.GamingApp", "Microsoft.GamingServices", "Microsoft.XboxGamingOverlay", "Microsoft.XboxGameOverlay", "Microsoft.XboxIdentityProvider" }
            .Any(value => name.Equals(value, StringComparison.OrdinalIgnoreCase))) return false;
        return app.HasGameConfig || app.HasGameExplorer || app.UsesGameLaunchHelper
            || ClassicGames.Any(value => name.StartsWith(value, StringComparison.OrdinalIgnoreCase));
    }

    internal static List<AppEntry> Discover()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UWPHook.Resources.GetInstalledGames.ps1")!;
        using var reader = new StreamReader(stream);
        string json = ScriptManager.RunScript(reader.ReadToEnd()).Trim();
        var apps = JsonSerializer.Deserialize<List<AppEntry>>(json) ?? [];
        foreach (var app in apps)
        {
            app.IsGame = IsGame(app);
            if (AppManager.IsKnownApp(app.Aumid, out string name)) app.Name = name;
        }
        return apps.Where(app => !string.IsNullOrWhiteSpace(app.Aumid)).DistinctBy(app => app.Aumid).ToList();
    }
}
