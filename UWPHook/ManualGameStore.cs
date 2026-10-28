using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace UWPHook;

internal sealed class ManualGameStore(string path)
{
    internal static ManualGameStore Default { get; } = new(Path.Combine(AppIdentity.DataDirectory, "folder-games.json"));
    internal List<AppEntry> Load() => File.Exists(path) ? JsonSerializer.Deserialize<List<AppEntry>>(File.ReadAllText(path)) ?? throw new JsonException("Lista de jogos por pasta inválida.") : [];
    internal void Save(AppEntry game)
    {
        var games = Load(); games.RemoveAll(item => item.Aumid == game.Aumid); games.Add(game);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(games, new JsonSerializerOptions { IgnoreReadOnlyProperties = true, WriteIndented = true }));
        File.Move(temp, path, true);
    }
    internal static string Identity(string folder) => "folder:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(folder).TrimEnd('\\').ToUpperInvariant())))[..24];
    internal static bool Inside(string root, string path) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    internal static AppEntry? RegisteredGame(string folder, IEnumerable<AppEntry> registered)
    {
        string root = Path.GetFullPath(folder).TrimEnd('\\');
        var configs = new[] { Path.Combine(root, "MicrosoftGame.Config"), Path.Combine(root, "Content", "MicrosoftGame.Config") };
        string? config = configs.FirstOrDefault(File.Exists);
        string? package = null;
        if (config != null)
        {
            var xml = XDocument.Load(config);
            package = xml.Descendants().FirstOrDefault(node => node.Name.LocalName == "Identity")?.Attribute("Name")?.Value;
        }
        return registered.FirstOrDefault(app =>
            ((!string.IsNullOrEmpty(package) && package.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrEmpty(app.InstallDirectory) && (app.InstallDirectory.TrimEnd('\\').Equals(root, StringComparison.OrdinalIgnoreCase) || Inside(root, app.InstallDirectory)))));
    }

    internal static bool IsGameExecutable(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)
            && !new[] { "unins", "setup", "crashreport", "crashhandler", "dxsetup", "vcredist", "updater", "easyanticheat" }.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
    internal static List<string> Executables(string folder)
    {
        var result = new List<string>();
        var queue = new Queue<(string Path, int Depth)>(); queue.Enqueue((Path.GetFullPath(folder), 0));
        while (queue.Count > 0 && result.Count < 200)
        {
            var (directory, depth) = queue.Dequeue();
            try
            {
                result.AddRange(Directory.EnumerateFiles(directory, "*.exe").Where(IsGameExecutable).Take(200 - result.Count));
                if (depth >= 4) continue;
                foreach (string child in Directory.EnumerateDirectories(directory))
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0
                        && !new[] { "redist", "__installer", ".git", "node_modules" }.Contains(Path.GetFileName(child).ToLowerInvariant())) queue.Enqueue((child, depth + 1));
            }
            catch (Exception error) when (error is UnauthorizedAccessException or IOException) { }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }
    internal static AppEntry Create(string folder, string executable)
    {
        folder = Path.GetFullPath(folder); executable = Path.GetFullPath(executable);
        if (!Directory.Exists(folder) || !File.Exists(executable) || !Inside(folder, executable) || !IsGameExecutable(executable))
            throw new ArgumentException("Selecione o executável do jogo dentro da pasta escolhida.");
        if (File.Exists(Path.Combine(folder, "MicrosoftGame.Config")) || File.Exists(Path.Combine(folder, "Content", "MicrosoftGame.Config")))
            throw new InvalidOperationException("Este é um jogo Xbox. Abra ou repare o jogo no aplicativo Xbox para registrar sua inicialização no Windows e tente novamente.");
        return new AppEntry { Name = new DirectoryInfo(folder).Name, Aumid = Identity(folder), Executable = "--folder", ExecutablePath = executable,
            InstallDirectory = folder, LaunchKind = "win32", IsGame = true };
    }
}
