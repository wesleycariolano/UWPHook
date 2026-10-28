using Force.Crc32;
using System.IO;
using System.Text;
using System.Text.Json;
using VDFParser;
using VDFParser.Models;

namespace UWPHook.LibraryDetails;

internal sealed record ImportGame(AppEntry App, SteamGameDetails Game);
internal sealed record ImportResult(string Backup, int Games, int Profiles);

// Prepares the shortcut and the companion's committed mapping together, before
// Steam is reopened. The extension reads the mapping directly at startup.
internal static class UnifiedImporter
{
    internal static VDFEntry[] BuildShortcuts(IEnumerable<VDFEntry> existing, IEnumerable<ImportGame> games, string launcher)
    {
        var shortcuts = existing.ToList();
        foreach (var plan in games)
        {
            if (string.IsNullOrWhiteSpace(plan.App.Aumid) || plan.Game.Id == 0) throw new ArgumentException("Jogo inválido.");
            var entry = shortcuts.FirstOrDefault(item => ShortcutIdentity.Matches(item, plan.App.Aumid, launcher));
            if (entry == null)
            {
                entry = new VDFEntry {
                    appid = unchecked((int)(Crc32Algorithm.Compute(Encoding.UTF8.GetBytes(plan.Game.Name + launcher)) | 0x80000000)),
                    Index = shortcuts.Count, AllowOverlay = 1, AllowDesktopConfig = 1,
                    Tags = ["Xbox"], ShortcutPath = "", DevkitGameID = "", Icon = "",
                };
                shortcuts.Add(entry);
            }
            // Keep identity, play history, visibility, tags and the user's icon.
            entry.AppName = plan.Game.Name;
            entry.Exe = launcher;
            entry.StartDir = Path.GetDirectoryName(launcher);
            entry.LaunchOptions = plan.App.Aumid + " " + plan.App.Executable;
            if (string.IsNullOrEmpty(entry.Icon) && File.Exists(plan.App.Icon)) entry.Icon = plan.App.Icon;
        }
        return shortcuts.ToArray();
    }

    internal static ImportResult Import(string steam, string launcher, IReadOnlyList<ImportGame> games,
        string stateDirectory, string appDirectory, Func<bool> steamRunning,
        IReadOnlyDictionary<string, byte[]>? artwork = null, Action<int>? beforeWrite = null)
    {
        if (steamRunning()) throw new InvalidOperationException("Aguarde o encerramento da Steam para adicionar os jogos.");
        if (games.Count == 0) throw new ArgumentException("Escolha um jogo para adicionar.");
        if (!File.Exists(Path.Combine(steam, "steam.exe"))) throw new DirectoryNotFoundException("Não foi possível encontrar a Steam.");
        var profiles = SteamManager.GetUsers(steam).Where(path => uint.TryParse(Path.GetFileName(path), out uint id) && id > 0).ToArray();
        if (profiles.Length == 0) throw new InvalidOperationException("Entre na Steam uma vez para criar a sua biblioteca.");
        var writes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var mappings = new Dictionary<uint, uint>();
        foreach (string profile in profiles)
        {
            string path = Path.Combine(profile, "config", "shortcuts.vdf");
            var existing = File.Exists(path) ? SteamManager.ReadShortcuts(profile) : [];
            var entries = BuildShortcuts(existing, games, launcher);
            writes[path] = VDFSerializer.Serialize(entries);
            foreach (var plan in games)
            {
                var entry = entries.Single(item => ShortcutIdentity.Matches(item, plan.App.Aumid, launcher));
                uint id = unchecked((uint)entry.appid);
                mappings[id] = plan.Game.Id;
                if (artwork == null) continue;
                foreach (string suffix in new[] { ".png", "p.png", "_hero.png", "_logo.png" })
                {
                    string target = Path.Combine(profile, "config", "grid", id + suffix);
                    // Keep artwork already customized in Steam.
                    if (!File.Exists(target) && artwork.TryGetValue(plan.App.Aumid + suffix, out var bytes)) writes[target] = bytes;
                }
            }
        }
        byte[] mappingBytes = Serialize(DetailsStore.MergeMappings(stateDirectory, mappings));
        writes[Path.Combine(stateDirectory, "mappings.json")] = mappingBytes;
        writes[Path.Combine(stateDirectory, "mappings.backup.json")] = mappingBytes;
        string selectionPath = Path.Combine(appDirectory, "steam-details.json");
        var selections = DetailsStore.Read<SteamGame>(selectionPath);
        foreach (var game in games) selections[game.App.Aumid] = new(game.Game.Id, game.Game.Name, game.Game.Release);
        writes[selectionPath] = Serialize(selections);
        string backup = Path.Combine(appDirectory, "backups", "import-" + Guid.NewGuid().ToString("N"));
        Commit(writes, backup, steamRunning, beforeWrite);
        return new(backup, games.Count, profiles.Length);
    }

    private static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions { WriteIndented = true });

    private static void Commit(Dictionary<string, byte[]> writes, string backup, Func<bool> steamRunning, Action<int>? beforeWrite)
    {
        var originals = writes.ToDictionary(pair => pair.Key, pair => File.Exists(pair.Key) ? File.ReadAllBytes(pair.Key) : null);
        Directory.CreateDirectory(backup);
        int index = 0;
        var manifest = new Dictionary<string, string>();
        foreach (var pair in originals)
        {
            string name = (index++).ToString("D3") + ".bak";
            manifest[pair.Key] = pair.Value == null ? "new" : name;
            if (pair.Value != null) File.WriteAllBytes(Path.Combine(backup, name), pair.Value);
        }
        File.WriteAllBytes(Path.Combine(backup, "manifest.json"), Serialize(manifest));
        var committed = new List<string>();
        try
        {
            foreach (var pair in writes)
            {
                if (steamRunning()) throw new IOException("A Steam foi aberta durante a importação. Tente novamente.");
                beforeWrite?.Invoke(committed.Count);
                Directory.CreateDirectory(Path.GetDirectoryName(pair.Key)!);
                committed.Add(pair.Key);
                File.WriteAllBytes(pair.Key, pair.Value);
            }
        }
        catch
        {
            foreach (string path in committed.AsEnumerable().Reverse())
                if (originals[path] is { } bytes) File.WriteAllBytes(path, bytes);
                else File.Delete(path);
            throw;
        }
    }
}
