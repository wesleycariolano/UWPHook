using System.IO;
using System.Text.Json;

namespace UWPHook.SteamGridDb;

// AUMID identifies the installed Xbox app even when its display name changes.
internal sealed class ArtworkSelectionStore
{
    private readonly string path;
    internal ArtworkSelectionStore(string path) => this.path = path;

    internal static ArtworkSelectionStore Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Briano", "UWPHook", "library-metadata.json"));

    internal Dictionary<string, ArtworkSelection> Load()
    {
        if (!File.Exists(path)) return new();
        // Never silently overwrite a corrupt file with an empty dictionary.
        return JsonSerializer.Deserialize<Dictionary<string, ArtworkSelection>>(
            File.ReadAllText(path)) ?? throw new JsonException("Invalid artwork selection file.");
    }

    internal ArtworkSelection? Find(string aumid) => Load().GetValueOrDefault(aumid);

    internal void Save(string aumid, ArtworkSelection selection)
    {
        if (string.IsNullOrWhiteSpace(aumid) || selection.GameId <= 0)
            throw new ArgumentException("A valid app identity and SteamGridDB game are required.");
        var selections = Load();
        selections[aumid] = selection;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(selections,
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, path, overwrite: true);
    }
}

internal sealed record ArtworkSelection(int GameId, string Name, string ReleaseYear, bool UseTitle = false);

