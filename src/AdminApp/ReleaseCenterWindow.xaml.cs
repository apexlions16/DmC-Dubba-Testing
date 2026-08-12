using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class ReleaseCenterWindow : Window
{
    private readonly PlatformApiClient _api;
    private string? _filePath;

    public ReleaseCenterWindow(PlatformApiClient api)
    {
        InitializeComponent();
        _api = api;
        Loaded += ReleaseCenterWindow_Loaded;
    }

    private async void ReleaseCenterWindow_Loaded(object sender, RoutedEventArgs e)
        => await ReloadHistoryAsync("stable");

    private async Task ReloadHistoryAsync(string channel)
    {
        try
        {
            var history = await _api.GetClientReleaseHistoryAsync(channel);
            HistoryGrid.ItemsSource = history.Select(item => new
            {
                item.Version,
                item.Title,
                Mandatory = item.Mandatory ? "Evet" : "Hayır",
                Date = TurkishUi.Date(item.PublishedAt)
            }).ToList();
        }
        catch (QaApiException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void HistoryChannelPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }
        var channel = (HistoryChannelPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "stable";
        await ReloadHistoryAsync(channel);
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "İstemci güncelleme ZIP paketini seçin",
            Filter = "ZIP Paketleri|*.zip"
        };
        if (dialog.ShowDialog(this) == true)
        {
            _filePath = dialog.FileName;
            FileInput.Text = dialog.FileName;
        }
    }

    private async void PublishButton_Click(object sender, RoutedEventArgs e)
    {
        var version = VersionInput.Text.Trim();
        var title = TitleInput.Text.Trim();
        if (version.Length == 0 || title.Length == 0 || string.IsNullOrWhiteSpace(_filePath))
        {
            MessageBox.Show("Sürüm, başlık ve ZIP payload zorunludur.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var channel = (ChannelPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "stable";
        PublishButton.IsEnabled = false;
        UploadProgress.Value = 0;
        try
        {
            var progress = new Progress<double>(value =>
            {
                UploadProgress.Value = value;
                StatusText.Text = $"İstemci paketi yükleniyor... %{value:0}";
            });
            var result = await _api.PublishClientReleaseAsync(
                _filePath,
                channel,
                version,
                title,
                NotesInput.Text.Trim(),
                string.IsNullOrWhiteSpace(MinimumVersionInput.Text) ? null : MinimumVersionInput.Text.Trim(),
                MandatoryCheck.IsChecked == true,
                progress);
            StatusText.Text = $"{TurkishUi.ReleaseChannel(result.Channel)} {result.Version} yayınlandı. SHA-256: {result.Sha256}";
            VersionInput.Clear();
            TitleInput.Clear();
            NotesInput.Clear();
            MinimumVersionInput.Clear();
            MandatoryCheck.IsChecked = false;
            FileInput.Clear();
            _filePath = null;
            await ReloadHistoryAsync(channel);
            MessageBox.Show(
                "İstemci sürümü yayınlandı. Aynı launcher EXE bir sonraki açılışta bu paketi algılayıp indirecektir.",
                "Yayınlandı",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (QaApiException ex)
        {
            StatusText.Text = ex.Message;
            MessageBox.Show(ex.Message, "İstemci Sürümü Yayınlanamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            PublishButton.IsEnabled = true;
        }
    }
}
