using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UWPHook.LibraryDetails;

namespace UWPHook;

public partial class LibraryDetailsWindow : Window
{
    private readonly AppEntry app;
    private readonly SteamCatalog catalog = new();
    private SteamGameDetails? selected;
    private int requestVersion;
    private readonly string steam;

    internal LibraryDetailsWindow(AppEntry app)
    {
        InitializeComponent();
        this.app = app;
        steam = SteamManager.GetSteamFolder();
        AppTitle.Text = app.Name;
        SearchText.Text = app.Name.Replace("®", "").Replace("™", "");
        RefreshComponent();
        Closed += (_, _) => { requestVersion++; catalog.Dispose(); };
    }

    private void RefreshComponent() => ComponentStatus.Text =
        Directory.Exists(steam) && CompanionInstaller.IsInstalled(steam)
            ? "Componente incluído instalado. Reabra a Steam após salvar a ficha."
            : "A ficha completa ainda não está ativada na Steam.";

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await SearchAsync();
        try
        {
            var saved = DetailsStore.Default.Find(app.Aumid);
            if (saved != null) Results.SelectedItem = Results.Items.OfType<SteamGame>().FirstOrDefault(game => game.Id == saved.Id);
        }
        catch (Exception) { Status.Text = "Não foi possível ler a ficha salva. Confira steam-details.json antes de salvar outra escolha."; }
    }
    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync();
    private async void SearchText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SearchButton.IsEnabled) { e.Handled = true; await SearchAsync(); }
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText.Text)) return;
        int version = ++requestVersion;
        selected = null;
        SaveButton.IsEnabled = false;
        SearchButton.IsEnabled = false;
        Status.Text = "Pesquisando títulos na Steam…";
        try
        {
            var games = await catalog.Search(SearchText.Text);
            if (version != requestVersion) return;
            Results.ItemsSource = games;
            Status.Text = games.Length == 0 ? "Nenhum título encontrado. Use o nome completo ou cole a URL da loja Steam." : "Escolha a edição correta para conferir a descrição e os idiomas.";
        }
        catch (Exception) { if (version == requestVersion) Status.Text = "Não foi possível consultar a Steam. Confira a conexão e tente novamente."; }
        finally { SearchButton.IsEnabled = true; }
    }

    private async void Results_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int version = ++requestVersion;
        selected = null;
        SaveButton.IsEnabled = false;
        if (Results.SelectedItem is not SteamGame game) return;
        Status.Text = "Carregando a ficha em português…";
        try
        {
            var data = await catalog.Details(game.Id);
            if (version != requestVersion) return;
            selected = data;
            GameTitle.Text = data.Name;
            Description.Text = data.Description;
            Credits.Text = $"Desenvolvimento: {data.Developers}\nPublicação: {data.Publishers}\nLançamento: {data.Release}";
            Languages.Text = "Idiomas informados pela Steam:\n" + data.Languages;
            SaveButton.IsEnabled = true;
            Status.Text = "Confira os dados e salve a ficha para este jogo.";
        }
        catch (Exception) { if (version == requestVersion) Status.Text = "A Steam não retornou os detalhes desse título. Escolha outro resultado ou tente novamente."; }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (selected == null) return;
        try
        {
            DetailsStore.Default.Save(app.Aumid, new(selected.Id, selected.Name, selected.Release));
            bool linked = DetailsLinker.ApplyExisting(app);
            Status.Text = linked ? "Ficha vinculada ao atalho existente. Ative o componente e reabra a Steam."
                : "Ficha salva. Exporte este jogo para a Steam e ative o componente incluído.";
        }
        catch (Exception) { Status.Text = "Não foi possível salvar a ficha. Confira a pasta de configurações e o backup do componente."; }
    }

    private async void Activate_Click(object sender, RoutedEventArgs e)
    {
        DetailsGrid.IsEnabled = false;
        Status.Text = "Ativando os componentes incluídos…";
        try { await Task.Run(() => CompanionInstaller.InstallBundled(steam)); Status.Text = "Ficha ativada. Salve o título escolhido e reabra a Steam."; }
        catch (UnauthorizedAccessException)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--install-library-details")
                { UseShellExecute = true, Verb = "runas" });
                if (process != null) await process.WaitForExitAsync();
                Status.Text = CompanionInstaller.IsInstalled(steam) ? "Componentes instalados. Salve a ficha e reabra a Steam." : "A instalação não foi concluída.";
            }
            catch (Win32Exception) { Status.Text = "Ativação cancelada na confirmação do Windows."; }
        }
        catch (Exception exception) { Status.Text = exception.Message; }
        finally { DetailsGrid.IsEnabled = true; RefreshComponent(); }
    }

    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CompanionInstaller.Disable(steam, () => Process.GetProcessesByName("steam").Length > 0);
            Status.Text = "Ficha desativada. Os jogos e os outros plugins foram preservados.";
            RefreshComponent();
        }
        catch (Exception exception) { Status.Text = exception.Message; }
    }
}
