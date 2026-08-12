using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using DmC.Qa.Shared;
using Microsoft.Win32;

namespace DmC.Qa.Tester;

public partial class BugReportWindow : Window
{
    private readonly QaApiClient _api;
    private readonly ProjectSummary _project;
    private readonly TaskSummary? _task;
    private readonly BuildSummary? _build;
    private string? _selectedEvidencePath;
    private BugCreatedResponse? _createdBug;

    public BugReportWindow(
        QaApiClient api,
        ProjectSummary project,
        TaskSummary? task,
        BuildSummary? build)
    {
        InitializeComponent();
        _api = api;
        _project = project;
        _task = task;
        _build = build;

        ProjectText.Text = project.Name;
        TaskText.Text = task?.Title ?? "Göreve bağlı değil";
        BuildText.Text = build is null ? "Test sürümü seçili değil" : $"{build.Version} — {build.Title}";

        BugTypePicker.ItemsSource = new[]
        {
            "Eksik Türkçe Ses",
            "Yanlış Replik / Yanlış Ses",
            "Senkron Problemi",
            "Ses Seviyesi / Mix Problemi",
            "Ses Kalitesi Problemi",
            "Kesilen Replik",
            "Tekrarlanan Replik",
            "Teknik Problem",
            "Diğer"
        };
        BugTypePicker.SelectedIndex = 0;

        TriggerPicker.ItemsSource = new[]
        {
            "Normal oynanış",
            "Ara sahne atlandıktan sonra",
            "Ölüm / Checkpoint sonrası",
            "Mission yeniden başlatıldıktan sonra",
            "Dövüş çok hızlı bitirildiğinde",
            "Dövüş uzun sürdüğünde",
            "Boss / sahne geçişinde",
            "Bilinmiyor / Emin değilim"
        };
        TriggerPicker.SelectedIndex = 0;
    }

    public string? CreatedBugKey => _createdBug?.Key;

    private void SelectVideoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Video veya kanıt dosyası seçin",
            Filter = "Desteklenen dosyalar|*.mp4;*.mov;*.mkv;*.webm;*.avi;*.png;*.jpg;*.jpeg|Video dosyaları|*.mp4;*.mov;*.mkv;*.webm;*.avi|Görseller|*.png;*.jpg;*.jpeg|Tüm dosyalar|*.*",
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _selectedEvidencePath = dialog.FileName;
        var fileInfo = new FileInfo(dialog.FileName);
        VideoFileText.Text = $"{fileInfo.Name} • {TurkishUi.FileSize(fileInfo.Length)}";
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        int? attempts = null;
        int? hits = null;
        double? timestampSeconds = null;

        if (_createdBug is null && !TryValidateForm(out attempts, out hits, out timestampSeconds))
        {
            return;
        }

        SubmitButton.IsEnabled = false;

