using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class BugDetailWindow
{
    private bool _liveFileHandlerInitialized;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_liveFileHandlerInitialized)
        {
            return;
        }
        _liveFileHandlerInitialized = true;
        EvidenceList.MouseDoubleClick -= EvidenceList_MouseDoubleClick;
        EvidenceList.MouseDoubleClick += EvidenceList_LiveMouseDoubleClick;
    }

    private async void EvidenceList_LiveMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EvidenceList.SelectedItem is not EvidenceItem evidence)
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "GameQaPlatform", "evidence", _bugId);
        var safeName = string.Concat(
            evidence.Filename.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var path = Path.Combine(directory, $"{evidence.Id}-{safeName}");

        try
        {
            VideoStatusText.Text = "Hugging Face'den indiriliyor...";
            if (!File.Exists(path))
            {
                await _api.DownloadEvidenceForCurrentBackendAsync(
                    evidence.Id,
                    path,
                    evidence.Sha256);
            }

            _currentVideoPath = path;
            if (evidence.MediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) &&
                Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
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
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or IOException or TaskCanceledException)
        {
            MessageBox.Show(ex.Message, "Kanıt Dosyası", MessageBoxButton.OK, MessageBoxImage.Error);
            VideoStatusText.Text = "Açılamadı";
        }
    }
}
