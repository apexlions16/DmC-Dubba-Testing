using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DmC.Qa.Shared;

namespace DmC.Qa.Tester;

public partial class MainWindow
{
    private bool _runtimeFixesInitialized;
    private bool _progressRefreshRunning;
    private DispatcherTimer? _liveNotificationTimer;

    private void InitializeRuntimeFixes()
    {
        if (_runtimeFixesInitialized)
        {
            return;
        }
        _runtimeFixesInitialized = true;

        // XAML'deki doğrudan handler'larla eski dinamik handler'ların çift çalışmasını önle.
        foreach (var button in FindButtons("Görevi Aç"))
        {
            button.Click -= SharedTaskButton_Click;
        }
        foreach (var button in FindButtons("Okudum"))
        {
            button.Click -= MustReadAcknowledge_Click;
        }

        // Canlı Supabase paketinde build byte'ları qa-files üzerinden doğrudan HF'ye gider.
        _buildDownloadButton = BuildDownloadButton;
        BuildDownloadButton.Click -= BuildDownloadButton_Click;
        BuildDownloadButton.Click += BuildDownloadLiveButton_Click;
        UpdateActionAvailability();

        // ItemsSource/SelectedItem değişiminde aynı projeyi paralel iki kere yükleme.
        ProjectsList.SelectionChanged -= ProjectsList_SelectionChanged;
        ProjectsList.SelectionChanged += ProjectsList_SelectionChangedSafe;

        var descriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        descriptor?.AddValueChanged(ResolutionPercent, (_, _) => _ = RefreshProgressVisualAsync());

        ConfigureLiveNotificationPolling();
    }

    private void ConfigureLiveNotificationPolling()
    {
        if (!_platformApi.BaseAddress.AbsolutePath.Contains(
                "/functions/v1/qa-api/",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Supabase Edge Function sürümünde kalıcı WebSocket endpoint'i yok. Eski watcher'ın
        // reconnect döngüsünü durdurup kısa aralıklı polling kullanıyoruz.
        _notificationCts.Cancel();
        _liveNotificationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _liveNotificationTimer.Tick += async (_, _) =>
        {
            if (!_hasAuthenticatedCredential)
            {
                return;
            }
            try
            {
                await LoadNotificationsAsync(showPopup: true);
            }
            catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
            {
                // Bildirim polling hatası ana tester akışını kesmez; sonraki tur tekrar dener.
            }
        };
        _liveNotificationTimer.Start();
        Closed += (_, _) => _liveNotificationTimer?.Stop();
    }

    private async void BuildDownloadLiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject is null || _currentBuildDetail is null || _blockingMustRead)
        {
            return;
        }

        var fileName = string.IsNullOrWhiteSpace(_currentBuildDetail.OriginalFilename)
            ? $"test-build-{_currentBuildDetail.Version}.bin"
            : _currentBuildDetail.OriginalFilename;
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "GameQA",
            _currentProject.Name);
        var destination = Path.Combine(downloads, fileName);

        try
        {
            BuildDownloadButton.IsEnabled = false;
            var progress = new Progress<double>(value =>
                SetConnectionState($"● Test sürümü Hugging Face'den indiriliyor %{value:0}", "#42A5F5"));
            await _platformApi.DownloadBuildForCurrentBackendAsync(
                _currentProject.Id,
                _currentBuildDetail.Id,
                destination,
                _currentBuildDetail.Sha256,
                progress);
            SetConnectionState("● Test sürümü indirildi ve SHA-256 doğrulandı", "#43A047");

            var result = MessageBox.Show(
                $"Test sürümü indirildi ve SHA-256 doğrulaması tamamlandı.\n\nKonum:\n{destination}\n\nKurulum Talimatları:\n{_currentBuildDetail.InstallationInstructions}\n\nKurulumu tamamladınız mı?",
                $"{_currentBuildDetail.Version} İndirildi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                await _platformApi.RecordBuildEventAsync(
                    _currentProject.Id,
                    _currentBuildDetail.Id,
                    "installed");
            }
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or IOException or TaskCanceledException)
        {
            MessageBox.Show(ex.Message, "Test Sürümü İndirilemedi", MessageBoxButton.OK, MessageBoxImage.Error);
            SetConnectionState("● İndirme başarısız", "#EF5350");
        }
        finally
        {
            UpdateActionAvailability();
        }
    }

    private async void ProjectsList_SelectionChangedSafe(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectsList.SelectedItem is not ProjectSummary selected)
        {
            return;
        }

        await Dispatcher.Yield(DispatcherPriority.Background);
        try
        {
            if (_currentProject?.Id != selected.Id)
            {
                await LoadProjectAsync(selected);
            }
            await RefreshProgressVisualAsync();
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            SetConnectionState($"● Proje yüklenemedi: {ex.Message}", "#EF5350");
        }
    }

    private async Task RefreshProgressVisualAsync()
    {
        if (_progressRefreshRunning || _currentProject is null)
        {
            return;
        }

        _progressRefreshRunning = true;
        try
        {
            var progress = await _api.GetProjectProgressAsync(_currentProject.Id);
            if (progress is null)
            {
                SetProgressColumns(0, 0, 0, 1);
                return;
            }

            var resolved = Math.Max(0, progress.Resolved);
            var inProgress = Math.Max(0, progress.InProgress + progress.OnHold);
            var retest = Math.Max(0, progress.RetestRequired);
            var unstarted = Math.Max(0, progress.NewOrUnstarted);
            var total = resolved + inProgress + retest + unstarted;
            SetProgressColumns(
                total == 0 ? 0 : resolved,
                total == 0 ? 0 : inProgress,
                total == 0 ? 0 : retest,
                total == 0 ? 1 : unstarted);
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            SetProgressColumns(0, 0, 0, 1);
        }
        finally
        {
            _progressRefreshRunning = false;
        }
    }

    private void SetProgressColumns(double resolved, double inProgress, double retest, double unstarted)
    {
        TesterResolvedColumn.Width = new GridLength(resolved, GridUnitType.Star);
        TesterInProgressColumn.Width = new GridLength(inProgress, GridUnitType.Star);
        TesterRetestColumn.Width = new GridLength(retest, GridUnitType.Star);
        TesterNewColumn.Width = new GridLength(unstarted, GridUnitType.Star);
    }
}