        try
        {
            if (_createdBug is null)
            {
                StatusText.Text = "Hata raporu oluşturuluyor...";
                _createdBug = await _api.SubmitBugAsync(new
                {
                    ProjectId = _project.Id,
                    TaskId = _task?.Id,
                    SectionId = _task?.SectionId,
                    ReportedBuildId = _build?.Id,
                    Title = TitleInput.Text.Trim(),
                    BugType = BugTypePicker.SelectedItem?.ToString() ?? "Diğer",
                    Trigger = TriggerPicker.SelectedItem?.ToString(),
                    Description = DescriptionInput.Text.Trim(),
                    ReproAttempts = attempts,
                    ReproHits = hits,
                    BugTimestampSeconds = timestampSeconds
                });

                StatusText.Text = $"{_createdBug.Key} oluşturuldu.";
            }

            if (!string.IsNullOrWhiteSpace(_selectedEvidencePath))
            {
                StatusText.Text = $"{_createdBug.Key} oluşturuldu. Video yükleniyor...";
                SubmitButton.Content = "Video Yükleniyor...";
                await _api.UploadBugEvidenceAsync(_createdBug.Id, _selectedEvidencePath);
            }

            StatusText.Text = $"{_createdBug.Key} başarıyla gönderildi.";
            MessageBox.Show(
                $"Hata raporunuz başarıyla gönderildi.\n\nRapor: {_createdBug.Key}",
                "Rapor Gönderildi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (QaApiException ex)
        {
            if (_createdBug is not null)
            {
                StatusText.Text = $"{_createdBug.Key} oluşturuldu ancak video yüklenemedi: {ex.Message}";
                SubmitButton.Content = "Videoyu Tekrar Yükle";
            }
            else
            {
                StatusText.Text = ex.Message;
                SubmitButton.Content = "Raporu Gönder";
            }
        }
        catch (IOException)
        {
            StatusText.Text = _createdBug is null
                ? "Kanıt dosyası başka bir program tarafından kullanılıyor. Dosyayı kullanan programı kapatıp tekrar deneyin."
                : $"{_createdBug.Key} oluşturuldu ancak kanıt dosyası başka bir program tarafından kullanıldığı için yüklenemedi.";
            SubmitButton.Content = _createdBug is null ? "Raporu Gönder" : "Videoyu Tekrar Yükle";
        }
        catch (HttpRequestException)
        {
            StatusText.Text = _createdBug is null
                ? "Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin."
                : $"{_createdBug.Key} oluşturuldu ancak video yüklenemedi. İnternet bağlantınızı kontrol edip tekrar deneyin.";
            SubmitButton.Content = _createdBug is null ? "Raporu Gönder" : "Videoyu Tekrar Yükle";
        }
        catch (TaskCanceledException)
        {
            StatusText.Text = "İşlem zamanında tamamlanamadı. Lütfen tekrar deneyin.";
            SubmitButton.Content = _createdBug is null ? "Raporu Gönder" : "Videoyu Tekrar Yükle";
        }
        finally
        {
            SubmitButton.IsEnabled = true;
        }
    }

    private bool TryValidateForm(out int? attempts, out int? hits, out double? timestampSeconds)
    {
        attempts = null;
        hits = null;
        timestampSeconds = null;

        if (TitleInput.Text.Trim().Length < 3)
        {
            StatusText.Text = "Lütfen hatayı anlatan kısa bir başlık yazın.";
            TitleInput.Focus();
            return false;
        }

        if (DescriptionInput.Text.Trim().Length < 3)
        {
            StatusText.Text = "Lütfen karşılaştığınız problemi birkaç cümleyle açıklayın.";
            DescriptionInput.Focus();
            return false;
        }

        if (!TryParseOptionalInteger(ReproAttemptsInput.Text, out attempts) ||
            !TryParseOptionalInteger(ReproHitsInput.Text, out hits))
        {
            StatusText.Text = "Tekrar edilebilirlik alanlarına 0 ile 100 arasında bir sayı yazın.";
            return false;
        }

        if (attempts is not null && hits is not null && hits > attempts)
        {
            StatusText.Text = "Sorunun oluştuğu deneme sayısı toplam deneme sayısından büyük olamaz.";
            return false;
        }

        try
        {
            timestampSeconds = ParseTimestamp(TimestampInput.Text);
        }
        catch (FormatException ex)
        {
            StatusText.Text = ex.Message;
            TimestampInput.Focus();
            return false;
        }

        return true;
    }

    private static bool TryParseOptionalInteger(string value, out int? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!int.TryParse(value.Trim(), NumberStyles.Integer, TurkishUi.Culture, out var number) || number < 0 || number > 100)
        {
            return false;
        }

        parsed = number;
        return true;
    }

    private static double? ParseTimestamp(string value)
    {
        var raw = value.Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        if (TimeSpan.TryParse(raw, TurkishUi.Culture, out var timeSpan) && timeSpan >= TimeSpan.Zero)
        {
            return timeSpan.TotalSeconds;
        }

        if (raw.Count(character => character == ':') == 1 &&
            TimeSpan.TryParse($"00:{raw}", TurkishUi.Culture, out timeSpan) &&
            timeSpan >= TimeSpan.Zero)
        {
            return timeSpan.TotalSeconds;
        }

        if (double.TryParse(raw, NumberStyles.Float, TurkishUi.Culture, out var seconds) && seconds >= 0)
        {
            return seconds;
        }

        throw new FormatException("Video zamanını 01:43, 00:01:43 veya saniye olarak yazın.");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
