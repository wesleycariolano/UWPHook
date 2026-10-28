using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UWPHook.LibraryDetails;

internal static class CompanionInstaller
{
    internal const string PluginName = "NativeGameLinkForSteam";
    internal const string PluginVersion = "5.0.0-uwphook.2";
    private const string Owner = "UWPHook.LibraryDetails";
    private static string Receipt(string steam) => Path.Combine(steam, "millennium", "config", "uwphook-library-details.json");
    internal static bool IsInstalled(string steam) => File.Exists(Receipt(steam))
        && File.Exists(Path.Combine(steam, "wsock32.dll"))
        && File.Exists(Path.Combine(steam, "millennium", "plugins", PluginName, "plugin.json"));

    internal static bool IsCurrent(string steam)
    {
        if (!IsInstalled(steam)) return false;
        try
        {
            using var receipt = JsonDocument.Parse(File.ReadAllText(Receipt(steam)));
            return receipt.RootElement.TryGetProperty("Owner", out var owner) && owner.GetString() == Owner
                && receipt.RootElement.TryGetProperty("PluginVersion", out var version) && version.GetString() == PluginVersion;
        }
        catch (JsonException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static bool IsEnabled(string steam)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(steam, "millennium", "config", "config.json")));
            return json.RootElement.GetProperty("plugins").GetProperty("enabledPlugins").EnumerateArray().Any(value => value.GetString() == PluginName);
        }
        catch (Exception error) when (error is JsonException or IOException or KeyNotFoundException or InvalidOperationException) { return false; }
    }

    internal static void InstallBundled(string steam)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var manifest = assembly.GetManifestResourceStream("UWPHook.Details.Components.json")
            ?? throw new InvalidOperationException("Os componentes da ficha não estão incluídos nesta compilação.");
        using var json = JsonDocument.Parse(manifest);
        using var host = assembly.GetManifestResourceStream("UWPHook.Details.Host.zip")!;
        using var plugin = assembly.GetManifestResourceStream("UWPHook.Details.Plugin.zip")!;
        Install(steam, Bytes(host), Bytes(plugin), json.RootElement.GetProperty("HostHash").GetString()!,
            json.RootElement.GetProperty("PluginHash").GetString()!, () => Process.GetProcessesByName("steam").Length > 0);
    }

    private static byte[] Bytes(Stream stream) { using var buffer = new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray(); }

    internal static void Install(string steam, byte[] hostArchive, byte[] pluginArchive,
        string hostHash, string pluginHash, Func<bool> steamRunning, Action<int>? beforeWrite = null)
    {
        if (steamRunning()) throw new InvalidOperationException("Feche completamente a Steam antes de ativar a ficha.");
        steam = Path.GetFullPath(steam);
        if (!File.Exists(Path.Combine(steam, "Steam.exe"))) throw new DirectoryNotFoundException("Pasta da Steam inválida.");
        VerifyHash(hostArchive, hostHash);
        VerifyHash(pluginArchive, pluginHash);
        var writes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        bool hasHost = File.Exists(Path.Combine(steam, "millennium", "lib", "millennium.dll"));
        if (hasHost && !File.Exists(Path.Combine(steam, "wsock32.dll")))
            throw new IOException("A instalação existente do Millennium está incompleta. Repare-a antes de ativar a ficha.");
        if (!hasHost && File.Exists(Path.Combine(steam, "wsock32.dll")))
            throw new IOException("Já existe um wsock32.dll nesta pasta. A instalação não irá substituí-lo.");
        bool owned = false;
        if (File.Exists(Receipt(steam)))
        {
            using var receipt = JsonDocument.Parse(File.ReadAllText(Receipt(steam)));
            owned = receipt.RootElement.GetProperty("Owner").GetString() == Owner;
        }
        string pluginRoot = Path.Combine(steam, "millennium", "plugins", PluginName);
        if (Directory.Exists(pluginRoot) && !owned)
            throw new IOException("Já existe uma instalação independente do NativeGameLink. Ela não será substituída pelo UWPHook.");
        // Validate both archives, even when an existing host is reused.
        var hostFiles = ReadArchive(hostArchive, "");
        if (!hostFiles.ContainsKey("wsock32.dll") || !hostFiles.ContainsKey("millennium/lib/millennium.dll"))
            throw new InvalidDataException("Pacote de interface incompleto.");
        if (!hasHost)
            foreach (var file in hostFiles)
            {
                if (file.Key != "wsock32.dll" && !file.Key.StartsWith("millennium/lib/", StringComparison.Ordinal)
                    && !file.Key.StartsWith("millennium/bin/", StringComparison.Ordinal))
                    throw new InvalidDataException("Arquivo inesperado no pacote de interface.");
                writes[SafePath(steam, file.Key)] = file.Value;
            }
        using (var license = Assembly.GetExecutingAssembly().GetManifestResourceStream("UWPHook.Details.Millennium.LICENSE")!)
            writes[SafePath(steam, "millennium/config/uwphook-licenses/Millennium.LICENSE")] = Bytes(license);
        var pluginFiles = ReadArchive(pluginArchive, PluginName + "/");
        if (!pluginFiles.ContainsKey("plugin.json") || !pluginFiles.ContainsKey("backend/main.lua")
            || !pluginFiles.ContainsKey(".millennium/Dist/index.js"))
            throw new InvalidDataException("Pacote da ficha incompleto.");
        using (var pluginManifest = JsonDocument.Parse(pluginFiles["plugin.json"]))
            if (pluginManifest.RootElement.GetProperty("name").GetString() != PluginName)
                throw new InvalidDataException("Identidade incorreta no pacote da ficha.");
        foreach (var file in pluginFiles) writes[SafePath(pluginRoot, file.Key)] = file.Value;
        string configPath = Path.Combine(steam, "millennium", "config", "config.json");
        var config = ReadConfig(configPath);
        var plugins = config["plugins"] as JsonObject;
        if (plugins == null)
        {
            if (config.ContainsKey("plugins")) throw new InvalidDataException("Configuração de plugins inválida.");
            config["plugins"] = plugins = new JsonObject();
        }
        var enabled = plugins["enabledPlugins"] as JsonArray;
        if (enabled == null)
        {
            if (plugins.ContainsKey("enabledPlugins")) throw new InvalidDataException("Lista de plugins inválida.");
            plugins["enabledPlugins"] = enabled = new JsonArray();
        }
        if (!enabled.Any(item => item?.GetValue<string>() == PluginName)) enabled.Add(PluginName);
        writes[configPath] = Encoding.UTF8.GetBytes(config.ToJsonString(new JsonSerializerOptions(JsonSerializerOptions.Default) { WriteIndented = true }));
        writes[Receipt(steam)] = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Owner, HostVersion = hasHost ? "existing" : "3.6.2", PluginVersion,
            Files = writes.Keys.Select(path => Path.GetRelativePath(steam, path)).ToArray()
        });
        if (steamRunning()) throw new InvalidOperationException("A Steam foi aberta durante a preparação. Feche-a e tente novamente.");
        Commit(steam, writes, beforeWrite);
    }

    internal static void Disable(string steam, Func<bool> steamRunning)
    {
        if (steamRunning()) throw new InvalidOperationException("Feche a Steam antes de desativar a ficha.");
        string path = Path.Combine(steam, "millennium", "config", "config.json");
        var config = ReadConfig(path);
        if (config["plugins"]?["enabledPlugins"] is not JsonArray enabled) return;
        for (int index = enabled.Count - 1; index >= 0; index--)
            if (enabled[index]?.GetValue<string>() == PluginName) enabled.RemoveAt(index);
        Commit(steam, new Dictionary<string, byte[]> { [path] = Encoding.UTF8.GetBytes(config.ToJsonString()) });
    }

    internal static Dictionary<string, byte[]> ReadArchive(byte[] bytes, string prefix)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        long size = 0;
        foreach (var entry in zip.Entries)
        {
            string name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(part => part is ".." or ".")
                || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("Caminho inseguro no pacote.");
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Raiz inesperada no pacote.");
            if (name.EndsWith('/')) continue;
            string relative = name[prefix.Length..];
            if (string.IsNullOrEmpty(relative) || files.ContainsKey(relative)) throw new InvalidDataException("Arquivo duplicado no pacote.");
            size += entry.Length;
            if (entry.Length > 32 * 1024 * 1024 || size > 96 * 1024 * 1024) throw new InvalidDataException("Pacote muito grande.");
            using var content = entry.Open();
            files[relative] = Bytes(content);
        }
        return files;
    }

    private static void VerifyHash(byte[] data, string expected)
    {
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(data)), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A verificação do componente falhou.");
    }

    private static JsonObject ReadConfig(string path) => File.Exists(path)
        ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new JsonException("Configuração inválida.") : new();

    private static string SafePath(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Caminho fora da instalação.");
        return path;
    }

    private static void Commit(string steam, IReadOnlyDictionary<string, byte[]> writes, Action<int>? beforeWrite = null)
    {
        var originals = new Dictionary<string, byte[]?>();
        foreach (string path in writes.Keys)
        {
            _ = SafePath(steam, Path.GetRelativePath(steam, path));
            if (Directory.Exists(path)) throw new IOException("Uma pasta ocupa o local de um arquivo do componente.");
            for (string? current = path; current != null; current = Path.GetDirectoryName(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("A instalação não segue links de arquivos ou pastas.");
                if (string.Equals(current, Path.GetFullPath(steam), StringComparison.OrdinalIgnoreCase)) break;
            }
            originals[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                throw new IOException("Um arquivo de destino está protegido contra gravação.");
        }
        string backup = Path.Combine(steam, "millennium", "config", "uwphook-backups", Guid.NewGuid().ToString("N"));
        foreach (var original in originals.Where(pair => pair.Value != null))
        {
            string path = SafePath(backup, Path.GetRelativePath(steam, original.Key));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, original.Value!);
        }
        var committed = new List<string>();
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var write in writes)
            {
                beforeWrite?.Invoke(committed.Count);
                string directory = Path.GetDirectoryName(write.Key)!;
                for (string? missing = directory; missing != null && !Directory.Exists(missing); missing = Path.GetDirectoryName(missing))
                    createdDirectories.Add(missing);
                Directory.CreateDirectory(directory);
                committed.Add(write.Key);
                File.WriteAllBytes(write.Key, write.Value);
            }
        }
        catch
        {
            foreach (string path in committed.AsEnumerable().Reverse())
                if (originals[path] is { } original) File.WriteAllBytes(path, original);
                else File.Delete(path);
            foreach (string directory in createdDirectories.OrderByDescending(path => path.Length))
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    _ = SafePath(steam, Path.GetRelativePath(steam, directory));
                    Directory.Delete(directory, false);
                }
            throw;
        }
    }
}
