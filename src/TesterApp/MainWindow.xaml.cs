using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DmC.Qa.Shared;

namespace DmC.Qa.Tester;

public partial class MainWindow : Window
{
    private static readonly string StateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameQaClient");
    private static readonly string StateFile = Path.Combine(StateDirectory, "client.json");
    private static readonly byte[] CredentialEntropy = Encoding.UTF8.GetBytes("GameQaClient.DeviceCredential.v1");

    private readonly QaApiClient _api;
    private readonly PlatformApiClient _platformApi;
    private readonly CancellationTokenSource _notificationCts = new();
    private ClientState _state = new();
    private bool _hasAuthenticatedCredential;
    private bool _blockingMustRead;
    private ProjectSummary? _currentProject;
    private TaskSummary? _currentTask;
    private SharedTaskDetail? _sharedTask;
    private BuildSummary? _currentBuild;
    private CurrentBuildDetail? _currentBuildDetail;
    private List<RetestAssignment> _pendingRetests = [];
    private List<InboxNotificationItem> _notifications = [];
    private string? _lastPopupNotificationId;
    private Button? _buildDownloadButton;

    public MainWindow()
    {
        InitializeComponent();

        var apiBaseUrl = Environment.GetEnvironmentVariable("GAME_QA_API") ?? "http://localhost:7860/";
        if (!apiBaseUrl.EndsWith('/'))
        {
            apiBaseUrl += "/";
        }
        var baseUri = new Uri(apiBaseUrl);
        _api = new QaApiClient(new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromMinutes(30)
        });
        _platformApi = new PlatformApiClient(baseUri);

