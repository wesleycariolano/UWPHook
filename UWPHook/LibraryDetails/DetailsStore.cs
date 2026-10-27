using System.IO;
using System.Text.Json;

namespace UWPHook.LibraryDetails;

internal sealed class DetailsStore
{
    private readonly string path;
    internal DetailsStore(string path) => this.path = path;
    internal static DetailsStore Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook", "steam-details.json"));
    internal SteamGame? Find(string aumid) => Read<SteamGame>(path).GetValueOrDefault(aumid);
    internal void Save(string aumid, SteamGame game)
    {
        if (string.IsNullOrWhiteSpace(aumid) || game.Id == 0) throw new ArgumentException("Seleção inválida.");
        var data = Read<SteamGame>(path);
        data[aumid] = game;
        Write(path, data);
    }

    internal static void Link(string directory, IReadOnlyDictionary<uint, uint> mappings)
    {
        if (mappings.Count == 0) return;
        string primary = Path.Combine(directory, "mappings.json");
        string backup = Path.Combine(directory, "mappings.backup.json");
        var existing = Read<string>(primary);
        // NativeGameLink can recover an empty primary from its backup. Preserve
        // those records before writing new mappings instead of discarding them.
        if (existing.Count == 0 && File.Exists(backup)) existing = Read<string>(backup);
        foreach (var pair in mappings)
        {
            if (pair.Key == 0 || pair.Value == 0) throw new ArgumentException("Identificador inválido.");
            existing[pair.Key.ToString()] = pair.Value.ToString();
        }
        Write(primary, existing);
        Write(backup, existing);
    }

    internal static Dictionary<string, T> Read<T>(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<Dictionary<string, T>>(File.ReadAllText(path)) ?? throw new JsonException("Configuração inválida.") : new();

    private static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }

    internal static string CompanionState => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NativeGameLinkForSteam");
}
