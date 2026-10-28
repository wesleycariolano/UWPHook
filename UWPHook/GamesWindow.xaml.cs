using Force.Crc32;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UWPHook.Properties;
using UWPHook.SteamGridDb;
using UWPHook.LibraryDetails;
using VDFParser;
using VDFParser.Models;

namespace UWPHook
{
    /// <summary>
    /// Interaction logic for GamesWindow.xaml
    /// </summary>
    public partial class GamesWindow : Window
    {
        AppEntryModel Apps;
        BackgroundWorker bwrLoad;
        static LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch();
        private bool maintenanceMode;

        private readonly bool previewMode;
        public GamesWindow() : this(false) { }

        internal GamesWindow(bool previewMode)
        {
            this.previewMode = previewMode;
            InitializeComponent();
            Log.Debug("Init GamesWindow");
            Apps = new AppEntryModel();
            InitializeBrand();
            if (previewMode) return;
            Closing += (_, e) => { if (importBusy) e.Cancel = true; };
            var args = Environment.GetCommandLineArgs();

            // Init log file to AppData\Roaming\Briano\UWPHook directory with size rotation on 10Mb with max 5 files
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            string loggerFilePath = String.Join("\\", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), fvi.CompanyName, fvi.ProductName, "application.log");

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.File(path: loggerFilePath, rollOnFileSizeLimit: true, fileSizeLimitBytes: 10485760, retainedFileCountLimit: 5)
            .WriteTo.Console()
            .CreateLogger();

            // Switch to Info by default to inform logger level in log file and switch to the correct log level
            levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
            SetLogLevel();

            if (args?.ElementAtOrDefault(1) == "--install-library-details")
            {
                maintenanceMode = true;
                _ = InstallDetailsAndClose();
                return;
            }

            // If null or 1, the app was launched normally
            if (args?.Length > 1)
            {
                // When length is 1, the only argument is the path where the app is installed
                _ = LauncherAsync(args); // Launches the requested game
            }
            else
            {
                TryMigrateArtworkKey();
                //auto refresh on load
                LoadButton_Click(null, null);
            }
        }

        private async Task InstallDetailsAndClose()
        {
            try
            {
                grid.IsEnabled = false;
                await Task.Run(() => CompanionInstaller.InstallBundled(SteamManager.GetSteamFolder()));
                Environment.ExitCode = 0;
            }
            catch (Exception exception)
            {
                Environment.ExitCode = 1;
                MessageBox.Show(exception.Message, "UWPHook — ativação da ficha");
            }
            finally { Close(); }
        }

