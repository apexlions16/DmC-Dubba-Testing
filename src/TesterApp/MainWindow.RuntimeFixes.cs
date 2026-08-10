using System.ComponentModel;
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

    private void InitializeRuntimeFixes()
    {
        if (_runtimeFixesInitialized)
        {
            return;
        }
        _runtimeFixesInitialized = true;

        // Eski içerik-aranarak eklenen handler'lardan XAML'deki doğrudan handler'larla
        // çakışanları kaldır. Böylece tek tıklama iki işlem üretmez.
        foreach (var button in FindButtons("Görevi Aç"))
        {
            button.Click -= SharedTaskButton_Click;
        }
        foreach (var button in FindButtons("Okudum"))
        {
            button.Click -= MustReadAcknowledge_Click;
        }

        _buildDownloadButton = BuildDownloadButton;
        UpdateActionAvailability();

        // Eski SelectionChanged akışını güvenli, tek-yüklemeli sürümle değiştir.
        ProjectsList.SelectionChanged -= ProjectsList_SelectionChanged;
        ProjectsList.SelectionChanged += ProjectsList_SelectionChangedSafe;

        var descriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        descriptor?.AddValueChanged(ResolutionPercent, (_, _) => _ = RefreshProgressVisualAsync());
    }

    private async void ProjectsList_SelectionChangedSafe(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectsList.SelectedItem is not ProjectSummary selected)
        {
            return;
        }

        // ItemsSource/SelectedItem, LoadDashboardAsync içinde programatik olarak değiştiğinde
        // aynı projeyi ikinci kez paralel yüklememek için ana akışa bir dispatcher turu ver.
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

            if (total == 0)
            {
                SetProgressColumns(0, 0, 0, 1);
            }
            else
            {
                SetProgressColumns(resolved, inProgress, retest, unstarted);
            }
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            // Ana dashboard zaten bağlantı durumunu gösteriyor; yalnızca görsel segmentleri nötr bırak.
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
