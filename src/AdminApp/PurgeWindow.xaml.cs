using System.Windows;
using System.Windows.Controls;
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
        ProjectNameInput.Text = project?.Name ?? string.Empty;
        Loaded += PurgeWindow_Loaded;
    }

    public bool ProjectPurged { get; private set; }

    private async void PurgeWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UiMotion.Reveal(PurgeRoot, 18);
        if (_project is not null)
        {
            try
            {
                var preview = await _api.GetPurgePreviewAsync(_project.Id);
                PreviewText.Text =
                    $"Proje: {preview.ProjectName}\nDurum: {TurkishUi.Status(preview.Status)}\n\n" +
                    $"Hata raporu: {preview.BugReports}\nVideo/kanıt: {preview.EvidenceFiles}\nYeniden test: {preview.Retests}\n" +
                    $"Görev: {preview.Tasks}\nTest sürümü: {preview.Builds}\nProje üyesi: {preview.ProjectMembers}\n" +
                    $"Tahmini storage: {TurkishUi.FileSize(preview.EstimatedStorageBytes)}";
                CreateRequestButton.IsEnabled = preview.Status == "closed";
            }
            catch (QaApiException ex)
            {
                PreviewText.Text = ex.Message;
                CreateRequestButton.IsEnabled = false;
            }
        }
        else
        {
            PreviewText.Text = "Mevcut bir proje seçilmeden yeni silme talebi oluşturulamaz. İkinci yönetici onayı yine bu ekrandan yapılabilir.";
            CreateRequestButton.IsEnabled = false;
        }
        await ReloadRequestsAsync();
    }

    private async Task ReloadRequestsAsync()
    {
        try
        {
            var requests = await _api.GetPurgeRequestsAsync();
            RequestsGrid.ItemsSource = requests.Select(item => new RequestRow(item)).ToList();
        }
        catch (QaApiException ex)
        {
            ApprovalStatusText.Text = ex.Message;
        }
    }

    private async void CreateRequestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            return;
        }
        var confirm = MessageBox.Show(
            "Bu adım henüz dosya silmez; ikinci bir yöneticinin kalıcı silmeyi onaylayabilmesi için kritik talep oluşturur. Devam edilsin mi?",
            "Kalıcı Silme Talebi",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }
        CreateRequestButton.IsEnabled = false;
        try
        {
            var created = await _api.CreatePurgeRequestAsync(
                _project.Id,
                ProjectNameInput.Text.Trim(),
                DeleteStorageCheck.IsChecked == true,
                DeleteDatabaseCheck.IsChecked == true,
                DeleteTombstoneCheck.IsChecked == true);
            ConfirmationCodeText.Text = created.ConfirmationCode;
            CodePanel.Visibility = Visibility.Visible;
            UiMotion.Reveal(CodePanel, 10);
            await ReloadRequestsAsync();
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Silme Talebi Oluşturulamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
            CreateRequestButton.IsEnabled = true;
        }
    }

    private void RequestsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RequestsGrid.SelectedItem is RequestRow row)
        {
            ApproveProjectNameInput.Text = row.Source.ProjectName;
            ApprovalStatusText.Text = $"Talep eden: {row.Source.RequestedBy} • {TurkishUi.Date(row.Source.RequestedAt)}";
        }
    }

    private async void ApproveButton_Click(object sender, RoutedEventArgs e)
    {
        if (RequestsGrid.SelectedItem is not RequestRow row)
        {
            MessageBox.Show("Onaylanacak silme talebini seçin.", "Talep Seçilmedi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var finalConfirm = MessageBox.Show(
            "Bu ikinci ve son onaydır. HF dosyaları ile işaretlenen Supabase proje verileri kalıcı olarak silinecektir. Bu işlem geri alınamaz. Devam edilsin mi?",
            "SON KALICI SİLME ONAYI",
            MessageBoxButton.YesNo,
            MessageBoxImage.Stop);
        if (finalConfirm != MessageBoxResult.Yes)
        {
            return;
        }
        ApproveButton.IsEnabled = false;
        ApprovalStatusText.Text = "HF ve Supabase verileri kalıcı olarak temizleniyor...";
        try
        {
            await _api.ApprovePurgeRequestAsync(row.Source.Id, ApproveProjectNameInput.Text.Trim(), ApproveCodeInput.Text.Trim());
            ProjectPurged = _project?.Id == row.Source.ProjectId;
            ApprovalStatusText.Text = "Kalıcı silme tamamlandı. Proje artık uygulama verilerinde bulunmuyor.";
            MessageBox.Show(
                "Kalıcı silme tamamlandı. Proje dosyaları ve seçilen veritabanı kayıtları kaldırıldı; proje uygulama listesinden de çıkarılacak.",
                "Silme Tamamlandı",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (ProjectPurged)
            {
                DialogResult = true;
                Close();
                return;
            }

            await ReloadRequestsAsync();
        }
        catch (QaApiException ex)
        {
            ApprovalStatusText.Text = ex.Message;
            MessageBox.Show(ex.Message, "Kalıcı Silme Başarısız", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (IsVisible)
            {
                ApproveButton.IsEnabled = true;
            }
        }
    }

    private sealed class RequestRow
    {
        public RequestRow(PurgeRequestItem source)
        {
            Source = source;
            ProjectName = source.ProjectName;
            RequestedBy = source.RequestedBy;
            StatusText = TurkishUi.Status(source.Status);
        }
        public PurgeRequestItem Source { get; }
        public string ProjectName { get; }
        public string RequestedBy { get; }
        public string StatusText { get; }
    }
}
