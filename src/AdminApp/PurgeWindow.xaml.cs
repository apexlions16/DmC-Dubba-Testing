using System.Windows;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class PurgeWindow : Window
{
    private readonly PlatformApiClient _api;
    private readonly ProjectSummary? _project;

    public PurgeWindow(PlatformApiClient api, ProjectSummary? project)
    {
        InitializeComponent();
        _api = api;
        _project = project;
        ExpectedProjectNameText.Text = project?.Name ?? "Proje seçilmedi";
        Loaded += PurgeWindow_Loaded;
    }

    public bool ProjectPurged { get; private set; }

    private async void PurgeWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UiMotion.Reveal(PurgeRoot, 18);
        if (_project is null)
        {
            PreviewText.Text = "Kalıcı silme için önce bir proje seçin.";
            DeleteProjectButton.IsEnabled = false;
            return;
        }

        try
        {
            var preview = await _api.GetPurgePreviewAsync(_project.Id);
            PreviewText.Text =
                $"Proje: {preview.ProjectName}\n" +
                $"Durum: {TurkishUi.Status(preview.Status)}\n\n" +
                $"Hata raporu: {preview.BugReports}\n" +
                $"Video / kanıt: {preview.EvidenceFiles}\n" +
                $"Yeniden test: {preview.Retests}\n" +
                $"Görev: {preview.Tasks}\n" +
                $"Test sürümü: {preview.Builds}\n" +
                $"Proje üyesi: {preview.ProjectMembers}\n" +
                $"Tahmini storage: {TurkishUi.FileSize(preview.EstimatedStorageBytes)}";
            StatusText.Text = preview.Status == "closed"
                ? "Proje kapalı. Kalıcı silmeye hazır."
                : "Proje aktif. Kalıcı silme sırasında otomatik olarak kapatılacak.";
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            PreviewText.Text = ex.Message;
            DeleteProjectButton.IsEnabled = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void DeleteProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            return;
        }

        var typedName = ProjectNameInput.Text.Trim();
        if (!string.Equals(typedName, _project.Name, StringComparison.Ordinal))
        {
            MessageBox.Show(
                "Proje adı birebir eşleşmiyor. Kalıcı silme başlatılmadı.",
                "Proje Adı Eşleşmiyor",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"'{_project.Name}' projesi şimdi tek yönetici onayıyla tamamen silinecek.\n\n" +
            "HF dosyaları, hata raporları, videolar, retestler, görevler ve proje veritabanı kayıtları geri alınamayacak şekilde kaldırılır.\n\nDevam edilsin mi?",
            "SON KALICI SİLME ONAYI",
            MessageBoxButton.YesNo,
            MessageBoxImage.Stop,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        DeleteProjectButton.IsEnabled = false;
        ProjectNameInput.IsEnabled = false;
        StatusText.Text = "HF ve Supabase proje verileri tamamen siliniyor...";
        try
        {
            await _api.DeleteProjectForCurrentBackendAsync(_project.Id, typedName);
            ProjectPurged = true;
            StatusText.Text = "Kalıcı silme tamamlandı.";
            MessageBox.Show(
                "Proje ve projeye ait bütün kayıtlar tamamen kaldırıldı. Proje artık uygulamada görünmeyecek.",
                "Proje Silindi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            StatusText.Text = "Kalıcı silme başarısız. Veriler korunmuştur.";
            MessageBox.Show(ex.Message, "Proje Silinemedi", MessageBoxButton.OK, MessageBoxImage.Error);
            DeleteProjectButton.IsEnabled = true;
            ProjectNameInput.IsEnabled = true;
        }
    }
}