        ProjectsList.DisplayMemberPath = nameof(ProjectSummary.Name);
        ProjectsList.SelectionChanged += ProjectsList_SelectionChanged;
        AttachShellButtons();
        ReportBugButton.IsEnabled = false;
        RetestButton.IsEnabled = false;
        LoadLocalProfile();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void LoadLocalProfile()
    {
        Directory.CreateDirectory(StateDirectory);
        if (File.Exists(StateFile))
        {
            try
            {
                _state = JsonSerializer.Deserialize<ClientState>(File.ReadAllText(StateFile)) ?? new ClientState();
            }
            catch
            {
                _state = new ClientState();
            }
        }

        if (string.IsNullOrWhiteSpace(_state.InstallationId))
        {
            _state.InstallationId = Guid.NewGuid().ToString("N");
            SaveState();
        }
        DisplayNameInput.Text = _state.DisplayName;

        if (string.IsNullOrWhiteSpace(_state.DeviceId) || string.IsNullOrWhiteSpace(_state.ProtectedCredential))
        {
            EnrollmentOverlay.Visibility = Visibility.Visible;
            SetConnectionState("● Cihaz eşleştirmesi gerekli", "#F9A825");
            return;
        }

        var credential = UnprotectCredential(_state.ProtectedCredential);
        if (string.IsNullOrWhiteSpace(credential))
        {
            _state.DeviceId = null;
            _state.ProtectedCredential = null;
            SaveState();
            EnrollmentOverlay.Visibility = Visibility.Visible;
            EnrollmentError.Text = "Bu bilgisayardaki güvenli oturum bilgisi okunamadı. Lütfen adınızı yeniden girin.";
            SetConnectionState("● Yeniden eşleştirme gerekli", "#F9A825");
            return;
        }

        ApplyCredential(_state.DeviceId, credential);
        _hasAuthenticatedCredential = true;
        TesterNameText.Text = _state.DisplayName;
        EnrollmentOverlay.Visibility = Visibility.Collapsed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasAuthenticatedCredential)
        {
            await ValidateSessionAndLoadAsync();
        }
    }

    private async Task ValidateSessionAndLoadAsync()
    {
        SetConnectionState("● Bağlanıyor", "#F9A825");
        try
        {
            var me = await _api.GetCurrentUserAsync();
            _state.DisplayName = me.DisplayName;
            SaveState();
            TesterNameText.Text = me.DisplayName;
            EnrollmentOverlay.Visibility = Visibility.Collapsed;
            await LoadDashboardAsync();
            await LoadNotificationsAsync(showPopup: false);
            _ = WatchNotificationsAsync(_notificationCts.Token);
            SetConnectionState("● Bağlı", "#43A047");
        }
        catch (QaApiException ex)
        {
            _api.ClearAuthentication();
            _hasAuthenticatedCredential = false;
            ReportBugButton.IsEnabled = false;
            RetestButton.IsEnabled = false;
            EnrollmentOverlay.Visibility = Visibility.Visible;
            EnrollmentError.Text = ex.Message;
            SetConnectionState("● Oturum doğrulanamadı", "#EF5350");
        }
        catch (HttpRequestException)
        {
            SetConnectionState("● Bağlantı yok", "#EF5350");
        }
        catch (TaskCanceledException)
        {
            SetConnectionState("● Sunucu yanıt vermiyor", "#EF5350");
        }
    }

    private async Task LoadDashboardAsync()
    {
        var selectedId = _currentProject?.Id;
        var projects = await _api.GetProjectsAsync();
        _pendingRetests = (await _api.GetMyRetestsAsync()).ToList();
        RetestCount.Text = _pendingRetests.Count.ToString(TurkishUi.Culture);

        ProjectsList.ItemsSource = projects;
        if (projects.Count == 0)
        {
            _currentProject = null;
            _currentTask = null;
            _currentBuild = null;
            _currentBuildDetail = null;
            ProjectTitle.Text = "Henüz bir projeye atanmadınız";
            ClearProjectPanel();
            UpdateActionAvailability();
            return;
        }

        var selected = projects.FirstOrDefault(item => item.Id == selectedId) ?? projects[0];
        ProjectsList.SelectedItem = selected;
        await LoadProjectAsync(selected);
    }

    private async void ProjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectsList.SelectedItem is ProjectSummary project && _currentProject?.Id != project.Id)
        {
            try
            {
                await LoadProjectAsync(project);
            }
            catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
            {
                SetConnectionState($"● Proje yüklenemedi: {ex.Message}", "#EF5350");
            }
        }
    }

    private async Task LoadProjectAsync(ProjectSummary project)
    {
        _currentProject = project;
        ProjectTitle.Text = project.Name;
        var tasks = await _api.GetMyTasksAsync();
        _currentTask = tasks
            .Where(item => item.ProjectId == project.Id)
            .OrderBy(item => item.DeadlineAt ?? DateTimeOffset.MaxValue)
            .FirstOrDefault();

        if (_currentTask is null)
        {
            _sharedTask = null;
            TaskTitle.Text = "Aktif ortak görev yok";
            TaskDeadline.Text = "Yeni görev atandığında burada görünecek.";
        }
        else
        {
            _sharedTask = await _platformApi.GetSharedTaskDetailAsync(_currentTask.Id);
            TaskTitle.Text = _currentTask.Title;
            TaskDeadline.Text = TurkishUi.Deadline(_currentTask.DeadlineAt);
        }

        _currentBuild = await _api.GetCurrentBuildAsync(project.Id);
        _currentBuildDetail = await _platformApi.GetCurrentBuildDetailAsync(project.Id);
        if (_currentBuildDetail is null)
        {
            BuildVersion.Text = "—";
            BuildTitle.Text = "Yayınlanmış test sürümü yok";
            BuildPublished.Text = "Henüz yayınlanmadı";
        }
        else
        {
            BuildVersion.Text = _currentBuildDetail.Version;
            BuildTitle.Text = _currentBuildDetail.Title;
            BuildPublished.Text = _currentBuildDetail.PublishedAt is null
                ? TurkishUi.FileSize(_currentBuildDetail.SizeBytes)
                : $"Yayın: {TurkishUi.Date(_currentBuildDetail.PublishedAt, false)} • {TurkishUi.FileSize(_currentBuildDetail.SizeBytes)}";
        }

        var progress = await _api.GetProjectProgressAsync(project.Id);
        ResolutionPercent.Text = progress is null ? "—" : $"%{progress.ResolutionPercentage:0.#} çözüldü";
        var reports = await _platformApi.GetMyBugsAsync(project.Id);
        MyReportsGrid.ItemsSource = reports.Select(item => new
        {
            item.Key,
            item.Title,
            item.BugType,
            Status = TurkishUi.Status(item.Status)
        }).ToList();
        UpdateActionAvailability();
    }

    private void ClearProjectPanel()
    {
        TaskTitle.Text = "Aktif ortak görev yok";
        TaskDeadline.Text = "—";
        BuildVersion.Text = "—";
        BuildTitle.Text = "Yayınlanmış test sürümü yok";
        BuildPublished.Text = "—";
        ResolutionPercent.Text = "—";
        MyReportsGrid.ItemsSource = null;
    }

    private async Task LoadNotificationsAsync(bool showPopup)
    {
        _notifications = (await _platformApi.GetMyNotificationsAsync()).ToList();
        var mustRead = _notifications.FirstOrDefault(item => item.RequiresAcknowledgement && item.AcknowledgedAt is null);
        _blockingMustRead = mustRead is not null;
        MustReadBanner.Visibility = mustRead is null ? Visibility.Collapsed : Visibility.Visible;
        MustReadBody.Text = mustRead?.Body ?? string.Empty;
        UpdateActionAvailability();

        if (showPopup)
        {
            var newest = _notifications.FirstOrDefault();
            if (newest is not null && newest.Id != _lastPopupNotificationId)
            {
                _lastPopupNotificationId = newest.Id;
                if (!newest.RequiresAcknowledgement)
                {
                    MessageBox.Show(newest.Body, newest.Title, MessageBoxButton.OK,
                        newest.Severity is "critical" or "warning" ? MessageBoxImage.Warning : MessageBoxImage.Information);
                }
            }
        }
    }

    private void UpdateActionAvailability()
    {
        var projectAvailable = _currentProject is not null;
        ReportBugButton.IsEnabled = projectAvailable && !_blockingMustRead;
        RetestButton.IsEnabled = _pendingRetests.Count > 0 && !_blockingMustRead;
        if (_buildDownloadButton is not null)
        {
            _buildDownloadButton.IsEnabled = _currentBuildDetail is not null && !_blockingMustRead;
        }
    }

    private async Task WatchNotificationsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_platformApi.DeviceAuthorization))
        {
            return;
        }
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                socket.Options.SetRequestHeader("Authorization", _platformApi.DeviceAuthorization);
                await socket.ConnectAsync(_platformApi.NotificationsWebSocketUri(), cancellationToken);
                var buffer = new byte[4096];
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (message.Contains("notifications_refresh", StringComparison.Ordinal))
                    {
                        await Dispatcher.InvokeAsync(async () => await LoadNotificationsAsync(showPopup: true));
                    }
                }
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch
                {
                    break;
                }
            }
        }
    }

    private async void ReportBugButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject is null || _blockingMustRead)
        {
            return;
        }
        var dialog = new BugReportWindow(_api, _currentProject, _currentTask, _currentBuild) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            await LoadProjectAsync(_currentProject);
        }
    }

    private async void RetestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingRetests.Count == 0 || _blockingMustRead)
        {
            return;
        }
        var projectRetests = _currentProject is null
            ? _pendingRetests
            : _pendingRetests.Where(item => true).ToList();
        var dialog = new RetestWindow(_api, projectRetests) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            await LoadDashboardAsync();
        }
    }

    private async void EnrollButton_Click(object sender, RoutedEventArgs e)
    {
        var displayName = DisplayNameInput.Text.Trim();
        if (displayName.Length < 2)
        {
            EnrollmentError.Text = "Lütfen ekipte kullanılan adınızı yazın.";
            return;
        }
        EnrollButton.IsEnabled = false;
        EnrollmentError.Text = string.Empty;
        SetConnectionState("● Cihaz eşleştiriliyor", "#F9A825");
        _state.DisplayName = displayName;
        if (string.IsNullOrWhiteSpace(_state.InstallationId))
        {
            _state.InstallationId = Guid.NewGuid().ToString("N");
        }
        SaveState();
        try
        {
            var enrollment = await _api.EnrollDeviceAsync(displayName, _state.InstallationId, Environment.MachineName);
            _state.DisplayName = enrollment.DisplayName;
            _state.DeviceId = enrollment.DeviceId;
            _state.ProtectedCredential = ProtectCredential(enrollment.Credential);
            SaveState();
            ApplyCredential(enrollment.DeviceId, enrollment.Credential);
            _hasAuthenticatedCredential = true;
            TesterNameText.Text = enrollment.DisplayName;
            EnrollmentOverlay.Visibility = Visibility.Collapsed;
            await LoadDashboardAsync();
            await LoadNotificationsAsync(showPopup: false);
            _ = WatchNotificationsAsync(_notificationCts.Token);
            SetConnectionState("● Bağlı", "#43A047");
        }
        catch (QaApiException ex)
        {
            EnrollmentError.Text = ex.Message;
            SetConnectionState("● Eşleştirme başarısız", "#EF5350");
        }
        catch (HttpRequestException)
        {
            EnrollmentError.Text = "Sunucuya ulaşılamadı. QA sunucusunun çalıştığını kontrol edin.";
            SetConnectionState("● Bağlantı yok", "#EF5350");
        }
        finally
        {
            EnrollButton.IsEnabled = true;
        }
    }

    private void AttachShellButtons()
    {
        _buildDownloadButton = FindButtons("Test Sürümünü İndir").FirstOrDefault();
        if (_buildDownloadButton is not null)
        {
            _buildDownloadButton.Click += BuildDownloadButton_Click;
        }
        foreach (var button in FindButtons("Kurulum Talimatları")) button.Click += InstallInstructionsButton_Click;
        foreach (var button in FindButtons("Bildirimler")) button.Click += NotificationsButton_Click;
        foreach (var button in FindButtons("Görevi Aç")) button.Click += SharedTaskButton_Click;
        foreach (var button in FindButtons("Okudum")) button.Click += MustReadAcknowledge_Click;
    }

    private async void BuildDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject is null || _currentBuildDetail is null || _blockingMustRead)
        {
            return;
        }
        var fileName = string.IsNullOrWhiteSpace(_currentBuildDetail.OriginalFilename)
            ? $"test-build-{_currentBuildDetail.Version}.bin"
            : _currentBuildDetail.OriginalFilename;
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "GameQA", _currentProject.Name);
        var destination = Path.Combine(downloads, fileName);
        try
        {
            _buildDownloadButton!.IsEnabled = false;
            var progress = new Progress<double>(value => SetConnectionState($"● Test sürümü indiriliyor %{value:0}", "#42A5F5"));
            await _platformApi.DownloadBuildAsync(_currentProject.Id, _currentBuildDetail.Id, destination, _currentBuildDetail.Sha256, progress);
            SetConnectionState("● Test sürümü doğrulandı", "#43A047");
            var result = MessageBox.Show(
                $"Test sürümü indirildi ve SHA-256 doğrulaması tamamlandı.\n\nKonum:\n{destination}\n\nKurulum Talimatları:\n{_currentBuildDetail.InstallationInstructions}\n\nKurulumu tamamladınız mı?",
                $"{_currentBuildDetail.Version} İndirildi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                await _platformApi.RecordBuildEventAsync(_currentProject.Id, _currentBuildDetail.Id, "installed");
            }
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or IOException)
        {
            MessageBox.Show(ex.Message, "Test Sürümü İndirilemedi", MessageBoxButton.OK, MessageBoxImage.Error);
            SetConnectionState("● İndirme başarısız", "#EF5350");
        }
        finally
        {
            UpdateActionAvailability();
        }
    }

    private void InstallInstructionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBuildDetail is null)
        {
            MessageBox.Show("Henüz yayınlanmış güncel test sürümü yok.", "Kurulum Talimatları", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        MessageBox.Show(
            $"{_currentBuildDetail.Title}\n\n{_currentBuildDetail.InstallationInstructions}\n\nDeğişiklik Notları:\n{_currentBuildDetail.Changelog}",
            $"{_currentBuildDetail.Version} • Kurulum Talimatları",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void NotificationsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new NotificationWindow(_platformApi) { Owner = this };
        window.ShowDialog();
        if (window.AcknowledgementChanged)
        {
            await LoadNotificationsAsync(showPopup: false);
        }
    }

    private void SharedTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sharedTask is null)
        {
            MessageBox.Show("Şu anda açık bir ortak göreviniz yok.", "Ortak Görev", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        MessageBox.Show(
            $"{_sharedTask.Title}\n\n{_sharedTask.Description}\n\nEkip:\n{string.Join(" • ", _sharedTask.Assignees)}\n\n{TurkishUi.Deadline(_sharedTask.DeadlineAt)}\nBilinen rapor: {_sharedTask.KnownReportCount}",
            "Ortak Görev Paneli",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void MustReadAcknowledge_Click(object sender, RoutedEventArgs e)
    {
        var notification = _notifications.FirstOrDefault(item => item.RequiresAcknowledgement && item.AcknowledgedAt is null);
        if (notification is null)
        {
            return;
        }
        try
        {
            await _platformApi.AcknowledgeNotificationAsync(notification.Id);
            await LoadNotificationsAsync(showPopup: false);
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Duyuru Onaylanamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private IEnumerable<Button> FindButtons(string content)
        => FindVisualChildren<Button>(this).Where(button => Equals(button.Content, content));

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) yield return typed;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    private void ApplyCredential(string deviceId, string credential)
    {
        _api.SetDeviceCredential(deviceId, credential);
        _platformApi.SetDeviceCredential(deviceId, credential);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _notificationCts.Cancel();
        _notificationCts.Dispose();
    }

    private void SetConnectionState(string text, string color)
    {
        ConnectionText.Text = text;
        ConnectionText.Foreground = (Brush)new BrushConverter().ConvertFromString(color)!;
    }

    private void SaveState()
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(StateFile, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ProtectCredential(string credential)
    {
        var clearBytes = Encoding.UTF8.GetBytes(credential);
        var encrypted = ProtectedData.Protect(clearBytes, CredentialEntropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string? UnprotectCredential(string protectedCredential)
    {
        try
        {
            var encrypted = Convert.FromBase64String(protectedCredential);
            var clearBytes = ProtectedData.Unprotect(encrypted, CredentialEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clearBytes);
        }
        catch
        {
            return null;
        }
    }

    private sealed class ClientState
    {
        public string DisplayName { get; set; } = string.Empty;
        public string InstallationId { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public string? ProtectedCredential { get; set; }
    }
}
