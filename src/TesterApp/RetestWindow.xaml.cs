using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using DmC.Qa.Shared;
using Microsoft.Win32;

namespace DmC.Qa.Tester;

public partial class RetestWindow : Window
{
    private readonly QaApiClient _api;
    private readonly List<RetestAssignment> _assignments;
    private string? _selectedEvidencePath;
    private string? _evidenceUploadedForRequestId;

    public RetestWindow(QaApiClient api, IReadOnlyList<RetestAssignment> assignments)
    {
        InitializeComponent();
        _api = api;
        _assignments = assignments.ToList();
        RefreshList();
    }

    public bool AnyResultSubmitted { get; private set; }

    private RetestAssignment? SelectedAssignment => RetestList.SelectedItem as RetestAssignment;

    private void RefreshList()
    {
        RetestList.ItemsSource = null;
        RetestList.ItemsSource = _assignments;
        RetestList.DisplayMemberPath = nameof(RetestAssignment.BugKey);
        PendingCountText.Text = $"{_assignments.Count} bekleyen";

        if (_assignments.Count > 0)
        {
            RetestList.SelectedIndex = 0;
        }
        else
        {
            ClearDetails();
            StatusText.Text = "Bekleyen yeniden testiniz kalmadı.";
        }
    }

    private void RetestList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var assignment = SelectedAssignment;
        _selectedEvidencePath = null;
        _evidenceUploadedForRequestId = null;
        EvidenceText.Text = "Video seçilmedi";
        PassedOption.IsChecked = false;
        FailedOption.IsChecked = false;
        UncertainOption.IsChecked = false;
        CommentInput.Clear();
        StatusText.Text = string.Empty;

        if (assignment is null)
        {
            ClearDetails();
            return;
        }

        BugKeyText.Text = assignment.BugKey;
        BugTitleText.Text = assignment.Title;
        BuildText.Text = assignment.BuildVersion;
        DeadlineText.Text = assignment.DeadlineAt is null
            ? "Son tarih belirtilmedi"
            : $"{TurkishUi.Deadline(assignment.DeadlineAt)} • {TurkishUi.Date(assignment.DeadlineAt, includeTime: true)}";
        AdminNoteText.Text = string.IsNullOrWhiteSpace(assignment.Note)
            ? "Yönetici notu bulunmuyor."
            : assignment.Note;
        SubmitButton.IsEnabled = true;
    }

    private void SelectEvidenceButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAssignment is null)
        {
            StatusText.Text = "Önce bir yeniden test seçin.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Yeniden test videosu veya kanıtı seçin",
            Filter = "Desteklenen dosyalar|*.mp4;*.mov;*.mkv;*.webm;*.avi;*.png;*.jpg;*.jpeg|Video dosyaları|*.mp4;*.mov;*.mkv;*.webm;*.avi|Görseller|*.png;*.jpg;*.jpeg|Tüm dosyalar|*.*",
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _selectedEvidencePath = dialog.FileName;
        _evidenceUploadedForRequestId = null;
        var info = new FileInfo(dialog.FileName);
        EvidenceText.Text = $"{info.Name} • {TurkishUi.FileSize(info.Length)}";
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        var assignment = SelectedAssignment;
        if (assignment is null)
        {
            StatusText.Text = "Önce bir yeniden test seçin.";
            return;
        }

        var result = SelectedResult();
        if (result is null)
        {
            StatusText.Text = "Lütfen yeniden test sonucunu seçin.";
            return;
        }

        SubmitButton.IsEnabled = false;
        try
        {
            if (!string.IsNullOrWhiteSpace(_selectedEvidencePath) &&
                !string.Equals(_evidenceUploadedForRequestId, assignment.Id, StringComparison.Ordinal))
            {
                StatusText.Text = "Video yükleniyor...";
                SubmitButton.Content = "Video Yükleniyor...";
                await _api.UploadRetestEvidenceAsync(assignment.Id, _selectedEvidencePath);
                _evidenceUploadedForRequestId = assignment.Id;
            }

            StatusText.Text = "Yeniden test sonucu kaydediliyor...";
            SubmitButton.Content = "Sonuç Kaydediliyor...";
            await _api.SubmitRetestAsync(
                assignment.Id,
                assignment.BuildId,
                result.Value,
                CommentInput.Text.Trim());

            AnyResultSubmitted = true;
            _assignments.RemoveAll(item => item.Id == assignment.Id);
            MessageBox.Show(
                $"{assignment.BugKey} için yeniden test sonucunuz kaydedildi.",
                "Yeniden Test Kaydedildi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RefreshList();
        }
        catch (QaApiException ex)
        {
            StatusText.Text = ex.Message;
        }
        catch (HttpRequestException)
        {
            StatusText.Text = "Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.";
        }
        catch (TaskCanceledException)
        {
            StatusText.Text = "İşlem zamanında tamamlanamadı. Lütfen tekrar deneyin.";
        }
        finally
        {
            SubmitButton.Content = "Sonucu Gönder";
            SubmitButton.IsEnabled = SelectedAssignment is not null;
        }
    }

    private RetestResult? SelectedResult()
    {
        if (PassedOption.IsChecked == true)
        {
            return RetestResult.Passed;
        }
        if (FailedOption.IsChecked == true)
        {
            return RetestResult.Failed;
        }
        if (UncertainOption.IsChecked == true)
        {
            return RetestResult.Uncertain;
        }
        return null;
    }

    private void ClearDetails()
    {
        BugKeyText.Text = "—";
        BugTitleText.Text = "Bekleyen yeniden test yok";
        BuildText.Text = "—";
        DeadlineText.Text = "—";
        AdminNoteText.Text = "—";
        EvidenceText.Text = "Video seçilmedi";
        PassedOption.IsChecked = false;
        FailedOption.IsChecked = false;
        UncertainOption.IsChecked = false;
        CommentInput.Clear();
        SubmitButton.IsEnabled = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = AnyResultSubmitted;
        Close();
    }
}
