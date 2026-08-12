using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        DeleteEvidenceButton.Visibility = _canAdminister ? Visibility.Visible : Visibility.Collapsed;
        DeleteEvidenceButton.IsEnabled = _canAdminister && EvidenceList.SelectedItem is EvidenceItem;
        InitializePermanentBugDeleteAction();
    }

    private async void EvidenceList_LiveMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EvidenceList.SelectedItem is not EvidenceItem evidence)
        {
            return;
        }

        var path = EvidenceCachePath(evidence);

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
        catch (IOException)
        {
            MessageBox.Show(
                "Kanıt dosyası hazırlanırken Windows dosyayı kullanıyor görünüyor. Lütfen birkaç saniye sonra yeniden deneyin. Sorun devam ederse Yönetim Merkezi'ni kapatıp tekrar açın.",
                "Kanıt Dosyası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            VideoStatusText.Text = "Dosya kullanımda";
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(ex.Message, "Kanıt Dosyası", MessageBoxButton.OK, MessageBoxImage.Error);
            VideoStatusText.Text = "Açılamadı";
        }
    }

    private void EvidenceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DeleteEvidenceButton.IsEnabled = _canAdminister && EvidenceList.SelectedItem is EvidenceItem;
    }

    private async void DeleteEvidenceButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_canAdminister || EvidenceList.SelectedItem is not EvidenceItem evidence)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            $"'{evidence.Filename}' kanıt dosyasını kalıcı olarak silmek istiyor musunuz?\n\n" +
            "Dosya Hugging Face deposundan ve Supabase veritabanından tamamen kaldırılacaktır. Bu işlem geri alınamaz.",
            "Kanıtı Kalıcı Sil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        DeleteEvidenceButton.IsEnabled = false;
        var cachePath = EvidenceCachePath(evidence);
        try
        {
            ReleasePlayerIfUsing(cachePath);
            VideoStatusText.Text = "Kanıt kalıcı olarak siliniyor...";
            await _api.DeleteEvidenceForCurrentBackendAsync(evidence.Id);
            TryDeleteLocalEvidenceCache(cachePath);
            Changed = true;
            await ReloadAsync();
            EvidenceList.SelectedItem = null;
            VideoStatusText.Text = "Kanıt kalıcı olarak silindi";
            MessageBox.Show(
                "Kanıt dosyası Hugging Face deposundan ve veritabanından tamamen kaldırıldı.",
                "Kanıt Silindi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or IOException or TaskCanceledException)
        {
            MessageBox.Show(
                $"Kanıt dosyası silinemedi. Kayıt korunmuştur.\n\n{ex.Message}",
                "Kanıt Silinemedi",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            VideoStatusText.Text = "Silme başarısız";
        }
        finally
        {
            DeleteEvidenceButton.IsEnabled = _canAdminister && EvidenceList.SelectedItem is EvidenceItem;
        }
    }

    private string EvidenceCachePath(EvidenceItem evidence)
    {
        var directory = Path.Combine(Path.GetTempPath(), "GameQaPlatform", "evidence", _bugId);
        var safeName = string.Concat(
            evidence.Filename.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return Path.Combine(directory, $"{evidence.Id}-{safeName}");
    }

    private void ReleasePlayerIfUsing(string cachePath)
    {
        if (string.IsNullOrWhiteSpace(_currentVideoPath) ||
            !string.Equals(_currentVideoPath, cachePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _playerTimer.Stop();
        VideoPlayer.Stop();
        VideoPlayer.Close();
        VideoPlayer.Source = null;
        _currentVideoPath = null;
        _mediaDuration = TimeSpan.Zero;
        _isVideoPlaying = false;
        _isUpdatingSeek = true;
        SeekSlider.Maximum = 1;
        SeekSlider.Value = 0;
        _isUpdatingSeek = false;
        CurrentTimeText.Text = "00:00";
        DurationText.Text = "00:00";
        PlayPauseButton.Content = "▶ Oynat";
    }

    private static void TryDeleteLocalEvidenceCache(string cachePath)
    {
        try
        {
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
        catch (IOException)
        {
            // Sunucudaki asıl kanıt silinmiştir. Windows temp cache'i başka bir uygulama
            // tarafından tutuluyorsa uygulama kapanınca geçici klasör tarafından temizlenebilir.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
