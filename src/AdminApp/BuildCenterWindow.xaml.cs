using System.Windows;
using Microsoft.Win32;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class BuildCenterWindow : Window
{
    private readonly PlatformApiClient _api;
    private readonly string _projectId;
    private readonly bool _isAdmin;
    private List<AdminBuildItem> _builds = [];
    private string? _filePath;

    public BuildCenterWindow(PlatformApiClient api, string projectId, bool isAdmin)
    {
        InitializeComponent();
        _api = api;
        _projectId = projectId;
        _isAdmin = isAdmin;
        PublishButton.IsEnabled = isAdmin;
        ArchiveButton.IsEnabled = isAdmin;
        Loaded += BuildCenterWindow_Loaded;
    }

    public bool Changed { get; private set; }

    private async void BuildCenterWindow_Loaded(object sender, RoutedEventArgs e)
        => await ReloadAsync();

    private async Task ReloadAsync()
    {
        try
        {
            _builds = (await _api.GetBuildsAsync(_projectId)).ToList();
            BuildsGrid.ItemsSource = _builds.Select(item => new BuildRow(item)).ToList();
            PublishButton.IsEnabled = false;
            ArchiveButton.IsEnabled = false;
            SelectedBuildInfo.Text = "Bir test sürümü seçin.";
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(ex.Message, "Test Sürümleri", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Test sürümü paketini seçin",
            Filter = "Arşiv ve Paketler|*.zip;*.7z;*.rar;*.pak;*.bin;*.exe|Tüm Dosyalar|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            _filePath = dialog.FileName;
            FileInput.Text = dialog.FileName;
        }
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        var version = VersionInput.Text.Trim();
        var title = TitleInput.Text.Trim();
        if (version.Length == 0 || title.Length == 0 || string.IsNullOrWhiteSpace(_filePath))
        {
            MessageBox.Show("Sürüm, başlık ve dosya alanları zorunludur.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UploadButton.IsEnabled = false;
        UploadProgress.Value = 0;
        try
        {
            UploadStatus.Text = "Test sürümü kaydı oluşturuluyor...";
            var created = await _api.CreateBuildAsync(
                _projectId,
                version,
                title,
                DescriptionInput.Text.Trim(),
                InstallInput.Text.Trim(),
                ChangelogInput.Text.Trim());
            var progress = new Progress<double>(value =>
            {
                UploadProgress.Value = value;
                UploadStatus.Text = $"Doğrudan Hugging Face'e yükleniyor... %{value:0}";
            });
            await _api.UploadBuildForCurrentBackendAsync(_projectId, created.Id, _filePath, progress);
            UploadStatus.Text = "Yükleme ve SHA-256 kaydı tamamlandı. Yönetici yayınlamadan testerlar bu sürümü güncel sürüm olarak görmez.";
            Changed = true;
            VersionInput.Clear();
            TitleInput.Clear();
            DescriptionInput.Clear();
            ChangelogInput.Clear();
            InstallInput.Clear();
            FileInput.Clear();
            _filePath = null;
            await ReloadAsync();
        }
        catch (QaApiException ex)
        {
            UploadStatus.Text = ex.Message;
            MessageBox.Show(ex.Message, "Test Sürümü Yüklenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            UploadButton.IsEnabled = true;
        }
    }

    private void BuildsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (BuildsGrid.SelectedItem is not BuildRow row)
        {
            PublishButton.IsEnabled = false;
            ArchiveButton.IsEnabled = false;
            return;
        }
        SelectedBuildInfo.Text = $"{row.Source.Version} • {TurkishUi.Status(row.Source.Status)}\n{row.Source.Description}\nKurulum: {row.Source.InstallationInstructions}";
        PublishButton.IsEnabled = _isAdmin && row.Source.Status != "current" && row.Source.OriginalFilename is not null;
        ArchiveButton.IsEnabled = _isAdmin && row.Source.Status is not "current" and not "archived" && row.Source.OriginalFilename is not null;
    }

    private async void PublishButton_Click(object sender, RoutedEventArgs e)
    {
        if (BuildsGrid.SelectedItem is not BuildRow row || !_isAdmin)
        {
            return;
        }
        var confirm = MessageBox.Show(
            $"{row.Source.Version} sürümünü tüm testerlar için Güncel Test Sürümü yapmak istiyor musunuz?",
            "Test Sürümünü Yayınla",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            await _api.PublishBuildAsync(_projectId, row.Source.Id);
            Changed = true;
            await ReloadAsync();
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Yayınlanamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (BuildsGrid.SelectedItem is not BuildRow row || !_isAdmin)
        {
            return;
        }
        var confirm = MessageBox.Show(
            $"{row.Source.Version} test sürümünü arşivlemek istiyor musunuz? Dosya proje içindeki archived alanına taşınacaktır.",
            "Test Sürümünü Arşivle",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            await _api.ArchiveBuildAsync(_projectId, row.Source.Id);
            Changed = true;
            await ReloadAsync();
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Arşivlenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private sealed class BuildRow
    {
        public BuildRow(AdminBuildItem source)
        {
            Source = source;
            Version = source.Version;
            Title = source.Title;
            StatusText = TurkishUi.Status(source.Status);
            UploadedBy = source.UploadedBy;
            Date = TurkishUi.Date(source.UploadedAt);
        }
        public AdminBuildItem Source { get; }
        public string Version { get; }
        public string Title { get; }
        public string StatusText { get; }
        public string UploadedBy { get; }
        public string Date { get; }
    }
}
