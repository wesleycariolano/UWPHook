using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UWPHook.SteamGridDb;

namespace UWPHook;

public partial class ArtworkPickerWindow : Window
{
    private readonly string apiKey;
    private readonly Func<SteamGridDbApi> createApi;
    internal bool UseSelectedTitle => UseTitle.IsChecked == true;
    internal GameResponse? SelectedGame { get; private set; }
    internal ArtworkPickerWindow(string appName, string apiKey, Func<SteamGridDbApi>? createApi = null)
    {
        InitializeComponent();
        this.apiKey = apiKey;
        this.createApi = createApi ?? (() => new SteamGridDbApi(this.apiKey));
        AppTitle.Text = appName;
        SearchText.Text = appName.Replace("®", "").Replace("™", "").Trim();
    }
    private async void Window_Loaded(object sender, RoutedEventArgs e) => await SearchAsync();
    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync();
    private async void SearchText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SearchButton.IsEnabled)
        {
            e.Handled = true;
            await SearchAsync();
        }
    }
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText.Text))
        {
            Status.Text = "Enter a title, for example Modern Warfare III.";
            return;
        }
        SearchButton.IsEnabled = SearchText.IsEnabled = false;
        Results.ItemsSource = null;
        ChooseButton.IsEnabled = false;
        Status.Text = "Searching SteamGridDB…";
        try
        {
            using var api = createApi();
            var games = await api.SearchGame(SearchText.Text);
            Results.ItemsSource = games;
            Status.Text = games.Length == 0
                ? "No games found. Try another title or skip artwork."
                : "Select the correct title and release year. No result is chosen automatically.";
        }
        catch (Exception)
        {
            // Request details can contain sensitive authentication information.
            Status.Text = "Could not search SteamGridDB. Check the API key in Settings and your connection, then retry or skip artwork.";
        }
        finally { SearchButton.IsEnabled = SearchText.IsEnabled = true; }
    }
    private void Results_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ChooseButton.IsEnabled = Results.SelectedItem is GameResponse;
    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        if (Results.SelectedItem is not GameResponse game) return;
        SelectedGame = game;
        DialogResult = true;
    }
}


