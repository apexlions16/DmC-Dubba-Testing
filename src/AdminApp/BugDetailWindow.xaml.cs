using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class BugDetailWindow : Window
{
    private readonly PlatformApiClient _api;
    private readonly string _bugId;
    private readonly bool _canAdminister;
    private AdminBugDetail? _detail;
    private string? _currentVideoPath;

    private static readonly StatusOption[] Statuses =
    [
        new("new", "Yeni / Henüz Başlanmadı"),
        new("acknowledged", "İncelendi"),
        new("in_progress", "Üzerinde Çalışılıyor"),
        new("retest_required", "Yeniden Test Bekliyor"),
        new("resolved", "Çözüldü"),
        new("on_hold", "Beklemede"),
        new("duplicate", "Tekrar Rapor"),
        new("not_a_bug", "Bug Değil"),
        new("wont_fix", "Düzeltilmeyecek"),
        new("reopened", "Yeniden Açıldı")
    ];

    public BugDetailWindow(PlatformApiClient api, string bugId, bool canAdminister)
    {
        InitializeComponent();
        _api = api;
        _bugId = bugId;
        _canAdminister = canAdminister;
        StatusPicker.ItemsSource = Statuses;
        StatusPicker.DisplayMemberPath = nameof(StatusOption.Display);
        RootCausePicker.ItemsSource = new[]
        {
            "Eksik ses dosyası",
            "Yanlış asset / dosya eşleştirmesi",
            "Trigger / state problemi",
            "Checkpoint state problemi",
            "Timing / senkron",
            "Mix / ses seviyesi",
            "Entegrasyon problemi",
            "Oyun kaynaklı",
            "Belirsiz",
            "Diğer"
        };
        SaveStatusButton.IsEnabled = canAdminister;
        RetestButton.IsEnabled = canAdminister;
        Loaded += BugDetailWindow_Loaded;
        Closed += (_, _) => VideoPlayer.Close();
    }

    public bool Changed { get; private set; }

    private async void BugDetailWindow_Loaded(object sender, RoutedEventArgs e)
        => await ReloadAsync();

    private async Task ReloadAsync()
    {
        try
        {
            _detail = await _api.GetBugDetailAsync(_bugId);
            BugKeyText.Text = _detail.Key;
            TitleText.Text = _detail.Title;
            MetaText.Text = $"{_detail.ReporterName} • {_detail.BuildVersion ?? "Sürüm belirtilmedi"} • {_detail.BugType} • {TurkishUi.Date(_detail.CreatedAt)}";
            DescriptionText.Text = _detail.Description;
            ReproText.Text = _detail.ReproAttempts is > 0
                ? $"Tekrar üretme: {_detail.ReproHits ?? 0}/{_detail.ReproAttempts}"
                : "Tekrar üretme bilgisi belirtilmedi.";
            TimestampText.Text = _detail.BugTimestampSeconds is null
                ? "Video zaman kodu belirtilmedi."
                : $"Hata anı: {TimeSpan.FromSeconds(_detail.BugTimestampSeconds.Value):hh\:mm\:ss\.fff}";

            StatusPicker.SelectedItem = Statuses.FirstOrDefault(item => item.Value == _detail.Status) ?? Statuses[0];
            RootCausePicker.Text = _detail.RootCause ?? string.Empty;
            EvidenceList.ItemsSource = _detail.Evidence;
            EventsGrid.ItemsSource = _detail.Events.Select(item => new
            {
                Date = TurkishUi.Date(item.CreatedAt),
                Actor = item.ActorName,
                Event = EventText(item.EventType),
                Status = item.NewStatus is null ? "—" : TurkishUi.Status(item.NewStatus),
                Note = item.Note ?? ""
            }).ToList();
            VideoStatusText.Text = _detail.Evidence.Count == 0
                ? "Video/kanıt yok"
                : $"{_detail.Evidence.Count} dosya";
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(ex.Message, "Hata Detayı", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_canAdminister || _detail is null || StatusPicker.SelectedItem is not StatusOption selected)
        {
            return;
        }

        SaveStatusButton.IsEnabled = false;
        try
        {
            await _api.ChangeBugStatusAsync(
                _detail.Id,
                selected.Value,
                AdminNoteInput.Text.Trim(),
                string.IsNullOrWhiteSpace(RootCausePicker.Text) ? null : RootCausePicker.Text.Trim(),
                _detail.ReportedBuildId);
            Changed = true;
            AdminNoteInput.Clear();
            await ReloadAsync();
            MessageBox.Show("Hata kaydı güncellendi.", "Kaydedildi", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Durum Değiştirilemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SaveStatusButton.IsEnabled = _canAdminister;
        }
    }

    private async void EvidenceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EvidenceList.SelectedItem is not EvidenceItem evidence)
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "GameQaPlatform", "evidence", _bugId);
        var safeName = string.Concat(evidence.Filename.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var path = Path.Combine(directory, $"{evidence.Id}-{safeName}");
        try
        {
            VideoStatusText.Text = "İndiriliyor...";
            if (!File.Exists(path))
            {
                await _api.DownloadEvidenceAsync(evidence.Id, path);
            }
            _currentVideoPath = path;
            if (evidence.MediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                && Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                VideoPlayer.Source = new Uri(path);
                VideoPlayer.Position = _detail?.BugTimestampSeconds is double seconds
                    ? TimeSpan.FromSeconds(seconds)
                    : TimeSpan.Zero;
                VideoPlayer.Play();
                VideoStatusText.Text = "Oynatılıyor";
            }
            else
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                VideoStatusText.Text = "Varsayılan uygulamada açıldı";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kanıt Dosyası", MessageBoxButton.OK, MessageBoxImage.Error);
            VideoStatusText.Text = "Açılamadı";
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (VideoPlayer.Source is not null)
        {
            VideoPlayer.Play();
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => VideoPlayer.Pause();

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Stop();
        VideoStatusText.Text = string.IsNullOrWhiteSpace(_currentVideoPath) ? "Video seçilmedi" : "Durduruldu";
    }

    private void SeekTimestamp_Click(object sender, RoutedEventArgs e)
    {
        if (_detail?.BugTimestampSeconds is double seconds && VideoPlayer.Source is not null)
        {
            VideoPlayer.Position = TimeSpan.FromSeconds(seconds);
            VideoPlayer.Play();
        }
    }

    private async void RetestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detail is null || !_canAdminister)
        {
            return;
        }

        var window = new RetestRequestWindow(_api, _detail.ProjectId, _detail.Id, _detail.Key)
        {
            Owner = this
        };
        if (window.ShowDialog() == true)
        {
            Changed = true;
            await ReloadAsync();
        }
    }

    private static string EventText(string value) => value switch
    {
        "report_created" => "Rapor oluşturuldu",
        "status_changed" => "Durum değiştirildi",
        "retest_requested" => "Yeniden test istendi",
        "retest_submitted" => "Yeniden test sonucu geldi",
        "retest_failed" => "Yeniden test başarısız",
        "retest_passed" => "Yeniden test başarılı",
        _ => value.Replace('_', ' ')
    };

    private sealed record StatusOption(string Value, string Display);
}
