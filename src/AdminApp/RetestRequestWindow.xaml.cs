using System.Collections.ObjectModel;
using System.Windows;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class RetestRequestWindow : Window
{
    private readonly PlatformApiClient _api;
    private readonly string _projectId;
    private readonly string _bugId;
    private readonly ObservableCollection<MemberChoice> _members = [];

    public RetestRequestWindow(PlatformApiClient api, string projectId, string bugId, string bugKey)
    {
        InitializeComponent();
        _api = api;
        _projectId = projectId;
        _bugId = bugId;
        BugText.Text = bugKey;
        MembersList.ItemsSource = _members;
        DeadlinePicker.SelectedDate = DateTime.Today.AddDays(3);
        Loaded += RetestRequestWindow_Loaded;
    }

    private async void RetestRequestWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
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
            MessageBox.Show(ex.Message, "Yeniden Test", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        if (BuildPicker.SelectedItem is not AdminBuildItem build)
        {
            MessageBox.Show("Yeniden test için bir test sürümü seçin.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var selected = _members.Where(item => item.Selected).Select(item => item.Id).ToArray();
        if (selected.Length == 0)
        {
            MessageBox.Show("En az bir test ekibi üyesi seçin.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SubmitButton.IsEnabled = false;
        try
        {
            var deadline = DeadlinePicker.SelectedDate is DateTime date
                ? new DateTimeOffset(date.Date.AddHours(23).AddMinutes(59))
                : (DateTimeOffset?)null;
            await _api.RequestRetestAsync(_bugId, build.Id, selected, NoteInput.Text.Trim(), deadline);
            MessageBox.Show("Yeniden test talebi seçilen kişilerin ekranına gönderildi.", "Yeniden Test Oluşturuldu", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Yeniden Test Oluşturulamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SubmitButton.IsEnabled = true;
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
