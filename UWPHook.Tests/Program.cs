using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using UWPHook;
using UWPHook.SteamGridDb;
using VDFParser.Models;

if (args.Length == 2 && args[0] == "--render")
{
    Exception? renderError = null;
    var thread = new Thread(() =>
    {
        try
        {
            var app = new UWPHook.App();
            app.InitializeComponent();
            var picker = new ArtworkPickerWindow("Call of Duty — choose the right edition", "test-key");
            var grid = (System.Windows.Controls.Grid)picker.Content;
            grid.Background = System.Windows.Media.Brushes.White;
            ((System.Windows.Controls.TextBox)picker.FindName("SearchText")).Text = "Modern Warfare";
            var results = (System.Windows.Controls.DataGrid)picker.FindName("Results");
            results.ItemsSource = new[] {
                new GameResponse { Id = 1001, Name = "Call of Duty: Modern Warfare 3", ReleaseDate = 1320710400 },
                new GameResponse { Id = 1002, Name = "Call of Duty: Modern Warfare III", ReleaseDate = 1699574400 }
            };
            results.SelectedIndex = 1;
            ((System.Windows.Controls.TextBlock)picker.FindName("Status")).Text = "Preview with simulated search results. Choose the correct title and year.";
            grid.Measure(new System.Windows.Size(700, 450));
            grid.Arrange(new System.Windows.Rect(0, 0, 700, 450));
            grid.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(700, 450, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(grid);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var output = File.Create(args[1]);
            encoder.Save(output);
        }
        catch (Exception exception) { renderError = exception; }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start(); thread.Join();
    if (renderError != null) throw renderError;
    Console.WriteLine("Picker XAML rendered without launching games or editing Steam.");
    return;
}

int passed = 0;
void Check(bool value, string name)
{
    if (!value) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
string folder = Path.Combine(Environment.GetEnvironmentVariable("UWPHOOK_TEST_ROOT") ?? Path.GetTempPath(), "UWPHook-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
string file = Path.Combine(folder, "library-metadata.json");
var store = new ArtworkSelectionStore(file);
Check(store.Find("xbox!cod") == null, "new app has no implicit artwork match");
store.Save("xbox!cod", new(2023, "Modern Warfare III", "2023"));
store.Save("xbox!other", new(8, "Other game", "2020"));
Check(new ArtworkSelectionStore(file).Find("xbox!cod")?.GameId == 2023, "selection survives restart by AUMID");
store.Save("xbox!cod", new(2024, "Corrected title", "2023"));
Check(store.Load().Count == 2 && store.Find("xbox!other")?.GameId == 8, "correction preserves other games");
File.WriteAllText(file, "broken json");
try { store.Save("xbox!cod", new(1, "Bad", "")); throw new Exception("corruption was ignored"); }
catch (JsonException) { Check(File.ReadAllText(file) == "broken json", "corrupt selection file is not overwritten"); }

var handler = new FakeHandler(HttpStatusCode.OK, "{\"success\":true,\"data\":[{\"id\":1,\"name\":\"Modern Warfare 3\",\"release_date\":1320710400},{\"id\":2,\"name\":\"Modern Warfare III\",\"release_date\":1699574400}]}");
using (var api = new SteamGridDbApi("test-key", handler))
{
    var games = await api.SearchGame("  COD / MW3? #®  ");
    Check(games.Length == 2 && games[0].ReleaseYear == "2011" && games[1].ReleaseYear == "2023", "API retains ambiguous editions and release years");
    Check(handler.RequestUri!.AbsoluteUri.EndsWith("COD%20%2F%20MW3%3F%20%23%C2%AE"), "search text is escaped as one URL segment");
}
using (var api = new SteamGridDbApi("test-key", new FakeHandler(HttpStatusCode.OK, "{\"success\":true,\"data\":[]}")))
    Check((await api.SearchGame("missing")).Length == 0, "empty search is safe");
foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests })
{
    using var api = new SteamGridDbApi("test-key", new FakeHandler(code, "{}"));
    try { await api.SearchGame("cod"); throw new Exception("HTTP failure was ignored"); }
    catch (HttpRequestException) { Check(true, "HTTP failure is surfaced: " + code); }
}
Check(new GameResponse { ReleaseDate = long.MaxValue }.ReleaseYear == "Unknown", "invalid release timestamp is safe");
var old = new VDFEntry { Index = 4, appid = -123456, Exe = "\"C:\\Old\\UWPHook.exe\"", AppName = "Wrong MW3 name", LaunchOptions = "xbox!cod cod.exe", LastPlayTime = 75 };
Check(ShortcutIdentity.Matches(old, "xbox!cod", "D:\\New\\UWPHook.exe"), "rename and fork path still match same Xbox app");
Check(!ShortcutIdentity.Matches(old, "xbox!cod2", "D:\\New\\UWPHook.exe"), "other Xbox app cannot be replaced");
old.Exe = "C:\\Other.exe";
Check(!ShortcutIdentity.Matches(old, "xbox!cod", "D:\\New\\UWPHook.exe"), "unrelated launcher cannot be replaced");
var replacement = new VDFEntry { Index = 10, appid = 900, LastPlayTime = 999 };
ShortcutIdentity.PreserveLibraryIdentity(old, replacement);
Check(replacement.Index == 4 && replacement.appid == -123456 && replacement.LastPlayTime == 75, "updated shortcut retains library identity and history");
var ids = new Dictionary<string, string> { ["900"] = "4294843840" };
Check(ShortcutIdentity.ArtworkFileName("900_hero.png", ids) == "4294843840_hero.png" && ShortcutIdentity.ArtworkFileName("900p.png", ids) == "4294843840p.png", "new artwork follows retained shortcut ID");
replacement.AppName = "Modern Warfare III";
replacement.Exe = "D:\\New\\UWPHook.exe";
replacement.LaunchOptions = "xbox!cod cod.exe";
replacement.Tags = ["Xbox"];
var unrelated = new VDFEntry { AppName = "Other app", Exe = "C:\\Other.exe", Tags = ["Existing"], appid = 501 };
string vdf = Path.Combine(folder, "shortcuts.vdf");
File.WriteAllBytes(vdf, VDFParser.VDFSerializer.Serialize([replacement, unrelated]));
var roundtrip = VDFParser.VDFParser.Parse(vdf);
Check(roundtrip.Length == 2 && roundtrip[0].appid == -123456 && roundtrip[0].AppName == "Modern Warfare III" && roundtrip[1].AppName == "Other app" && roundtrip[1].appid == 501, "binary VDF roundtrip retains renamed and unrelated shortcuts");
Console.WriteLine($"{passed} checks passed. No live Steam files were modified.");

sealed class FakeHandler(HttpStatusCode code, string body) : HttpMessageHandler
{
    internal Uri? RequestUri;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}



