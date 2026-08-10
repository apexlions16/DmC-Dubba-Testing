using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
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
    private ClientState _state = new();
    private bool _hasAuthenticatedCredential;
    private ProjectSummary? _currentProject;
    private TaskSummary? _currentTask;
    private BuildSummary? _currentBuild;
    private List<RetestAssignment> _pendingRetests = [];

    public MainWindow()
    {
        InitializeComponent();

        var apiBaseUrl = Environment.GetEnvironmentVariable("GAME_QA_API") ?? "http://localhost:7860/";
        if (!apiBaseUrl.EndsWith('/'))
        {
            apiBaseUrl += "/";
        }

        _api = new QaApiClient(new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        });

        ReportBugButton.IsEnabled = false;
        RetestButton.IsEnabled = false;
        LoadLocalProfile();
        Loaded += MainWindow_Loaded;
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

        _api.SetDeviceCredential(_state.DeviceId, credential);
        _hasAuthenticatedCredential = true;
        TesterNameText.Text = _state.DisplayName;
        EnrollmentOverlay.Visibility = Visibility.Collapsed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_hasAuthenticatedCredential)
        {
            return;
        }

        await ValidateSessionAndLoadAsync();
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
        var projects = await _api.GetProjectsAsync();
        var tasks = await _api.GetMyTasksAsync();
        _pendingRetests = (await _api.GetMyRetestsAsync()).ToList();
        RetestCount.Text = _pendingRetests.Count.ToString(TurkishUi.Culture);
        RetestButton.IsEnabled = _pendingRetests.Count > 0;

        ProjectsList.Items.Clear();
        foreach (var projectItem in projects)
        {
            ProjectsList.Items.Add(projectItem.Name);
        }

        if (projects.Count == 0)
        {
            _currentProject = null;
            _currentTask = null;
            _currentBuild = null;
            ReportBugButton.IsEnabled = false;
            ProjectTitle.Text = "Henüz bir projeye atanmadınız";
            TaskTitle.Text = "Aktif ortak görev yok";
            TaskDeadline.Text = "—";
            BuildVersion.Text = "—";
            BuildTitle.Text = "Yayınlanmış test sürümü yok";
            BuildPublished.Text = "—";
            ResolutionPercent.Text = "—";
            return;
        }

        var project = projects[0];
        _currentProject = project;
        ProjectTitle.Text = project.Name;
        ProjectsList.SelectedIndex = 0;
        ReportBugButton.IsEnabled = true;

        var task = tasks.FirstOrDefault(item => item.ProjectId == project.Id);
        _currentTask = task;
        if (task is null)
        {
            TaskTitle.Text = "Aktif ortak görev yok";
            TaskDeadline.Text = "Yeni görev atandığında burada görünecek.";
        }
        else
        {
            TaskTitle.Text = task.Title;
            TaskDeadline.Text = TurkishUi.Deadline(task.DeadlineAt);
        }

        var build = await _api.GetCurrentBuildAsync(project.Id);
        _currentBuild = build;
        if (build is null)
        {
            BuildVersion.Text = "—";
            BuildTitle.Text = "Yayınlanmış test sürümü yok";
            BuildPublished.Text = "Henüz yayınlanmadı";
        }
        else
        {
            BuildVersion.Text = build.Version;
            BuildTitle.Text = build.Title;
            BuildPublished.Text = build.PublishedAt is null
                ? "Yayın tarihi belirtilmedi"
                : $"Yayın: {TurkishUi.Date(build.PublishedAt, includeTime: false)}";
        }

        var progress = await _api.GetProjectProgressAsync(project.Id);
        if (progress is not null)
        {
            ResolutionPercent.Text = $"%{progress.ResolutionPercentage:0.#} çözüldü";
        }
    }

    private async void ReportBugButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject is null)
        {
            MessageBox.Show(
                "Hata raporu gönderebilmek için önce bir projeye atanmış olmanız gerekiyor.",
                "Proje Bulunamadı",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new BugReportWindow(_api, _currentProject, _currentTask, _currentBuild)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await LoadDashboardAsync();
            }
            catch
            {
                SetConnectionState("● Rapor gönderildi, özet yenilenemedi", "#F9A825");
            }
        }
    }

    private async void RetestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingRetests.Count == 0)
        {
            MessageBox.Show(
                "Şu anda sizden beklenen bir yeniden test bulunmuyor.",
                "Yeniden Test",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new RetestWindow(_api, _pendingRetests)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await LoadDashboardAsync();
            }
            catch
            {
                SetConnectionState("● Yeniden test kaydedildi, özet yenilenemedi", "#F9A825");
            }
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
            var enrollment = await _api.EnrollDeviceAsync(
                displayName,
                _state.InstallationId,
                Environment.MachineName);

            _state.DisplayName = enrollment.DisplayName;
            _state.DeviceId = enrollment.DeviceId;
            _state.ProtectedCredential = ProtectCredential(enrollment.Credential);
            SaveState();

            _api.SetDeviceCredential(enrollment.DeviceId, enrollment.Credential);
            _hasAuthenticatedCredential = true;
            TesterNameText.Text = enrollment.DisplayName;
            EnrollmentOverlay.Visibility = Visibility.Collapsed;

            await LoadDashboardAsync();
            SetConnectionState("● Bağlı", "#43A047");
        }
        catch (QaApiException ex)
        {
            EnrollmentError.Text = ex.Message;
            SetConnectionState("● Eşleştirme başarısız", "#EF5350");
        }
        catch (HttpRequestException)
        {
            EnrollmentError.Text = "Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.";
            SetConnectionState("● Bağlantı yok", "#EF5350");
        }
        catch (TaskCanceledException)
        {
            EnrollmentError.Text = "Sunucu zamanında yanıt vermedi. Lütfen tekrar deneyin.";
            SetConnectionState("● Sunucu yanıt vermiyor", "#EF5350");
        }
        finally
        {
            EnrollButton.IsEnabled = true;
        }
    }

    private void SetConnectionState(string text, string color)
    {
        ConnectionText.Text = text;
        ConnectionText.Foreground = (Brush)new BrushConverter().ConvertFromString(color)!;
    }

    private void SaveState()
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(
            StateFile,
            JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
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
