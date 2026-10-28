namespace UWPHook.SteamGridDb;

internal class GameResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [Newtonsoft.Json.JsonProperty("release_date")]
    public long? ReleaseDate { get; set; }
    public string ReleaseYear => ReleaseDate is > 0 and <= 253402300799
        ? System.DateTimeOffset.FromUnixTimeSeconds(ReleaseDate.Value).Year.ToString()
        : "Unknown";
}
