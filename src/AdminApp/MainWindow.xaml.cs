using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class MainWindow : Window
{
    private readonly QaApiClient _api;
    private readonly CurrentUserSummary _signedInUser;
    private List<ProjectSummary> _projects = [];

    public MainWindow(QaApiClient api, CurrentUserSummary signedInUser)
    {
        InitializeComponent();
        _api = api;
        _signedInUser = signedInUser;

        SignedInUserText.Text = $"{signedInUser.DisplayName} • {TurkishUi.Role(signedInUser.Role)}";
        TeamManagementButton.Visibility = IsAdminRole(signedInUser.Role)
            ? Visibility.Visible
            : Visibility.Collapsed;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadProjectsAsync();
    }

    private async Task LoadProjectsAsync()
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
                ProjectPicker.SelectedIndex = 0;
            }
            else
            {
                ProjectTitle.Text = "Henüz erişebildiğiniz bir proje yok";
                ProjectStatus.Text = "Yeni proje oluşturulduğunda burada görünecek.";
                ClearSummary();
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

        ProjectTitle.Text = project.Name;
        ProjectStatus.Text = $"{project.SectionLabel} • {TurkishUi.Status(project.Status)}";

        try
        {
            var summary = await _api.GetProjectProgressAsync(project.Id);
            if (summary is null)
            {
                ClearSummary();
                return;
            }

            TotalReportsText.Text = summary.TotalReports.ToString(TurkishUi.Culture);
            ActiveIssuesText.Text = (summary.InProgress + summary.NewOrUnstarted + summary.OnHold)
                .ToString(TurkishUi.Culture);
            RetestIssuesText.Text = summary.RetestRequired.ToString(TurkishUi.Culture);
            ResolvedIssuesText.Text = summary.Resolved.ToString(TurkishUi.Culture);
            ResolutionRateText.Text = $"%{summary.ResolutionPercentage:0.#}";

            SetProgressSegments(summary);
        }
        catch (QaApiException ex)
        {
            ProjectStatus.Text = $"{TurkishUi.Status(project.Status)} • İstatistik alınamadı: {ex.Message}";
            ClearSummary();
        }
    }

    private void SetProgressSegments(IssueProgress summary)
    {
        var total = Math.Max(1, summary.ValidKnownIssues);
        ResolvedColumn.Width = new GridLength(Math.Max(0, summary.Resolved), GridUnitType.Star);
        InProgressColumn.Width = new GridLength(Math.Max(0, summary.InProgress), GridUnitType.Star);
        RetestColumn.Width = new GridLength(Math.Max(0, summary.RetestRequired), GridUnitType.Star);
        NewColumn.Width = new GridLength(
            Math.Max(0, total - summary.Resolved - summary.InProgress - summary.RetestRequired),
            GridUnitType.Star);
    }

    private void TeamManagementButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IsAdminRole(_signedInUser.Role))
        {
            MessageBox.Show(
                "Ekip ve cihaz yönetimi yalnızca yönetici hesaplarına açıktır.",
                "Yetki Gerekli",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var window = new TeamManagementWindow(_api)
        {
            Owner = this
        };
        window.ShowDialog();
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

    private static bool IsAdminRole(string role)
        => role is "admin" or "super_admin";
}
