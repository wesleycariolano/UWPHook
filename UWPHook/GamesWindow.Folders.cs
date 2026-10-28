using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace UWPHook;

public partial class GamesWindow
{
    private List<AppEntry> registeredGames = [];
    private bool initializingPreferences;
    private void InitializeBrand()
    {
        Title = AppIdentity.Name + " · " + AppIdentity.Version;
        TitleText.Text = LogoWordmark.Text = AppIdentity.Name;
        VersionText.Text = "VERSÃO " + AppIdentity.Version;
        AboutTitle.Text = "SOBRE O " + AppIdentity.Name + " " + AppIdentity.Version;
        initializingPreferences = true;
        try { LinkedStateToggle.IsChecked = UiPreferences.Current.ShowLinkedState; }
        finally { initializingPreferences = false; }
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (importBusy || bwrLoad?.IsBusy == true) return;
        var picker = new OpenFolderDialog { Title = "Selecione a pasta do jogo", Multiselect = false };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            string folder = picker.FolderName;
            var game = ManualGameStore.RegisteredGame(folder, registeredGames);
            if (game == null)
            {
                var files = await Task.Run(() => ManualGameStore.Executables(folder));
                if (files.Count == 0) throw new IOException("Não encontrei o executável do jogo. Selecione a pasta que contém os arquivos do jogo.");
                string executable;
                if (files.Count == 1) executable = files[0];
                else
                {
                    var executablePicker = new OpenFileDialog { Title = "Escolha o executável principal do jogo", Filter = "Executável do jogo (*.exe)|*.exe",
                        InitialDirectory = folder, FileName = files[0], CheckFileExists = true };
                    if (executablePicker.ShowDialog(this) != true) return;
                    executable = executablePicker.FileName;
                }
                game = ManualGameStore.Create(folder, executable);
            }
            game.IsGame = true;
            ManualGameStore.Default.Save(game);
            var existing = Apps.Entries.FirstOrDefault(item => item.Aumid == game.Aumid);
            if (existing == null) Apps.Entries.Add(game);
            textBox.Text = "";
            listGames.SelectedItem = existing ?? game;
            listGames.ScrollIntoView(existing ?? game);
            RefreshLibraryState();
            label.Content = "Jogo adicionado por pasta. Confira a edição e vincule à Steam.";
        }
        catch (Exception error) { label.Content = error.Message; MessageBox.Show(this, error.Message, AppIdentity.Name, MessageBoxButton.OK, MessageBoxImage.Information); }
    }

    private async Task LaunchFolderGame(string identity)
    {
        Hide();
        try
        {
            var game = ManualGameStore.Default.Load().SingleOrDefault(app => app.Aumid == identity)
                ?? throw new IOException("O jogo por pasta não foi encontrado. Abra o sGs-app e selecione sua pasta novamente.");
            var validated = ManualGameStore.Create(game.InstallDirectory, game.ExecutablePath);
            using var process = Process.Start(new ProcessStartInfo(validated.ExecutablePath) { WorkingDirectory = Path.GetDirectoryName(validated.ExecutablePath), UseShellExecute = true });
            if (process != null) await process.WaitForExitAsync();
        }
        catch (Exception error) { MessageBox.Show(error.Message, AppIdentity.Name, MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { Close(); }
    }

    private void LinkedState_Changed(object sender, RoutedEventArgs e)
    {
        if (initializingPreferences || previewMode || Apps == null || LinkedStateToggle == null) return;
        UiPreferences.Current.ShowLinkedState = LinkedStateToggle.IsChecked == true;
        try { UiPreferences.Current.Save(); }
        catch (IOException) { label.Content = "Não foi possível salvar essa preferência."; }
        foreach (var app in Apps.Entries) app.RefreshStatus();
        UpdateLinkAction();
    }

    private void UpdateLinkAction()
    {
        if (currentGame == null || listGames.SelectedItem is not AppEntry app)
        {
            ImportButton.IsEnabled = false; UpdateLinkButton.Visibility = Visibility.Collapsed; return;
        }
        bool linked = app.InSteam && linkedSteamIds.GetValueOrDefault(app.Aumid) == currentGame.Id;
        bool showLinked = linked && UiPreferences.Current.ShowLinkedState;
        if (linked && !importBusy) ImportHint.Text = "Este jogo já está vinculado à sua Steam. Você pode atualizar o vínculo quando quiser.";
        ImportButton.Content = showLinked ? "✓  Vinculado" : linked ? "Atualizar na Steam  →" : "Vincular à Steam  →";
        ImportButton.IsEnabled = !showLinked;
        ImportButton.Tag = showLinked ? "linked" : null;
        ImportButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString(showLinked ? "#243B30" : "#B7EE85") is System.Windows.Media.Color bg ? bg : System.Windows.Media.Colors.Transparent);
        ImportButton.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString(showLinked ? "#B7EE85" : "#17231A") is System.Windows.Media.Color fg ? fg : System.Windows.Media.Colors.White);
        UpdateLinkButton.Visibility = showLinked ? Visibility.Visible : Visibility.Collapsed;
    }
}
