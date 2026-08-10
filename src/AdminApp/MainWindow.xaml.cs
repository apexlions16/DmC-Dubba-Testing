using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class MainWindow : Window
{
    private readonly QaApiClient _api;
    private readonly PlatformApiClient _platformApi;
    private readonly CurrentUserSummary _signedInUser;
    private List<ProjectSummary> _projects = [];
    private ProjectSummary? _currentProject;

    public MainWindow(QaApiClient api, PlatformApiClient platformApi, CurrentUserSummary signedInUser)
    {
        InitializeComponent();
        _api = api;
        _platformApi = platformApi;
        _signedInUser = signedInUser;

        SignedInUserText.Text = $"{signedInUser.DisplayName} • {TurkishUi.Role(signedInUser.Role)}";
        TeamManagementButton.Visibility = IsAdminRole(signedInUser.Role)
            ? Visibility.Visible
            : Visibility.Collapsed;

        BugReportsGrid.MouseDoubleClick += BugReportsGrid_MouseDoubleClick;
        BuildsGrid.MouseDoubleClick += (_, _) => OpenBuildCenter();
        AttachShellButtons();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadProjectsAsync();
    }

    private async Task LoadProjectsAsync(string? selectProjectId = null)
    {
        try
        {
            _projects = (await _api.GetProjectsAsync()).ToList();
            ProjectPicker.ItemsSource = _projects;
            ProjectPicker.DisplayMemberPath = nameof(ProjectSummary.Name);
            ProjectPicker.SelectionChanged -= ProjectPicker_SelectionChanged;
            ProjectPicker.SelectionChanged += ProjectPicker_SelectionChanged;

            if (_projects.Count > 0)
            {
                ProjectPicker.SelectedItem = _projects.FirstOrDefault(item => item.Id == selectProjectId) ?? _projects[0];
            }
            else
            {
                _currentProject = null;
                ProjectTitle.Text = "Henüz erişebildiğiniz bir proje yok";
                ProjectStatus.Text = "Yeni proje oluşturulduğunda burada görünecek.";
                ClearSummary();
                ClearOperationalGrids();
            }
        }
        catch (QaApiException ex)
        {
            ProjectTitle.Text = "Proje verileri alınamadı";
            ProjectStatus.Text = ex.Message;
            ClearSummary();
        }
        catch (HttpRequestException)
        {
            ProjectTitle.Text = "Sunucuya ulaşılamadı";
            ProjectStatus.Text = "İnternet bağlantınızı veya QA sunucusunu kontrol edin.";
            ClearSummary();
        }
    }

    private async void ProjectPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectPicker.SelectedItem is not ProjectSummary project)
        {
            return;
        }
        _currentProject = project;
        ProjectTitle.Text = project.Name;
        ProjectStatus.Text = $"{project.SectionLabel} • {TurkishUi.Status(project.Status)} • Veriler yenileniyor...";
        await LoadProjectDashboardAsync(project);
    }

    private async Task LoadProjectDashboardAsync(ProjectSummary project)
    {
        try
        {
            var summaryTask = _api.GetProjectProgressAsync(project.Id);
            var bugsTask = _platformApi.GetAdminBugsAsync(project.Id);
            var tasksTask = _platformApi.GetTasksAsync(project.Id);
            var buildsTask = _platformApi.GetBuildsAsync(project.Id);
            var membersTask = _platformApi.GetProjectMembersAsync(project.Id);
            var retestsTask = _platformApi.GetAdminRetestsAsync(project.Id);
            var bugTypesTask = _platformApi.GetAnalyticsAsync(project.Id, "bug_type");
            var reportersTask = _platformApi.GetAnalyticsAsync(project.Id, "reporter");

            await Task.WhenAll(summaryTask, bugsTask, tasksTask, buildsTask, membersTask, retestsTask, bugTypesTask, reportersTask);

            var summary = await summaryTask;
            var bugs = await bugsTask;
            var tasks = await tasksTask;
            var builds = await buildsTask;
            var members = await membersTask;
            var retests = await retestsTask;
            var bugTypes = await bugTypesTask;
            var reporters = await reportersTask;

            if (summary is not null)
            {
                TotalReportsText.Text = summary.TotalReports.ToString(TurkishUi.Culture);
                ActiveIssuesText.Text = (summary.InProgress + summary.NewOrUnstarted + summary.OnHold)
                    .ToString(TurkishUi.Culture);
                RetestIssuesText.Text = summary.RetestRequired.ToString(TurkishUi.Culture);
                ResolvedIssuesText.Text = summary.Resolved.ToString(TurkishUi.Culture);
                ResolutionRateText.Text = $"%{summary.ResolutionPercentage:0.#}";
                SetProgressSegments(summary);
            }

            BugReportsGrid.ItemsSource = bugs.Select(item => new BugRow(item)).ToList();
            RetestsGrid.ItemsSource = retests.Select(item => new RetestRow(item)).ToList();
            TasksGrid.ItemsSource = tasks.Select(item => new TaskRow(item)).ToList();
            BuildsGrid.ItemsSource = builds.Select(item => new BuildRow(item)).ToList();

            var taskCounts = tasks.SelectMany(item => item.AssigneeIds).GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
            var retestNames = retests.SelectMany(item => item.Assignees).GroupBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
            TestersGrid.ItemsSource = members.Where(item => item.Role == "tester").Select(item => new
            {
                Name = item.DisplayName,
                Tasks = taskCounts.GetValueOrDefault(item.Id),
                Retests = retestNames.GetValueOrDefault(item.DisplayName)
            }).ToList();

            BugTypesGrid.ItemsSource = bugTypes.Select(item => new
            {
                Name = item.Key,
                Count = item.Count,
                Resolved = item.Resolved,
                Rate = $"%{item.ResolutionPercentage:0.#}"
            }).ToList();
            TesterStatsGrid.ItemsSource = reporters.Select(item => new
            {
                Name = item.Key,
                Reports = item.Count,
                Valid = item.Count,
                VideoRate = "—",
                RetestRate = "—"
            }).ToList();

            var allRetestResults = retests.SelectMany(item => item.Results).ToList();
            var passed = allRetestResults.Count(item => item.Result == "passed");
            var failed = allRetestResults.Count(item => item.Result == "failed");
            var decisive = passed + failed;
            FirstRetestPassText.Text = decisive == 0 ? "—" : $"%{passed * 100d / decisive:0.#}";
            FailedRetestText.Text = failed.ToString(TurkishUi.Culture);
            ProjectStatus.Text = $"{project.SectionLabel} • {TurkishUi.Status(project.Status)} • Son yenileme {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            ProjectStatus.Text = $"{TurkishUi.Status(project.Status)} • Veriler alınamadı: {ex.Message}";
        }
    }

    private async void BugReportsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (BugReportsGrid.SelectedItem is not BugRow row)
        {
            return;
        }
        var window = new BugDetailWindow(_platformApi, row.Source.Id, IsAdminRole(_signedInUser.Role))
        {
            Owner = this
        };
        window.ShowDialog();
        if (window.Changed && _currentProject is not null)
        {
            await LoadProjectDashboardAsync(_currentProject);
        }
    }

    private void AttachShellButtons()
    {
        AttachButton("+ Yeni Görev", async (_, _) => await OpenTaskCreateAsync());
        AttachButton("+ Yeni Test Sürümü", (_, _) => OpenBuildCenter());
        AttachButton("📦  Test Sürümleri", (_, _) => OpenBuildCenter());
        AttachButton("🔔  Bildirimler", (_, _) => OpenNotifications());
        AttachButton("⚙  Proje Ayarları", (_, _) => OpenProjectManagement());
        AttachButton("Projeyi Kapat", async (_, _) => await CloseCurrentProjectAsync());

        var tabControl = FindVisualChild<TabControl>(this);
        if (tabControl is not null)
        {
            AttachButton("📊  Genel Bakış", (_, _) => tabControl.SelectedIndex = 0);
            AttachButton("🐞  Hata Raporları", (_, _) => tabControl.SelectedIndex = 1);
            AttachButton("🔁  Yeniden Testler", (_, _) => tabControl.SelectedIndex = 2);
            AttachButton("👥  Test Yönetimi", (_, _) => tabControl.SelectedIndex = 3);
            AttachButton("🚀  İstemci Sürümleri", (_, _) => tabControl.SelectedIndex = 5);
        }
    }

    private async Task OpenTaskCreateAsync()
    {
        if (_currentProject is null)
        {
            return;
        }
        var window = new TaskCreateWindow(_platformApi, _currentProject.Id) { Owner = this };
        if (window.ShowDialog() == true)
        {
            await LoadProjectDashboardAsync(_currentProject);
        }
    }

    private void OpenBuildCenter()
    {
        if (_currentProject is null)
        {
            return;
        }
        var window = new BuildCenterWindow(_platformApi, _currentProject.Id, IsAdminRole(_signedInUser.Role)) { Owner = this };
        window.ShowDialog();
        if (window.Changed)
        {
            _ = LoadProjectDashboardAsync(_currentProject);
        }
    }

    private void OpenNotifications()
    {
        if (_currentProject is null || !IsAdminRole(_signedInUser.Role))
        {
            if (!IsAdminRole(_signedInUser.Role))
            {
                MessageBox.Show("Bildirim gönderme yetkisi yalnızca yöneticilerdedir.", "Yetki Gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }
        new NotificationAdminWindow(_platformApi, _currentProject.Id) { Owner = this }.ShowDialog();
    }

    private void OpenProjectManagement()
    {
        if (!IsAdminRole(_signedInUser.Role))
        {
            MessageBox.Show("Proje yönetimi yalnızca yöneticilere açıktır.", "Yetki Gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var window = new ProjectManagementWindow(_platformApi, _currentProject) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _ = LoadProjectsAsync(window.SelectedProjectId ?? _currentProject?.Id);
        }
    }

    private async Task CloseCurrentProjectAsync()
    {
        if (_currentProject is null || !IsAdminRole(_signedInUser.Role))
        {
            return;
        }
        var confirm = MessageBox.Show(
            $"'{_currentProject.Name}' projesini kapatmak istiyor musunuz? Bu işlem dosyaları silmez; yeni çalışma kabulünü durdurur.",
            "Projeyi Kapat",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            await _platformApi.CloseProjectAsync(_currentProject.Id);
            await LoadProjectsAsync(_currentProject.Id);
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Proje Kapatılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void TeamManagementButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IsAdminRole(_signedInUser.Role))
        {
            return;
        }
        new TeamManagementWindow(_api) { Owner = this }.ShowDialog();
    }

    private void SetProgressSegments(IssueProgress summary)
    {
        var total = Math.Max(1, summary.ValidKnownIssues);
        ResolvedColumn.Width = new GridLength(Math.Max(0, summary.Resolved), GridUnitType.Star);
        InProgressColumn.Width = new GridLength(Math.Max(0, summary.InProgress), GridUnitType.Star);
        RetestColumn.Width = new GridLength(Math.Max(0, summary.RetestRequired), GridUnitType.Star);
        NewColumn.Width = new GridLength(Math.Max(0, total - summary.Resolved - summary.InProgress - summary.RetestRequired), GridUnitType.Star);
    }

    private void ClearSummary()
    {
        TotalReportsText.Text = "—";
        ActiveIssuesText.Text = "—";
        RetestIssuesText.Text = "—";
        ResolvedIssuesText.Text = "—";
        ResolutionRateText.Text = "—";
        ResolvedColumn.Width = new GridLength(1, GridUnitType.Star);
        InProgressColumn.Width = new GridLength(1, GridUnitType.Star);
        RetestColumn.Width = new GridLength(1, GridUnitType.Star);
        NewColumn.Width = new GridLength(1, GridUnitType.Star);
    }

    private void ClearOperationalGrids()
    {
        BugReportsGrid.ItemsSource = null;
        RetestsGrid.ItemsSource = null;
        TasksGrid.ItemsSource = null;
        TestersGrid.ItemsSource = null;
        BuildsGrid.ItemsSource = null;
        BugTypesGrid.ItemsSource = null;
        TesterStatsGrid.ItemsSource = null;
    }

    private void AttachButton(string content, RoutedEventHandler handler)
    {
        foreach (var button in FindVisualChildren<Button>(this).Where(item => Equals(item.Content, content)))
        {
            button.Click += handler;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        => FindVisualChildren<T>(parent).FirstOrDefault();

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is null)
        {
            yield break;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                yield return typed;
            }
            foreach (var nested in FindVisualChildren<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static bool IsAdminRole(string role) => role is "admin" or "super_admin";

    private sealed class BugRow
    {
        public BugRow(AdminBugListItem source)
        {
            Source = source;
            Key = source.Key;
            Title = source.Title;
            BugType = source.BugType;
            Build = source.BuildVersion ?? "—";
            Tester = source.ReporterName;
            Status = TurkishUi.Status(source.Status);
        }
        public AdminBugListItem Source { get; }
        public string Key { get; }
        public string Title { get; }
        public string BugType { get; }
        public string Build { get; }
        public string Tester { get; }
        public string Status { get; }
    }

    private sealed class RetestRow
    {
        public RetestRow(AdminRetestItem source)
        {
            BugKey = source.BugKey;
            BuildVersion = source.BuildVersion ?? "—";
            Assignees = string.Join(", ", source.Assignees);
            Results = source.Results.Count == 0 ? "Sonuç bekleniyor" : string.Join(", ", source.Results.Select(item => $"{item.TesterName}: {TurkishUi.RetestResult(item.Result)}"));
            Deadline = TurkishUi.Deadline(source.DeadlineAt);
        }
        public string BugKey { get; }
        public string BuildVersion { get; }
        public string Assignees { get; }
        public string Results { get; }
        public string Deadline { get; }
    }

    private sealed class TaskRow
    {
        public TaskRow(AdminTaskItem source)
        {
            Title = source.Title;
            Build = source.BuildVersion ?? "—";
            AssigneeCount = source.Assignees.Count;
            Deadline = TurkishUi.Deadline(source.DeadlineAt);
        }
        public string Title { get; }
        public string Build { get; }
        public int AssigneeCount { get; }
        public string Deadline { get; }
    }

    private sealed class BuildRow
    {
        public BuildRow(AdminBuildItem source)
        {
            Version = source.Version;
            Title = source.Title;
            Status = TurkishUi.Status(source.Status);
            UploadedBy = source.UploadedBy;
            UploadedAt = TurkishUi.Date(source.UploadedAt);
        }
        public string Version { get; }
        public string Title { get; }
        public string Status { get; }
        public string UploadedBy { get; }
        public string UploadedAt { get; }
    }
}
