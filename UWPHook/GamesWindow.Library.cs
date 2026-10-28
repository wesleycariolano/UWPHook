using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using UWPHook.LibraryDetails;

namespace UWPHook;

public partial class GamesWindow
{
    private readonly SteamCatalog unifiedCatalog = new();
    private SteamGameDetails? currentGame;
    private int editionRequest;
    private bool updatingEdition, importBusy;
    private readonly Dictionary<string, uint> linkedSteamIds = new();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) { if (!importBusy) Close(); }
    private void LibraryNav_Click(object sender, RoutedEventArgs e) => PreferencesPanel.Visibility = Visibility.Collapsed;

    private void RefreshLibraryState()
    {
        try
        {
            var steam = SteamManager.GetSteamFolder();
            var shortcuts = Directory.Exists(steam) ? SteamManager.GetUsers(steam)
                .SelectMany(user => File.Exists(Path.Combine(user, "config", "shortcuts.vdf")) ? SteamManager.ReadShortcuts(user) : []).ToArray() : [];
            var mappings = DetailsStore.Read<string>(Path.Combine(DetailsStore.CompanionState, "mappings.json"));
            linkedSteamIds.Clear();
            foreach (var app in Apps.Entries)
            {
                var shortcut = shortcuts.FirstOrDefault(entry => ShortcutIdentity.Matches(entry, app.Aumid, Environment.ProcessPath!));
                app.InSteam = shortcut != null && uint.TryParse(mappings.GetValueOrDefault(DetailsStore.MappingKey(unchecked((uint)shortcut.appid))), out uint mapped) && mapped > 0;
                if (app.InSteam) linkedSteamIds[app.Aumid] = uint.Parse(mappings[DetailsStore.MappingKey(unchecked((uint)shortcut!.appid))]);
                if (shortcut != null && File.Exists(shortcut.Icon)) app.Icon = shortcut.Icon;
            }
            listGames.Items.Refresh();
            GameCount.Text = Apps.Entries.Count + " jogos";
            label.Content = "Escolha um jogo. O resto fica por nossa conta.";
            if (listGames.SelectedItem == null)
                listGames.SelectedItem = Apps.Entries.FirstOrDefault(app => app.InSteam) ?? Apps.Entries.FirstOrDefault();
        }
        catch (Exception) { label.Content = "Biblioteca carregada. Não foi possível conferir os atalhos existentes."; }
    }

    private async void Game_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (previewMode || listGames.SelectedItem is not AppEntry app) return;
        currentGame = null; ImportButton.IsEnabled = false;
        GameTitle.Text = app.Name; HeroImage.Source = null;
        GameDescription.Text = "Buscando a edição, a descrição e os idiomas na Steam…";
        DeveloperText.Text = ReleaseText.Text = "—"; LanguagesText.Text = "Carregando a ficha…";
        EditionQuery.Text = app.Name; GameSubtitle.Text = app.LaunchKind == "win32" ? "Jogo de PC · Adicionado por pasta" : "Instalado no Windows · Inicialização pelo Xbox";
        GameSourceTag.Text = app.LaunchKind == "win32" ? "PC / PASTA" : "XBOX / WINDOWS";
        int version = ++editionRequest;
        try
        {
            var saved = DetailsStore.Default.Find(app.Aumid);
            if (saved != null)
            {
                SetEditions([saved], saved);
                await LoadEdition(saved, app, version);
            }
            else await SearchEditions(app, version);
        }
        catch (Exception) { if (version == editionRequest) ShowCatalogError(); }
    }

    private void SetEditions(SteamGame[] games, SteamGame? selection)
    {
        updatingEdition = true;
        EditionResults.ItemsSource = games; EditionResults.SelectedItem = selection;
        updatingEdition = false;
    }

    private async Task SearchEditions(AppEntry app, int version)
    {
        var games = await unifiedCatalog.Search(EditionQuery.Text);
        if (version != editionRequest || listGames.SelectedItem != app) return;
        var exact = games.Where(game => NormalizeEdition(game.Name) == NormalizeEdition(app.Name)).ToArray();
        var chosen = exact.Length == 1 ? exact[0] : games.Length == 1 ? games[0] : null;
        SetEditions(games, chosen);
        if (chosen != null) await LoadEdition(chosen, app, version);
        else
        {
            GameDescription.Text = games.Length == 0 ? "Nenhuma edição foi encontrada. Em ‘Conferir ou trocar a edição’, busque pelo nome completo ou informe o AppID da Steam."
                : "Este título tem várias edições. Em ‘Conferir ou trocar a edição’, escolha a versão que você joga. Essa escolha fica salva para a próxima vez.";
            ImportHint.Text = "Escolha a edição para preparar a ficha e as artes corretas.";
        }
    }

    internal static string NormalizeEdition(string value) => Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{N}]", "");

    private async Task LoadEdition(SteamGame game, AppEntry app, int version)
    {
        var details = await unifiedCatalog.Details(game.Id);
        if (version != editionRequest || listGames.SelectedItem != app) return;
        ShowEdition(details);
        label.Content = app.InSteam && linkedSteamIds.GetValueOrDefault(app.Aumid) == game.Id
            ? "Jogo vinculado à Steam. Use Atualizar vínculo para sincronizar novamente."
            : "Edição identificada. Vincule à Steam para concluir tudo em uma etapa.";
    }

    private void ShowEdition(SteamGameDetails game)
    {
        currentGame = game;
        GameTitle.Text = game.Name.Replace("®", "").Replace("™", "");
        GameSubtitle.Text = game.Release + "  ·  Edição identificada na Steam";
        GameDescription.Text = game.Description;
        DeveloperText.Text = game.Developers; ReleaseText.Text = game.Release;
        LanguagesText.Text = game.Languages;
        ImportHint.Text = "A Steam será fechada e reaberta. Atalho, artes e ficha serão preparados juntos.";
        UpdateLinkAction();
        string url = string.IsNullOrEmpty(game.Background) ? game.Header : game.Background;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https") HeroImage.Source = new BitmapImage(uri);
    }

    private void ShowCatalogError()
    {
        currentGame = null; ImportButton.IsEnabled = false;
        GameDescription.Text = "Não foi possível carregar a ficha. Confira a conexão e tente buscar a edição novamente.";
        label.Content = "A biblioteca foi preservada. Tente consultar a Steam novamente.";
    }

    private async void EditionSearch_Click(object sender, RoutedEventArgs e)
    {
        if (listGames.SelectedItem is not AppEntry app || importBusy) return;
        currentGame = null; ImportButton.IsEnabled = false;
        int version = ++editionRequest;
        try { await SearchEditions(app, version); } catch (Exception) { if (version == editionRequest) ShowCatalogError(); }
    }
    private void EditionQuery_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; EditionSearch_Click(sender, e); } }
    private async void Edition_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingEdition || previewMode || EditionResults.SelectedItem is not SteamGame game || listGames.SelectedItem is not AppEntry app) return;
        currentGame = null; ImportButton.IsEnabled = false;
        int version = ++editionRequest;
        try { await LoadEdition(game, app, version); } catch (Exception) { if (version == editionRequest) ShowCatalogError(); }
    }

    private static async Task StopSteam(string steam)
    {
        if (Process.GetProcessesByName("steam").Length == 0) return;
        Process.Start(new ProcessStartInfo(Path.Combine(steam, "steam.exe"), "-shutdown") { UseShellExecute = false, CreateNoWindow = true });
        for (int attempt = 0; attempt < 60; attempt++)
        {
            if (Process.GetProcessesByName("steam").Length == 0) return;
            await Task.Delay(500);
        }
        throw new InvalidOperationException("A Steam ainda está aberta. Termine as sessões em andamento, feche-a pelo ícone perto do relógio e tente novamente.");
    }

    private static async Task EnsurePresentation(string steam)
    {
        if (CompanionInstaller.IsCurrent(steam) && CompanionInstaller.IsEnabled(steam)) return;
        try { await Task.Run(() => CompanionInstaller.InstallBundled(steam)); }
        catch (UnauthorizedAccessException)
        {
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--install-library-details") { UseShellExecute = true, Verb = "runas" });
            if (process == null) throw new IOException("Não foi possível preparar a apresentação da biblioteca.");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 || !CompanionInstaller.IsCurrent(steam) || !CompanionInstaller.IsEnabled(steam))
                throw new IOException("A preparação não foi concluída. Confirme a solicitação do Windows ao tentar novamente.");
        }
    }

    private async void UnifiedImport_Click(object sender, RoutedEventArgs e)
    {
        if (importBusy || currentGame == null || listGames.SelectedItem is not AppEntry app) return;
        var game = currentGame;
        ImportHint.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(145, 165, 182));
        ImportHint.Text = "Preparando o jogo. Aguarde a conclusão da importação…";
        importBusy = true; grid.IsEnabled = false; progressBar.Visibility = Visibility.Visible;
        try
        {
            string steam = SteamManager.GetSteamFolder();
            if (!File.Exists(Path.Combine(steam ?? "", "steam.exe"))) throw new DirectoryNotFoundException("Não foi possível encontrar a Steam. Instale e abra a Steam antes de adicionar jogos.");
            label.Content = "Preparando a apresentação e as artes do seu jogo…";
            var plan = new ImportGame(app, game);
            var artwork = await OfficialArtwork.Download(plan);
            label.Content = "Encerrando a Steam para atualizar a biblioteca…";
            await StopSteam(steam!);
            label.Content = "Preparando a integração da biblioteca…";
            await EnsurePresentation(steam!);
            label.Content = "Salvando o atalho e a ficha do jogo…";
            var result = await Task.Run(() => UnifiedImporter.Import(steam!, Environment.ProcessPath!, [plan], DetailsStore.CompanionState,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook"),
                () => Process.GetProcessesByName("steam").Length > 0, artwork));
            app.Name = game.Name; app.InSteam = true; linkedSteamIds[app.Aumid] = game.Id; listGames.Items.Refresh();
            label.Content = "Pronto! Jogo, artes e ficha adicionados. Abrindo a sua biblioteca…";
            UpdateLinkAction();
            ImportHint.Text = "Tudo pronto. A ficha já foi vinculada; basta jogar pela Steam.";
            Process.Start(new ProcessStartInfo(Path.Combine(steam!, "steam.exe")) { UseShellExecute = true });
            if (artwork.Count < 4) label.Content = "Jogo e ficha prontos na Steam. Algumas artes não estavam disponíveis; as existentes foram mantidas.";
        }
        catch (Exception exception)
        {
            Serilog.Log.Error(exception, "Unified library import failed.");
            ImportHint.Text = "Não foi possível concluir: " + exception.Message;
            ImportHint.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 176, 141));
            label.Content = "Importação não concluída. Confira a mensagem acima do botão e tente novamente.";
        }
        finally { importBusy = false; grid.IsEnabled = true; progressBar.Visibility = Visibility.Collapsed; }
    }

    internal void PreparePreview(string hero)
    {
        var app = new AppEntry { Name = "Call of Duty: Modern Warfare III", Aumid = "preview!cod", Executable = "bootstrapper.exe", InSteam = true };
        linkedSteamIds[app.Aumid] = 3595270;
        string cover = hero.Replace("_hero.png", "p.png");
        if (File.Exists(cover)) app.Icon = cover;
        Apps.Entries.Add(app);
        Apps.Entries.Add(new AppEntry { Name = "Forza Horizon 5", Aumid = "preview!forza" });
        Apps.Entries.Add(new AppEntry { Name = "Halo Infinite", Aumid = "preview!halo" });
        listGames.ItemsSource = Apps.Entries; listGames.SelectedItem = app; GameCount.Text = "3 jogos";
        ShowEdition(new(3595270, app.Name, "Na sequência direta de Call of Duty: Modern Warfare II, o Capitão Price e a Força-Tarefa 141 enfrentam a maior ameaça até agora. Prepare sua próxima sessão com a campanha, o multijogador e o modo Zumbis.", "Sledgehammer Games, Treyarch", "Activision", "10 de novembro de 2023", "Português (Brasil), Inglês, Espanhol e outros.\n* Idiomas com suporte completo de áudio."));
        if (File.Exists(hero)) HeroImage.Source = new BitmapImage(new Uri(Path.GetFullPath(hero)));
        label.Content = "Prévia da interface · dados de demonstração";
    }
}
