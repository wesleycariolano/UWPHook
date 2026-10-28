using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UWPHook.LibraryDetails;

internal sealed record SteamGame(uint Id, string Name, string Release = "");
internal sealed record SteamGameDetails(uint Id, string Name, string Description, string Developers,
    string Publishers, string Release, string Languages, string Header = "", string Background = "");

internal sealed class SteamCatalog : IDisposable
{
    private readonly HttpClient client;
    internal SteamCatalog(HttpMessageHandler? handler = null)
    {
        client = handler == null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(20);
    }

    internal async Task<SteamGame[]> Search(string title)
    {
        if (TryAppId(title, out uint appId))
        {
            var game = await Details(appId);
            return [new(game.Id, game.Name, game.Release)];
        }
        string query = Regex.Replace(Regex.Replace(title, @"[^\p{L}\p{N}\s]", " "), @"\s+", " ").Trim();
        if (query.Length == 0) return [];
        using var response = await client.GetAsync("https://store.steampowered.com/api/storesearch/?term="
            + Uri.EscapeDataString(query) + "&l=brazilian&cc=BR");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!json.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return [];
        return items.EnumerateArray().Where(item => item.TryGetProperty("id", out var id) && id.TryGetUInt32(out _))
            .Select(item => new SteamGame(item.GetProperty("id").GetUInt32(), Text(item, "name")))
            .Where(game => game.Id > 0 && !string.IsNullOrWhiteSpace(game.Name)).ToArray();
    }

    internal static bool TryAppId(string input, out uint id)
    {
        if (uint.TryParse(input.Trim(), out id) && id > 0) return true;
        id = 0;
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.Equals(uri.Host, "store.steampowered.com", StringComparison.OrdinalIgnoreCase)) return false;
        var match = Regex.Match(uri.AbsolutePath, @"^/app/(\d+)(?:/|$)");
        return match.Success && uint.TryParse(match.Groups[1].Value, out id) && id > 0;
    }

    internal async Task<SteamGameDetails> Details(uint appId)
    {
        if (appId == 0) throw new ArgumentOutOfRangeException(nameof(appId));
        using var response = await client.GetAsync($"https://store.steampowered.com/api/appdetails?appids={appId}&l=brazilian&cc=BR");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!json.RootElement.TryGetProperty(appId.ToString(), out var result)
            || !result.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True
            || !result.TryGetProperty("data", out var data))
            throw new InvalidDataException("A Steam não retornou uma ficha para esse jogo.");
        if (!data.TryGetProperty("steam_appid", out var id) || id.GetUInt32() != appId)
            throw new InvalidDataException("A ficha retornada não corresponde ao jogo escolhido.");
        string release = data.TryGetProperty("release_date", out var date) ? Text(date, "date") : "";
        return new(appId, PlainText(Text(data, "name")), PlainText(Text(data, "short_description")),
            List(data, "developers"), List(data, "publishers"), release, PlainText(Text(data, "supported_languages")),
            Text(data, "header_image"), Text(data, "background_raw"));
    }

    internal static string PlainText(string html) => WebUtility.HtmlDecode(Regex.Replace(
        Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase), "<[^>]+>", "")).Trim();
    private static string Text(JsonElement parent, string field) =>
        parent.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static string List(JsonElement parent, string field) => parent.TryGetProperty(field, out var value)
        && value.ValueKind == JsonValueKind.Array ? string.Join(", ", value.EnumerateArray().Select(item => item.GetString())) : "";
    public void Dispose() => client.Dispose();
}
