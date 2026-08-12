using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class ProjectManagementWindow : Window
{
    private readonly PlatformApiClient _api;
    private ProjectSummary? _project;
    private readonly ObservableCollection<UserChoice> _users = [];

    public ProjectManagementWindow(PlatformApiClient api, ProjectSummary? project)
    {
        InitializeComponent();
        _api = api;
        _project = project;
        UsersList.ItemsSource = _users;
        UpdateProjectStateUi();
        Loaded += ProjectManagementWindow_Loaded;
    }

    public string? SelectedProjectId { get; private set; }

    private async void ProjectManagementWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UiMotion.Reveal(ProjectRoot, 18);
        if (_project is null)
        {
            return;
        }
        await ReloadCurrentProjectAsync();
    }

    private void UpdateProjectStateUi()
    {
        var hasProject = _project is not null;
        var isClosed = string.Equals(_project?.Status, "closed", StringComparison.OrdinalIgnoreCase);

        CurrentProjectText.Text = _project?.Name ?? "Aktif proje yok";
        CurrentProjectStatusText.Text = !hasProject ? "PROJE YOK" : isClosed ? "KAPALI" : "AKTİF";
        ProjectStatusBadge.Background = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                !hasProject ? "#182234" : isClosed ? "#351821" : "#173324"));
        ProjectStatusBadge.BorderBrush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                !hasProject ? "#40506A" : isClosed ? "#9C4355" : "#2D8C62"));
        CurrentProjectStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                !hasProject ? "#9AA9C2" : isClosed ? "#FF9EAA" : "#8EF0BB"));

        SaveMembersButton.IsEnabled = hasProject && !isClosed;
        SectionNameInput.IsEnabled = hasProject && !isClosed;
        CloseProjectButton.IsEnabled = hasProject && !isClosed;
        CloseProjectButton.Content = isClosed ? "Proje Kapalı" : "Projeyi Kapat";
        PurgeProjectButton.IsEnabled = hasProject;
    }

    private async Task ReloadCurrentProjectAsync()
    {
        if (_project is null)
        {
            return;
        }
        try
        {
            var allUsers = await _api.GetAllUsersAsync();
            var members = await _api.GetProjectMembersAsync(_project.Id);
            var memberIds = members.Select(item => item.Id).ToHashSet();
            _users.Clear();
            foreach (var user in allUsers.Where(item => item.Enabled))
            {
                _users.Add(new UserChoice(user.Id, user.DisplayName, TurkishUi.Role(user.Role), memberIds.Contains(user.Id)));
            }
            SectionsList.ItemsSource = await _api.GetSectionsAsync(_project.Id);
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(ex.Message, "Proje Ayarları", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveMembersButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            return;
        }
        try
        {
            await _api.ReplaceProjectMembersAsync(
                _project.Id,
                _users.Where(item => item.Selected).Select(item => item.Id));
            MessageBox.Show("Proje üyeleri güncellendi.", "Kaydedildi", MessageBoxButton.OK, MessageBoxImage.Information);
            SelectedProjectId = _project.Id;
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Üyeler Güncellenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void AddSectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null || string.IsNullOrWhiteSpace(SectionNameInput.Text))
        {
            return;
        }
        try
        {
            var current = await _api.GetSectionsAsync(_project.Id);
            await _api.CreateSectionAsync(_project.Id, SectionNameInput.Text.Trim(), current.Count + 1);
            SectionNameInput.Clear();
            SectionsList.ItemsSource = await _api.GetSectionsAsync(_project.Id);
            SelectedProjectId = _project.Id;
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Bölüm Eklenemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void CloseProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null || string.Equals(_project.Status, "closed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (MessageBox.Show(
                $"'{_project.Name}' projesini kapatmak istiyor musunuz?\n\nKapatma işlemi hiçbir veriyi silmez. Tester erişimi ve yeni operasyonlar durdurulur; daha sonra kalıcı silme yapılabilir.",
                "Projeyi Kapat",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        CloseProjectButton.IsEnabled = false;
        try
        {
            await _api.CloseProjectAsync(_project.Id);
            _project = _project with { Status = "closed" };
            SelectedProjectId = _project.Id;
            UpdateProjectStateUi();
            UiMotion.Pulse(ProjectStatusBadge);
            MessageBox.Show("Proje kapatıldı. Veri silinmedi; artık isterseniz güvenli kalıcı silme akışını başlatabilirsiniz.", "Proje Kapalı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Proje Kapatılamadı", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateProjectStateUi();
        }
    }

    private async void PurgeProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            MessageBox.Show("Kalıcı silme için önce bir proje seçin.", "Proje Seçilmedi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.Equals(_project.Status, "closed", StringComparison.OrdinalIgnoreCase))
        {
            var closeFirst = MessageBox.Show(
                $"'{_project.Name}' projesi hâlâ aktif.\n\nKalıcı silme için önce proje kapatılmalıdır. Şimdi projeyi kapatıp silme ekranına devam edilsin mi?",
                "Önce Projeyi Kapat",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (closeFirst != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                await _api.CloseProjectAsync(_project.Id);
                _project = _project with { Status = "closed" };
                SelectedProjectId = _project.Id;
                UpdateProjectStateUi();
            }
            catch (QaApiException ex)
            {
                MessageBox.Show(ex.Message, "Proje Kapatılamadı", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        var window = new PurgeWindow(_api, _project)
        {
            Owner = this
        };
        window.ShowDialog();
        if (window.ProjectPurged)
        {
            _project = null;
            SelectedProjectId = null;
            UpdateProjectStateUi();
            DialogResult = true;
            Close();
        }
    }

    private async void CreateProjectButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NewProjectNameInput.Text.Trim();
        var key = NewProjectKeyInput.Text.Trim().ToLowerInvariant();
        var label = (SectionLabelPicker.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Mission";
        if (name.Length < 2 || !Regex.IsMatch(key, "^[a-z0-9][a-z0-9-]+$"))
        {
            MessageBox.Show("Proje adını ve küçük harf/tire formatında geçerli bir proje anahtarını girin.", "Eksik veya Geçersiz Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CreateProjectButton.IsEnabled = false;
        try
        {
            var created = await _api.CreateProjectAsync(key, name, label);
            SelectedProjectId = created.Id;
            MessageBox.Show("Yeni proje oluşturuldu. Projeyi seçip tester üyelerini ve bölümleri ekleyebilirsiniz.", "Proje Oluşturuldu", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Proje Oluşturulamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            CreateProjectButton.IsEnabled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DialogResult is null && SelectedProjectId is not null)
        {
            DialogResult = true;
        }
        base.OnClosed(e);
    }

    private sealed class UserChoice
    {
        public UserChoice(string id, string name, string role, bool selected)
        {
            Id = id;
            Name = name;
            Role = role;
            Selected = selected;
        }
        public string Id { get; }
        public string Name { get; }
        public string Role { get; }
        public bool Selected { get; set; }
    }
}
