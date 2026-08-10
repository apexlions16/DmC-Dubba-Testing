using System.Collections.ObjectModel;
using System.Windows;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class TaskCreateWindow : Window
{
    private readonly PlatformApiClient _api;
    private readonly string _projectId;
    private readonly ObservableCollection<MemberChoice> _members = [];

    public TaskCreateWindow(PlatformApiClient api, string projectId)
    {
        InitializeComponent();
        _api = api;
        _projectId = projectId;
        MembersList.ItemsSource = _members;
        DeadlinePicker.SelectedDate = DateTime.Today.AddDays(14);
        Loaded += TaskCreateWindow_Loaded;
    }

    private async void TaskCreateWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SectionPicker.ItemsSource = await _api.GetSectionsAsync(_projectId);
            var builds = (await _api.GetBuildsAsync(_projectId))
                .Where(item => item.Status is "current" or "candidate" or "superseded")
                .ToList();
            BuildPicker.ItemsSource = builds;
            BuildPicker.SelectedItem = builds.FirstOrDefault(item => item.Status == "current") ?? builds.FirstOrDefault();

            var members = await _api.GetProjectMembersAsync(_projectId);
            _members.Clear();
            foreach (var member in members.Where(item => item.Enabled && item.Role == "tester"))
            {
                _members.Add(new MemberChoice(member.Id, member.DisplayName, TurkishUi.Role(member.Role)));
            }
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(ex.Message, "Görev Hazırlığı", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleInput.Text.Trim();
        var selected = _members.Where(item => item.Selected).Select(item => item.Id).ToArray();
        if (title.Length < 2)
        {
            MessageBox.Show("Göreve açıklayıcı bir başlık yazın.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (selected.Length == 0)
        {
            MessageBox.Show("En az bir test ekibi üyesi seçin.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CreateButton.IsEnabled = false;
        try
        {
            var section = SectionPicker.SelectedItem as SectionItem;
            var build = BuildPicker.SelectedItem as AdminBuildItem;
            var deadline = DeadlinePicker.SelectedDate is DateTime date
                ? new DateTimeOffset(date.Date.AddHours(23).AddMinutes(59))
                : (DateTimeOffset?)null;
            await _api.CreateTaskAsync(
                _projectId,
                title,
                DescriptionInput.Text.Trim(),
                section?.Id,
                build?.Id,
                deadline,
                selected);
            MessageBox.Show("Ortak görev oluşturuldu ve seçilen kişilere atandı.", "Görev Oluşturuldu", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Görev Oluşturulamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            CreateButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed class MemberChoice
    {
        public MemberChoice(string id, string displayName, string roleText)
        {
            Id = id;
            DisplayName = displayName;
            RoleText = roleText;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string RoleText { get; }
        public bool Selected { get; set; }
    }
}