        private static void TryMigrateArtworkKey()
        {
            if (!string.IsNullOrWhiteSpace(Settings.Default.SteamGridDbApiKey)) return;
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Briano");
                string? key = ArtworkKeyMigration.ResolveKey(Settings.Default.SteamGridDbApiKey, folder);
                if (string.IsNullOrWhiteSpace(key)) return;
                Settings.Default.SteamGridDbApiKey = key;
                Settings.Default.OfferedSteamGridDB = true;
                Settings.Default.Save();
                Log.Information("Imported existing SteamGridDB configuration from a previous UWPHook installation.");
            }
            catch (Exception)
            {
                Log.Warning("Could not import previous artwork settings. Configure SteamGridDB in Settings.");
            }
        }

        /// <summary>
        /// We have to wait a little untill Steam catches up, otherwise it will stream a black screen
        /// </summary>
        /// <returns></returns>
        async Task LaunchDelay()
        {
            await Task.Delay(10000);
        }

        /// <summary>
        /// Main task that launches a game
        /// Usually invoked by steam
        /// </summary>
        /// <param name="args">launch args received from the program execution</param>
        /// <returns></returns>
        private async Task LauncherAsync(string[] args)
        {
            if (args[1].StartsWith("folder:", StringComparison.Ordinal)) { await LaunchFolderGame(args[1]); return; }
            FullScreenLauncher launcher = null;
            //So, for some reason, Steam is now stopping in-home streaming if the launched app is minimized, so not hiding UWPHook's window is doing the trick for now
            if (Settings.Default.StreamMode)
            {
                this.Hide();
                launcher = new FullScreenLauncher();
                launcher.Show();

                await LaunchDelay();

                launcher.Close();
            }
            else
            {
                this.Title = AppIdentity.Name + ": jogando";
                this.Hide();
            }

            //Hide the window so the app is launched seamless making UWPHook run in the background without bothering the user
            string currentLanguage = CultureInfo.CurrentCulture.ToString();

            try
            {
                //Some apps have their language locked to the UI language of the system, so overriding it might change the language of the game
                //I my self couldn't get this to work on neither Forza Horizon 3 or Halo 5 Forge, @AbGedreht reported it works tho
                if (Settings.Default.ChangeLanguage && !String.IsNullOrEmpty(Settings.Default.TargetLanguage))
                {
                    ScriptManager.RunScript("Set-WinUILanguageOverride " + Properties.Settings.Default.TargetLanguage);
                }

                if (Settings.Default.ChangeResolution && !String.IsNullOrEmpty(Settings.Default.TargetResolution))
                {
                    var targetResolution = ExtractDimensions(Settings.Default.TargetResolution);
                    ScriptManager.RunScript("Set-DisplayResolution -Width " + targetResolution.Width + " - Height " + targetResolution.Height + " -Force");
                }

                //The only other parameter Steam will send is the app AUMID
                AppManager.LaunchUWPApp(args);

                //While the current launched app is running, sleep for n seconds and then check again
                while (AppManager.IsRunning())
                {
                    Thread.Sleep(Settings.Default.Seconds * 1000);
                }
            }
            catch (Exception e)
            {
                this.Show();
                MessageBox.Show(e.Message, AppIdentity.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (Settings.Default.ChangeLanguage && !String.IsNullOrEmpty(Settings.Default.TargetLanguage))
                {
                    ScriptManager.RunScript("Set-WinUILanguageOverride " + currentLanguage);
                }

                //The user has probably finished using the app, so let's close UWPHook to keep the experience clean
                this.Close();
            }
        }

        static (int Width, int Height) ExtractDimensions(string resolution)
        {
            var parts = resolution.Split('x');
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0].Trim(), out int width) && int.TryParse(parts[1].Trim(), out int height))
                {
                    return (width, height);
                }
            }
            throw new FormatException("Invalid resolution format.");
        }

        /// <summary>
        /// Generates a CRC32 hash expected by Steam to link an image with a game in the library
        /// See https://blog.yo1.dog/calculate-id-for-non-steam-games-js/ for an example
        /// </summary>
        /// <param name="appName">The name of the executable to be displayed</param>
        /// <param name="appTarget">The executable target path</param>
        /// <returns></returns>
        private UInt64 GenerateSteamGridAppId(string appName, string appTarget)
        {
            byte[] nameTargetBytes = Encoding.UTF8.GetBytes(appTarget + appName + "");
            UInt64 crc = Crc32Algorithm.Compute(nameTargetBytes);
            UInt64 gameId = crc | 0x80000000;

            return gameId;
        }

        /// <summary>
        /// Task responsible for triggering the export, blocks the UI, and shows a message
        /// once the task is finished, unlocking the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Apps.Entries.Any(app => app.Selected))
            {
                MessageBox.Show("Select at least one app to export.", "UWPHook");
                return;
            }
            if (Process.GetProcessesByName("steam").Length > 0)
            {
                MessageBox.Show("Exit Steam from its system tray menu before exporting, then try again. This prevents Steam from overwriting your shortcuts.", "UWPHook");
                return;
            }
            grid.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;
            artworkFailures.Clear();
            string msg;
            try
            {
                RemoveTempGridImages();
                await ExportGames();
                msg = "Your apps were exported. Open Steam to see the updated library.";
            }
            catch (Exception)
            {
                msg = "Export could not finish. Check your Steam folder and the saved library-metadata.json file.";
            }
            finally
            {
                grid.IsEnabled = true;
                progressBar.Visibility = Visibility.Collapsed;
            }
            if (artworkFailures.Count > 0)
                msg += "\nSome artwork could not be downloaded. Existing artwork was kept where possible. Retry exporting to download it.";
            if (string.IsNullOrWhiteSpace(Settings.Default.SteamGridDbApiKey))
                msg += "\nNo library artwork was downloaded: add a SteamGridDB API key in Settings, then export again.";
            MessageBox.Show(msg, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Downloads the given image in the url to a given path in a given format
        /// </summary>
        /// <param name="imageUrl">The url for the image</param>
        /// <param name="destinationFilename">Path to store the image</param>
        /// <param name="format"></param>
        /// <returns></returns>
        private static readonly System.Net.Http.HttpClient imageClient = new();

        private async Task SaveImage(string imageUrl, string destinationFilename, ImageFormat format)
        {
            using var stream = await imageClient.GetStreamAsync(imageUrl);
            using var bitmap = new Bitmap(stream);
            bitmap.Save(destinationFilename, format);
        }

        /// <summary>
        /// Copies all temporary images to the given user
        /// </summary>
        /// <param name="user">The user path to copy images to</param>
        private void CopyTempGridImagesToSteamUser(string user, IReadOnlyDictionary<string, string> artworkIds)
        {
            string tmpGridDirectory = exportGridDirectory;
            string userGridDirectory = user + "\\config\\grid\\";

            // No images were downloaded, maybe the key is invalid or no app had an image
            if (!Directory.Exists(tmpGridDirectory))
            {
                return;
            }

            string[] images = Directory.GetFiles(tmpGridDirectory);

            if (!Directory.Exists(userGridDirectory))
            {
                Directory.CreateDirectory(userGridDirectory);
            }

            foreach (string image in images)
            {
                string destFile = userGridDirectory + ShortcutIdentity.ArtworkFileName(Path.GetFileName(image), artworkIds);
                File.Copy(image, destFile, true);
            }
        }

        private void RemoveTempGridImages()
        {
            string tmpGridDirectory = exportGridDirectory;
            if (Directory.Exists(tmpGridDirectory))
            {
                Directory.Delete(tmpGridDirectory, true);
            }
        }

        /// <summary>
        /// Task responsible for downloading grid images to a temporary location,
        /// generates the steam ID for the game based in the receiving parameters,
        /// Throws TaskCanceledException if cannot communicate with SteamGridDB properly
        /// </summary>
        /// <param name="appName">The name of the app</param>
        /// <param name="appTarget">The target path of the executable</param>
        /// <returns></returns>
        private async Task DownloadTempGridImages(string appName, string appTarget, ArtworkSelection game)
        {
            using var api = new SteamGridDbApi(Properties.Settings.Default.SteamGridDbApiKey);
            string tmpGridDirectory = exportGridDirectory;
            Log.Verbose("Detected Game: " + game.ToString());
            UInt64 gameId = GenerateSteamGridAppId(appName, appTarget);

            SafellyCreateTmpGridDirectory(tmpGridDirectory);

            var gameGridsVertical = api.GetGameGrids(game.GameId, "600x900,342x482,660x930");
            var gameGridsHorizontal = api.GetGameGrids(game.GameId, "460x215,920x430");
            var gameHeroes = api.GetGameHeroes(game.GameId);
            var gameLogos = api.GetGameLogos(game.GameId);

            Log.Verbose("Game ID: " + game.GameId);

            await Task.WhenAll(
                gameGridsVertical,
                gameGridsHorizontal,
                gameHeroes,
                gameLogos
            );

            var gridsVertical = await gameGridsVertical;
            var gridsHorizontal = await gameGridsHorizontal;
            var heroes = await gameHeroes;
            var logos = await gameLogos;

            List<Task> saveImagesTasks = new List<Task>();

            if (gridsHorizontal != null && gridsHorizontal.Length > 0)
            {
                var grid = gridsHorizontal[0];
                saveImagesTasks.Add(SaveImage(grid.Url, $"{tmpGridDirectory}\\{gameId}.png", ImageFormat.Png));
            }

            if (gridsVertical != null && gridsVertical.Length > 0)
            {
                var grid = gridsVertical[0];
                saveImagesTasks.Add(SaveImage(grid.Url, $"{tmpGridDirectory}\\{gameId}p.png", ImageFormat.Png));
            }

            if (heroes != null && heroes.Length > 0)
            {
                var hero = heroes[0];
                saveImagesTasks.Add(SaveImage(hero.Url, $"{tmpGridDirectory}\\{gameId}_hero.png", ImageFormat.Png));
            }

            if (logos != null && logos.Length > 0)
            {
                var logo = logos[0];
                saveImagesTasks.Add(SaveImage(logo.Url, $"{tmpGridDirectory}\\{gameId}_logo.png", ImageFormat.Png));
            }

            if (saveImagesTasks.Count == 0)
                throw new InvalidOperationException("No artwork matched the selected game and filters.");

            await Task.WhenAll(saveImagesTasks);
        }

        private void SafellyCreateTmpGridDirectory(string tmpGridDirectory)
        {
            if (!Directory.Exists(tmpGridDirectory))
            {
                try
                {
                    Directory.CreateDirectory(tmpGridDirectory);
                    Log.Information("Created directory: " + tmpGridDirectory);
                }
                catch (Exception exception)
                {
                    Log.Error(exception.Message);
                    Log.Error(exception.StackTrace);
                    throw new Exception("UWPHook was not able to create the tmp directory for some reason." + Environment.NewLine +
                        "You may solve this by manually creating the following folder:" + Environment.NewLine +
                        tmpGridDirectory);
                }
            }
        }

        /// <summary>
        /// Main Task to export the selected games to steam
        /// </summary>
        /// <param name="restartSteam"></param>
        /// <returns></returns>
        private async Task<bool> ExportGames()
        {
            string[] tags = Settings.Default.Tags.Split(',');
            string steam_folder = SteamManager.GetSteamFolder();

            if (!Directory.Exists(steam_folder)) throw new DirectoryNotFoundException("Steam installation was not found.");
            if (Directory.Exists(steam_folder))
            {
                var users = SteamManager.GetUsers(steam_folder);
                var selected_apps = Apps.Entries.Where(app => app.Selected).ToList();
                var processModule = Process.GetCurrentProcess().MainModule;
                var exePath = processModule?.FileName;
                var exeDir = Path.GetDirectoryName(exePath);

                List<Task> gridImagesDownloadTasks = new List<Task>();
                bool downloadGridImages = !String.IsNullOrEmpty(Properties.Settings.Default.SteamGridDbApiKey);
                //To make things faster, decide icons and download grid images before looping users
                Log.Verbose("downloadGridImages: " + (downloadGridImages));

                foreach (var app in selected_apps)
                {
                    app.Icon = app.widestSquareIcon();

                    if (downloadGridImages)
                    {
                        Log.Verbose("Downloading grid images for app " + app.Name);

                        var selection = ArtworkSelectionStore.Default.Find(app.Aumid);
                        if (selection == null)
                            selection = ChooseArtwork(app);
                        if (selection != null)
                            gridImagesDownloadTasks.Add(DownloadArtworkSafely(app.Name, exePath, selection));
                    }
                }

                await Task.WhenAll(gridImagesDownloadTasks);

                // Export the selected apps and the downloaded images to each user
                // in the steam folder by modifying it's VDF file
                foreach (var user in users)
                {
                    var artworkIds = new Dictionary<string, string>();
                    try
                    {
                        VDFEntry[] shortcuts = new VDFEntry[0];
                        try
                        {
                            shortcuts = SteamManager.ReadShortcuts(user);
                        }
                        catch (Exception ex)
                        {
                            //If it's a short VDF, let's just overwrite it
                            if (ex.GetType() != typeof(VDFTooShortException))
                            {
                                Log.Error("Error: Program failed to load existing Steam shortcuts." + Environment.NewLine + ex.Message);
                                throw new Exception("Error: Program failed to load existing Steam shortcuts." + Environment.NewLine + ex.Message);
                            }
                        }

                        if (shortcuts != null)
                        {
                            foreach (var app in selected_apps)
                            {
                                try
                                {
                                    app.Icon = PersistAppIcon(app);
                                    Log.Verbose("Defaulting to app.Icon for app " + app.Name);
                                }
                                catch (System.IO.IOException)
                                {
                                    Log.Verbose("Using backup icon for app " + app.Name);

                                    await Task.Run(() =>
                                    {
                                        string tmpGridDirectory = exportGridDirectory;

                                        SafellyCreateTmpGridDirectory(tmpGridDirectory);

                                        string[] images = Directory.GetFiles(tmpGridDirectory);

                                        UInt64 gameId = GenerateSteamGridAppId(app.Name, exePath);
                                        app.Icon = PersistAppIcon(app, Path.Combine(tmpGridDirectory, gameId + "_logo.png"));
                                    });
                                }

                                VDFEntry newApp = new VDFEntry()
                                {
                                    appid = ((int)GenerateSteamGridAppId(app.Name, exePath)),
                                    AppName = app.Name,
                                    Exe = exePath,
                                    StartDir = exeDir,
                                    LaunchOptions = app.Aumid + " " + app.Executable,
                                    AllowDesktopConfig = 1,
                                    AllowOverlay = 1,
                                    Icon = app.Icon,
                                    Index = shortcuts.Length,
                                    IsHidden = 0,
                                    OpenVR = 0,
                                    ShortcutPath = "",
                                    Tags = tags,
                                    Devkit = 0,
                                    DevkitGameID = "",
                                    LastPlayTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                };
                                Boolean shortcutAlreadyExists = false;
                                for (int i = 0; i < shortcuts.Length; i++)
                                {
                                    Log.Verbose(shortcuts[i].ToString());

                                    if (ShortcutIdentity.Matches(shortcuts[i], app.Aumid, exePath))
                                    {
                                        shortcutAlreadyExists = true;
                                        Log.Verbose(app.Name + " already added to Steam. Updating existing shortcut.");
                                        ShortcutIdentity.PreserveLibraryIdentity(shortcuts[i], newApp);
                                        artworkIds[GenerateSteamGridAppId(app.Name, exePath).ToString()] = unchecked((uint)newApp.appid).ToString();
                                        shortcuts[i] = newApp;
                                        break;
                                    }
                                }

                                if (!shortcutAlreadyExists)
                                {
                                    //Resize this array so it fits the new entries
                                    Array.Resize(ref shortcuts, shortcuts.Length + 1);
                                    shortcuts[shortcuts.Length - 1] = newApp;
                                }
                            }

                            try
                            {
                                if (!Directory.Exists(user + @"\\config\\"))
                                {
                                    Directory.CreateDirectory(user + @"\\config\\");
                                }

                                BackupShortcutsVDF(user, @"\\config\\shortcuts.vdf");

                                //Write the file with all the shortcuts
                                File.WriteAllBytes(user + @"\\config\\shortcuts.vdf", VDFSerializer.Serialize(shortcuts));
                                Log.Debug("Shortcuts written to " + user + @"\\config\\shortcuts.vdf");
                                DetailsLinker.Apply(shortcuts, selected_apps);
                                CopyTempGridImagesToSteamUser(user, artworkIds);
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Error: Program failed while trying to write your Steam shortcuts" + Environment.NewLine + ex.InnerException + ex.StackTrace);
                                throw new Exception("Error: Program failed while trying to write your Steam shortcuts" + Environment.NewLine + ex.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error: Program failed exporting your games:" + Environment.NewLine + ex.Message + ex.StackTrace);
                        throw new InvalidOperationException("Could not export shortcuts for a Steam user.", ex);
                    }
                }

                if (gridImagesDownloadTasks.Count > 0)
                {
                    await Task.WhenAll(gridImagesDownloadTasks);

                    await Task.Run(() =>
                    {
                        RemoveTempGridImages();
                    });
                }
            }

            return true;
        }

        private void BackupShortcutsVDF(string userPath, string vdfSubPath)
        {
            string backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook", "backups");
            if (!Directory.Exists(backupFolder))
            {
                Directory.CreateDirectory(backupFolder);
                Log.Debug("Created backup folder: " + backupFolder);
            }

            string user_id = userPath.Split('\\').Last();

            string sourceFilePath = Path.Combine(userPath, "config", "shortcuts.vdf");
            string destinationFileName = Path.Combine(backupFolder, $"{user_id}_{DateTime.Now.ToString("yyyyMMddHHmmss")}_shortcuts.vdf");

            if (!File.Exists(sourceFilePath))
            {
               Log.Information("No VDF file found for user: " + sourceFilePath);
               return;
            }

            File.Copy(sourceFilePath, destinationFileName);
            Log.Debug("Backup created: " + destinationFileName);
        }

        /// <summary>
        /// Copies an apps icon to a intermediate location
        /// Due to some apps changing the icon location when they update, which causes icons to be "lost"
        /// </summary>
        /// <param name="app">App to copy the icon to</param>
        /// <param name="forcedIcon">Overwrites the app.icon to be copied</param>
        /// <returns>string, the path to the usable and persisted icon</returns>
        private string PersistAppIcon(AppEntry app, string forcedIcon = "")
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string icons_path = path + @"\Briano\UWPHook\icons\";

            // If we do not have an specific icon to copy, copy app.icon, if we do, copy the specified icon
            string icon_to_copy = String.IsNullOrEmpty(forcedIcon) ? app.Icon : forcedIcon;

            if (!Directory.Exists(icons_path))
            {
                Directory.CreateDirectory(icons_path);
            }

            string dest_file = String.Join(String.Empty, icons_path, app.Aumid + Path.GetFileName(icon_to_copy));
            try
            {
                if (File.Exists(icon_to_copy))
                {
                    File.Copy(icon_to_copy, dest_file, true);
                }
                else
                {
                    dest_file = app.Icon;
                }
            }
            catch (System.IO.IOException e)
            {
                Log.Warning(e, "Could not copy icon " + app.Icon);
                throw e;
            }

            return dest_file;
        }

        public static void ClearAllShortcuts()
        {
            Log.Debug("Clearing all elements in shortcuts.vdf");
            string[] tags = Settings.Default.Tags.Split(',');
            string steam_folder = SteamManager.GetSteamFolder();

            if (Directory.Exists(steam_folder))
            {
                var users = SteamManager.GetUsers(steam_folder);
                var exePath = @"""" + System.Reflection.Assembly.GetExecutingAssembly().Location + @"""";
                var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                foreach (var user in users)
                {
                    try
                    {
                        VDFEntry[] shortcuts = new VDFEntry[0];

                        try
                        {
                            if (!Directory.Exists(user + @"\\config\\"))
                            {
                                Directory.CreateDirectory(user + @"\\config\\");
                            }
                            //Write the file with all the shortcuts
                            File.WriteAllBytes(user + @"\\config\\shortcuts.vdf", VDFSerializer.Serialize(shortcuts));
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Error: Program failed while trying to write your Steam shortcuts" + Environment.NewLine + ex.Message);
                            throw new Exception("Error: Program failed while trying to write your Steam shortcuts" + Environment.NewLine + ex.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error: Program failed while trying to clear your Steam shortcuts:" + Environment.NewLine + ex.Message + ex.StackTrace);
                        MessageBox.Show("Error: Program failed while trying to clear your Steam shortcuts:" + Environment.NewLine + ex.Message + ex.StackTrace);
                    }
                }
                MessageBox.Show("All non-Steam shortcuts has been cleared.");
            }
        }

        /// <summary>
        /// Fires the Bwr_DoWork, to load the apps installed at the machine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (importBusy || bwrLoad?.IsBusy == true) return;
            bwrLoad = new BackgroundWorker();
            bwrLoad.DoWork += Bwr_DoWork;
            bwrLoad.RunWorkerCompleted += Bwr_RunWorkerCompleted;

            grid.IsEnabled = false;
            label.Content = "Encontrando os seus jogos e aplicativos…";

            progressBar.Visibility = Visibility.Visible;
            Apps.Entries = new System.Collections.ObjectModel.ObservableCollection<AppEntry>();

            bwrLoad.RunWorkerAsync();
        }

        /// <summary>
        /// Callback for restoring the grid list interactivity
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bwr_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            listGames.ItemsSource = Apps.Entries;

            grid.IsEnabled = true;
            progressBar.Visibility = Visibility.Collapsed;
            RefreshLibraryState();
            if (e.Result is string error) label.Content = "Não foi possível concluir a detecção: " + error;
        }

        /// <summary>
        /// Worker responsible for loading the apps installed in the machine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bwr_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var registered = GameDiscovery.Discover();
                var games = registered.Where(app => app.IsGame).Concat(ManualGameStore.Default.Load())
                    .DistinctBy(app => app.Aumid).OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    registeredGames = registered;
                    foreach (var app in games) Apps.Entries.Add(app);
                });
            }
            catch (Exception error)
            {
                Log.Error(error, "Game discovery failed");
                e.Result = error.Message;
            }
        }
        private void textBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (Apps?.Entries != null)
            {
                if (SearchHint != null) SearchHint.Visibility = string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                if (!String.IsNullOrEmpty(textBox.Text) && Apps.Entries.Count > 0)
                {
                    listGames.Items.Filter = new Predicate<object>(Contains);
                }
                else
                {
                    listGames.Items.Filter = null;
                }
            }
        }

        public bool Contains(object o)
        {
            AppEntry appEntry = o as AppEntry;
            return (appEntry.Aumid.ToLower().Contains(textBox.Text.ToLower()) || appEntry.Name.ToLower().Contains(textBox.Text.ToLower()));
        }

        private ArtworkSelection? ChooseArtwork(AppEntry app)
        {
            var picker = new ArtworkPickerWindow(app.Name, Properties.Settings.Default.SteamGridDbApiKey) { Owner = this };
            if (picker.ShowDialog() != true || picker.SelectedGame is not { } game) return null;
            var selection = new ArtworkSelection(game.Id, game.Name, game.ReleaseYear, picker.UseSelectedTitle);
            ArtworkSelectionStore.Default.Save(app.Aumid, selection);
            if (selection.UseTitle) app.Name = selection.Name;
            return selection;
        }

        private void ArtworkButton_Click(object sender, RoutedEventArgs e)
        {
            if (listGames.SelectedItem is not AppEntry app)
            {
                MessageBox.Show("Select a game row first.", "UWPHook");
                return;
            }
            if (string.IsNullOrWhiteSpace(Properties.Settings.Default.SteamGridDbApiKey))
            {
                MessageBox.Show("Add your SteamGridDB API key in Settings first.", "UWPHook");
                return;
            }
            try
            {
                var selection = ChooseArtwork(app);
                if (selection != null)
                    label.Content = $"Artwork: {selection.Name} ({selection.ReleaseYear}). Export to apply.";
            }
            catch (Exception)
            {
                MessageBox.Show("Could not save the artwork choice. Check access to library-metadata.json in the UWPHook AppData folder.", "UWPHook");
            }
        }

        private async Task DownloadArtworkSafely(string appName, string appTarget, ArtworkSelection selection)
        {
            try { await DownloadTempGridImages(appName, appTarget, selection); }
            catch (Exception)
            {
                Log.Warning("Artwork download failed for {Game}; exporting the shortcut without new artwork.", appName);
                artworkFailures.Add(appName);
            }
        }

        private readonly List<string> artworkFailures = new();
        private readonly string exportGridDirectory = Path.Combine(Path.GetTempPath(), "UWPHook", "grid-" + Guid.NewGuid().ToString("N"));
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SteamPathText.Text = SteamManager.GetSteamFolder() ?? "Steam não encontrada neste computador.";
            PreferencesPanel.Visibility = Visibility.Visible;
        }

        private void LibraryDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (listGames.SelectedItem is not AppEntry app)
            {
                MessageBox.Show("Selecione a linha do jogo para configurar a ficha na Steam.", "UWPHook");
                return;
            }
            new LibraryDetailsWindow(app) { Owner = this }.ShowDialog();
        }

        /// <summary>
        /// Function that executes when the Games Window is loaded
        /// Will inform the user of the possibility of using the SteamGridDB API
        /// redirecting him to the settings page if he wishes to use the functionality
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (maintenanceMode || previewMode) return;
            ConnectionStatus.Text = Directory.Exists(SteamManager.GetSteamFolder()) ? "●  Steam encontrada" : "○  Steam não encontrada";
        }

        public static void SetLogLevel()
        {
            int logLevel_index;
            int.TryParse(Settings.Default.SelectedLogLevel, out logLevel_index);

            switch (logLevel_index)
            {
                case 1:
                    Log.Information("Init log with DEBUG level.");
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
                    break;
                case 2:
                    Log.Information("Init log with TRACE level.");
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;
                    break;
                default:
                    Log.Information("Init log with ERROR level.");
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Error;
                    break;
            }
        }
    }
}
