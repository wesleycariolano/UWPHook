using System.IO;
using System.Text.Json;

namespace UWPHook;

internal sealed class UiPreferences
{
    public bool ShowLinkedState { get; set; } = true;
    private static string FilePath => Path.Combine(AppIdentity.DataDirectory, "interface.json");
    internal static UiPreferences Current { get; } = Load();
    private static UiPreferences Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch (Exception error) when (error is IOException or JsonException) { return new(); }
    }
    internal void Save()
    {
        Directory.CreateDirectory(AppIdentity.DataDirectory);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this)); File.Move(temp, FilePath, true);
    }
}
