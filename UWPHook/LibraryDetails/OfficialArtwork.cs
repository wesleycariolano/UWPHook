using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;

namespace UWPHook.LibraryDetails;

internal static class OfficialArtwork
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };
    internal static string Asset(uint id, string file) => $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{id}/{file}";

    internal static async Task<Dictionary<string, byte[]>> Download(ImportGame plan)
    {
        var results = new Dictionary<string, byte[]>();
        var slots = new[] { (".png", "header.jpg"), ("p.png", "library_600x900.jpg"), ("_hero.png", "library_hero.jpg"), ("_logo.png", "logo.png") };
        foreach (var (suffix, file) in slots)
        {
            string url = suffix == ".png" && !string.IsNullOrEmpty(plan.Game.Header) ? plan.Game.Header : Asset(plan.Game.Id, file);
            try
            {
                using var stream = await Client.GetStreamAsync(url);
                using var image = new Bitmap(stream);
                using var output = new MemoryStream();
                image.Save(output, ImageFormat.Png);
                results[plan.App.Aumid + suffix] = output.ToArray();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException) { }
        }
        return results;
    }
}
