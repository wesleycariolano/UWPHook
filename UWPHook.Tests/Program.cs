using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using UWPHook;
using UWPHook.SteamGridDb;
using UWPHook.LibraryDetails;
using System.IO.Compression;
using System.Security.Cryptography;
using VDFParser.Models;

if (args.Length >= 2 && args[0] == "--render-main")
{
    Exception? renderError = null;
    var thread = new Thread(() =>
    {
        try
        {
            var application = new UWPHook.App(); application.InitializeComponent();
            var window = new GamesWindow(true);
            window.PreparePreview(args.ElementAtOrDefault(2) ?? "");
            var content = (System.Windows.Controls.Grid)window.Content;
            content.Measure(new System.Windows.Size(1320, 860)); content.Arrange(new System.Windows.Rect(0, 0, 1320, 860)); content.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1320, 860, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var output = File.Create(args[1]); encoder.Save(output);
        }
        catch (Exception error) { renderError = error; }
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    if (renderError != null) throw renderError;
    Console.WriteLine("Unified interface rendered with demonstration data; no Steam files changed.");
    return;
}

if (args.Length == 2 && args[0] == "--render-details")
{
    Exception? renderError = null;
    var thread = new Thread(() =>
    {
        try
        {
            var application = new UWPHook.App(); application.InitializeComponent();
            var window = new LibraryDetailsWindow(new AppEntry { Name = "Call of Duty: Modern Warfare III", Aumid = "preview!cod" });
            var grid = (System.Windows.Controls.Grid)window.Content;
            grid.Background = System.Windows.Media.Brushes.White;
            ((System.Windows.Controls.DataGrid)window.FindName("Results")).ItemsSource = new[] { new SteamGame(3595270, "Call of Duty®: Modern Warfare® III") };
            ((System.Windows.Controls.TextBlock)window.FindName("GameTitle")).Text = "Call of Duty®: Modern Warfare® III";
            ((System.Windows.Controls.TextBlock)window.FindName("Description")).Text = "Descrição oficial do jogo, com a edição escolhida e as informações da Steam em português. O executável do Xbox continua sendo usado para jogar.";
            ((System.Windows.Controls.TextBlock)window.FindName("Credits")).Text = "Desenvolvimento: Sledgehammer Games, Treyarch e outros\nPublicação: Activision\nLançamento: 10/nov./2023";
            ((System.Windows.Controls.TextBlock)window.FindName("Languages")).Text = "Idiomas informados pela Steam:\nPortuguês (Brasil), Inglês e outros. * Idiomas com suporte total de áudio.";
            ((System.Windows.Controls.TextBlock)window.FindName("Status")).Text = "Prévia da configuração integrada; nenhum componente foi ativado nesta imagem.";
            ((System.Windows.Controls.Button)window.FindName("SaveButton")).IsEnabled = true;
            grid.Measure(new System.Windows.Size(810, 680)); grid.Arrange(new System.Windows.Rect(0, 0, 810, 680)); grid.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(810, 680, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(grid);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var output = File.Create(args[1]); encoder.Save(output);
        }
        catch (Exception exception) { renderError = exception; }
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    if (renderError != null) throw renderError;
    Console.WriteLine("Integrated details window rendered without installing components.");
    return;
}

if (args.Length == 1 && args[0] == "--check-catalog")
{
    using var catalog = new SteamCatalog();
    var games = await catalog.Search("Call of Duty®: Modern Warfare® III");
    if (!games.Any(game => game.Id == 3595270)) throw new Exception("MWIII was not found in the current Steam catalog.");
    var game = await catalog.Details(3595270);
    if (string.IsNullOrWhiteSpace(game.Description) || !game.Languages.Contains("Português")) throw new Exception("Localized details were not returned.");
    Console.WriteLine($"Live Steam catalog verified: {game.Name}, {game.Id}; Portuguese description/languages available.");
    return;
}

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
string migrationRoot = Path.Combine(folder, "settings");
Check(ArtworkKeyMigration.FindPreviousKey(migrationRoot) == null, "no prior settings requires no migration");
string oldFolder = Path.Combine(migrationRoot, "UWPHook_Url_old", "2.14.3.0");
string newFolder = Path.Combine(migrationRoot, "UWPHook_Url_new", "2.15.0.0");
Directory.CreateDirectory(oldFolder);
Directory.CreateDirectory(newFolder);
string configTemplate = "<configuration><userSettings><UWPHook.Properties.Settings><setting name='SteamGridDbApiKey'><value>{0}</value></setting></UWPHook.Properties.Settings></userSettings></configuration>";
File.WriteAllText(Path.Combine(oldFolder, "user.config"), string.Format(configTemplate, "previous-version-test-key"));
File.WriteAllText(Path.Combine(newFolder, "user.config"), string.Format(configTemplate, ""));
Check(ArtworkKeyMigration.ResolveKey("", migrationRoot) == "previous-version-test-key", "key migrates across version and executable path");
Check(ArtworkKeyMigration.ResolveKey("current-key", migrationRoot) == "current-key", "configured current key is never overwritten");
File.WriteAllText(Path.Combine(newFolder, "user.config"), "invalid XML");
Check(ArtworkKeyMigration.FindPreviousKey(migrationRoot) == "previous-version-test-key", "damaged newer settings do not hide an older key");
File.WriteAllText(Path.Combine(newFolder, "user.config"), "<!DOCTYPE config [<!ENTITY external SYSTEM 'file:///not-allowed'>]><configuration>&external;</configuration>");
Check(ArtworkKeyMigration.FindPreviousKey(migrationRoot) == "previous-version-test-key", "migration rejects XML external entities");

byte[] Archive(params (string Name, string Content)[] files)
{
    using var buffer = new MemoryStream();
    using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        foreach (var item in files)
        {
            var entry = zip.CreateEntry(item.Name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(item.Content);
        }
    return buffer.ToArray();
}
string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); }
    catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or JsonException or InvalidOperationException) { rejected = true; }
    Check(rejected, name);
}
string fakeSteam = Path.Combine(folder, "Steam");
Directory.CreateDirectory(fakeSteam);
File.WriteAllText(Path.Combine(fakeSteam, "Steam.exe"), "test fixture; never executed");
byte[] host = Archive(("wsock32.dll", "fake-proxy"), ("millennium/lib/millennium.dll", "fake-host"));
byte[] plugin = Archive(("NativeGameLinkForSteam/plugin.json", "{\"name\":\"NativeGameLinkForSteam\"}"),
    ("NativeGameLinkForSteam/backend/main.lua", "-- test"), ("NativeGameLinkForSteam/.millennium/Dist/index.js", "// test"));
Reject(() => CompanionInstaller.Install(fakeSteam, host, plugin, Hash(host), Hash(plugin), () => true), "integration refuses to install while Steam is running");
Reject(() => CompanionInstaller.Install(fakeSteam, host, plugin, new string('0', 64), Hash(plugin), () => false), "component hash mismatch prevents installation");
Check(!File.Exists(Path.Combine(fakeSteam, "wsock32.dll")), "failed preflight does not modify Steam files");
bool injectionReached = false;
Reject(() => CompanionInstaller.Install(fakeSteam, host, plugin, Hash(host), Hash(plugin), () => false,
    index => { if (index == 4) { injectionReached = true; throw new IOException("injected write failure"); } }), "a mid-installation write failure is reported");
Check(injectionReached && !File.Exists(Path.Combine(fakeSteam, "wsock32.dll"))
    && !Directory.Exists(Path.Combine(fakeSteam, "millennium", "plugins", CompanionInstaller.PluginName)),
    "failed activation rolls back new files and the partial companion directory");
Reject(() => CompanionInstaller.ReadArchive(Archive(("../outside.dll", "bad")), ""), "archive traversal is rejected");
Reject(() => CompanionInstaller.ReadArchive(Archive(("C:/outside.dll", "bad")), ""), "archive drive paths are rejected");
Reject(() => CompanionInstaller.ReadArchive(Archive(("same.lua", "one"), ("SAME.lua", "two")), ""), "case-colliding archive files are rejected");
string configPath = Path.Combine(fakeSteam, "millennium", "config", "config.json");
Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
File.WriteAllText(configPath, "{\"otherSetting\":42,\"plugins\":{\"enabledPlugins\":[\"OtherPlugin\"]}}");
CompanionInstaller.Install(fakeSteam, host, plugin, Hash(host), Hash(plugin), () => false);
using (var config = JsonDocument.Parse(File.ReadAllText(configPath)))
    Check(config.RootElement.GetProperty("otherSetting").GetInt32() == 42
        && config.RootElement.GetProperty("plugins").GetProperty("enabledPlugins").GetArrayLength() == 2,
        "activation preserves other plugins and settings");
Check(CompanionInstaller.IsInstalled(fakeSteam), "installation records ownership and includes the plugin");
Check(CompanionInstaller.IsCurrent(fakeSteam), "installation records the current companion revision");
Check(CompanionInstaller.IsEnabled(fakeSteam), "installed presentation is enabled for the unified workflow");
string receiptPath = Path.Combine(fakeSteam, "millennium", "config", "uwphook-library-details.json");
File.WriteAllText(receiptPath, File.ReadAllText(receiptPath).Replace(CompanionInstaller.PluginVersion, "5.0.0-uwphook.1"));
Check(!CompanionInstaller.IsCurrent(fakeSteam), "previous companion revision is detected as needing an update");
File.WriteAllText(Path.Combine(fakeSteam, "millennium", "lib", "millennium.dll"), "existing-host");
CompanionInstaller.Install(fakeSteam, host, plugin, Hash(host), Hash(plugin), () => false);
Check(File.ReadAllText(Path.Combine(fakeSteam, "millennium", "lib", "millennium.dll")) == "existing-host", "reinstall reuses an existing host without downgrading it");
Check(CompanionInstaller.IsCurrent(fakeSteam), "updating an owned installation refreshes its revision");
CompanionInstaller.Disable(fakeSteam, () => false);
Check(!CompanionInstaller.IsEnabled(fakeSteam), "unified workflow detects when presentation must be re-enabled");
using (var config = JsonDocument.Parse(File.ReadAllText(configPath)))
    Check(config.RootElement.GetProperty("plugins").GetProperty("enabledPlugins").EnumerateArray().Select(item => item.GetString()).SequenceEqual(["OtherPlugin"]), "deactivation preserves other enabled plugins");
string unowned = Path.Combine(folder, "UnownedSteam");
Directory.CreateDirectory(Path.Combine(unowned, "millennium", "plugins", CompanionInstaller.PluginName));
File.WriteAllText(Path.Combine(unowned, "Steam.exe"), "test");
Reject(() => CompanionInstaller.Install(unowned, host, plugin, Hash(host), Hash(plugin), () => false), "an independently installed companion is not overwritten");
File.WriteAllText(Path.Combine(unowned, "wsock32.dll"), "another application");
Reject(() => CompanionInstaller.Install(unowned, host, plugin, Hash(host), Hash(plugin), () => false), "an unrelated proxy DLL is not overwritten");
string mappingFolder = Path.Combine(folder, "companion-state");
Directory.CreateDirectory(mappingFolder);
File.WriteAllText(Path.Combine(mappingFolder, "mappings.backup.json"), "{\"123\":\"456\"}");
DetailsStore.Link(mappingFolder, new Dictionary<uint, uint> { [2437190603] = 3595270 });
Check(DetailsStore.Read<string>(Path.Combine(mappingFolder, "mappings.json")).Count == 2, "details mappings retain the existing backup and link the correct Steam AppID");
Check(DetailsStore.Read<string>(Path.Combine(mappingFolder, "mappings.json"))["shortcut:2437190603"] == "3595270", "metadata uses the committed shortcut namespace expected by the Steam frontend");
File.WriteAllText(Path.Combine(mappingFolder, "mappings.json"), "broken");
Reject(() => DetailsStore.Link(mappingFolder, new Dictionary<uint, uint> { [2] = 3 }), "corrupt companion mappings are not overwritten");
Check(File.ReadAllText(Path.Combine(mappingFolder, "mappings.json")) == "broken", "failed mapping update preserves source bytes");
var detailStore = new DetailsStore(Path.Combine(folder, "chosen-details.json"));
detailStore.Save("xbox!cod", new(3595270, "Modern Warfare III"));
Check(detailStore.Find("xbox!cod")?.Id == 3595270, "Steam metadata selection persists independently of the launch target");

// A complete import commits the real binary shortcut and its mapping together.
string importSteam = Path.Combine(folder, "UnifiedSteam"), importState = Path.Combine(folder, "UnifiedState"), importApp = Path.Combine(folder, "UnifiedApp");
Directory.CreateDirectory(Path.Combine(importSteam, "userdata", "777", "config"));
Directory.CreateDirectory(Path.Combine(importSteam, "userdata", "888", "config"));
File.WriteAllText(Path.Combine(importSteam, "steam.exe"), "fixture");
string importVdf = Path.Combine(importSteam, "userdata", "777", "config", "shortcuts.vdf");
var importPlan = new ImportGame(new AppEntry { Aumid = "xbox!unified", Name = "Old name", Executable = "bootstrapper.exe" },
    new(3595270, "Call of Duty®: Modern Warfare® III", "Descrição", "Studio", "Publisher", "2023", "Português"));
var retained = new VDFEntry { appid = unchecked((int)2838666077u), AppName = "Previous name", Exe = "C:\\Old\\UWPHook.exe", LaunchOptions = "xbox!unified bootstrapper.exe", Tags = ["Favorites"], LastPlayTime = 72, IsHidden = 1 };
File.WriteAllBytes(importVdf, VDFParser.VDFSerializer.Serialize([retained, unrelated]));
var importResult = UnifiedImporter.Import(importSteam, "C:\\New\\UWPHook.exe", [importPlan], importState, importApp, () => false);
var imported = VDFParser.VDFParser.Parse(importVdf);
Check(imported.Length == 2 && imported[0].appid == retained.appid && imported[0].LastPlayTime == 72 && imported[0].IsHidden == 1
    && imported[0].Tags.SequenceEqual(["Favorites"]) && imported[1].appid == unrelated.appid, "unified import preserves the existing identity, history, visibility, tags and unrelated entries");
Check(imported[0].AppName == importPlan.Game.Name && imported[0].Exe == "C:\\New\\UWPHook.exe" && imported[0].LaunchOptions == "xbox!unified bootstrapper.exe", "canonical edition is assigned before Steam starts while Xbox launch arguments remain intact");
var importMappings = DetailsStore.Read<string>(Path.Combine(importState, "mappings.json"));
Check(importMappings["shortcut:2838666077"] == "3595270" && !importMappings.ContainsKey("2838666077"), "one import commits a mapping recognized without a Steam linking dialog");
var freshEntries = VDFParser.VDFParser.Parse(Path.Combine(importSteam, "userdata", "888", "config", "shortcuts.vdf"));
Check(freshEntries.Length == 1 && freshEntries[0].AppName == importPlan.Game.Name
    && importMappings[DetailsStore.MappingKey(unchecked((uint)freshEntries[0].appid))] == "3595270", "first import into a fresh Steam profile creates a pre-linked shortcut in one action");
Check(File.ReadAllBytes(Path.Combine(importState, "mappings.json")).SequenceEqual(File.ReadAllBytes(Path.Combine(importState, "mappings.backup.json")))
    && Directory.Exists(importResult.Backup), "import includes a persistent mapping backup and a restore manifest");
var priorVdf = File.ReadAllBytes(importVdf); var priorMappings = File.ReadAllBytes(Path.Combine(importState, "mappings.json"));
Reject(() => UnifiedImporter.Import(importSteam, "C:\\New\\UWPHook.exe", [importPlan], importState, importApp, () => true), "unified import refuses a running Steam client");
Reject(() => UnifiedImporter.Import(importSteam, "C:\\Different\\UWPHook.exe", [importPlan], importState, importApp, () => false,
    beforeWrite: count => { if (count == 3) throw new IOException("injected import failure"); }), "unified import rolls back a failure after writing the shortcuts and mapping");
Check(priorVdf.SequenceEqual(File.ReadAllBytes(importVdf)) && priorMappings.SequenceEqual(File.ReadAllBytes(Path.Combine(importState, "mappings.json"))), "failed unified import restores the original shortcut and mapping bytes");
UnifiedImporter.Import(importSteam, "C:\\New\\UWPHook.exe", [importPlan], importState, importApp, () => false);
Check(VDFParser.VDFParser.Parse(importVdf).Length == 2, "repeating the unified action does not duplicate the game");
Check(GamesWindow.NormalizeEdition("Call of Duty®: Modern Warfare® III") == GamesWindow.NormalizeEdition("Call of Duty Modern Warfare III")
    && GamesWindow.NormalizeEdition("Modern Warfare 3") != GamesWindow.NormalizeEdition("Modern Warfare III"), "automatic edition matching keeps the 2011 and 2023 titles distinct");
if (args.Length == 2 && args[0] == "--contract-output") File.WriteAllText(args[1], JsonSerializer.Serialize(importMappings));
Check(SteamCatalog.PlainText("Português&lt;teste&gt;<br><strong>*</strong>áudio") == "Português<teste>\n*áudio", "Steam descriptions and language labels render as plain text");
using (var catalog = new SteamCatalog(new FakeHandler(HttpStatusCode.OK, "{\"items\":[{\"id\":3595270,\"name\":\"Modern Warfare III\"}]}")))
    Check((await catalog.Search("Modern Warfare III")).Single().Id == 3595270, "Steam catalog search keeps the confirmed official ID");
Check(SteamCatalog.TryAppId("https://store.steampowered.com/app/3595270/Call_of_Duty/", out uint parsedId) && parsedId == 3595270,
    "official store URLs can resolve metadata when text search is incomplete");
Check(!SteamCatalog.TryAppId("https://example.com/app/3595270/", out _) && !SteamCatalog.TryAppId("0", out _)
    && !SteamCatalog.TryAppId("file://store.steampowered.com/app/3595270/", out _),
    "direct catalog lookup accepts only valid Steam IDs and store URLs");
var queryHandler = new FakeHandler(HttpStatusCode.OK, "{\"items\":[]}");
using (var catalog = new SteamCatalog(queryHandler))
{
    await catalog.Search("Call of Duty®: Modern Warfare™ III");
    Check(queryHandler.RequestUri!.Query.Contains("Call%20of%20Duty%20Modern%20Warfare%20III"), "Steam search removes legal marks and punctuation from the title");
}
using (var catalog = new SteamCatalog(new FakeHandler(HttpStatusCode.OK, "{\"3595270\":{\"success\":true,\"data\":{\"steam_appid\":3595270,\"name\":\"MWIII\",\"short_description\":\"Descrição\",\"supported_languages\":\"Português (Brasil)\",\"developers\":[\"Studio\"],\"publishers\":[\"Publisher\"],\"release_date\":{\"date\":\"10/nov./2023\"}}}}")))
    Check((await catalog.Details(3595270)).Languages == "Português (Brasil)", "official description and language data are loaded without an API key");
var bundledAssembly = typeof(CompanionInstaller).Assembly;
using (var stream = bundledAssembly.GetManifestResourceStream("UWPHook.Details.Plugin.zip")!)
{
    using var data = new MemoryStream(); stream.CopyTo(data);
    var files = CompanionInstaller.ReadArchive(data.ToArray(), CompanionInstaller.PluginName + "/");
    string js = Encoding.UTF8.GetString(files[".millennium/Dist/index.js"]);
    string lua = Encoding.UTF8.GetString(files["backend/main.lua"]);
    Check(!System.Text.RegularExpressions.Regex.IsMatch(js, "[\"'][a-f0-9]{28,40}[\"']"), "bundled companion contains no distribution API credentials");
    Check(lua.Contains("sync_steam_account_achievements = uwphook_policy.disabled") && !files.Keys.Any(path => path.EndsWith("steam_achievement_sync.ps1")), "bundled integration disables account-achievement writes");
}
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



