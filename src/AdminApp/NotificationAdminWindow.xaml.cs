using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class NotificationAdminWindow : Window
{
    private readonly PlatformApiClient _api;
    private readonly string _projectId;
    private readonly ObservableCollection<MemberChoice> _members = [];

    public NotificationAdminWindow(PlatformApiClient api, string projectId)
    {
        InitializeComponent();
        _api = api;
        _projectId = projectId;
        MembersList.ItemsSource = _members;
        Loaded += NotificationAdminWindow_Loaded;
    }

    private async void NotificationAdminWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var members = await _api.GetProjectMembersAsync(_projectId);
            foreach (var member in members.Where(item => item.Enabled))
            {
                _members.Add(new MemberChoice(member.Id, member.DisplayName));
            }
            await ReloadHistoryAsync();
            UpdateMemberVisibility();
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(ex.Message, "Bildirim Merkezi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ReloadHistoryAsync()
    {
        var items = await _api.GetAdminNotificationsAsync(_projectId);
        HistoryGrid.ItemsSource = items.Select(item => new
        {
            Date = TurkishUi.Date(item.CreatedAt),
            item.Title,
            Severity = SeverityText(item.Severity),
            Read = $"{item.ReadCount}/{item.RecipientCount}",
            Ack = item.Severity == "must_read" ? $"{item.AcknowledgedCount}/{item.RecipientCount}" : "—"
        }).ToList();
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleInput.Text.Trim();
        var body = BodyInput.Text.Trim();
        if (title.Length == 0 || body.Length == 0)
        {
            MessageBox.Show("Başlık ve mesaj alanlarını doldurun.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var severity = (SeverityPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "info";
        var target = (TargetPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "project";
        var selected = _members.Where(item => item.Selected).Select(item => item.Id).ToArray();
        if (target == "users" && selected.Length == 0)
        {
            MessageBox.Show("Seçili kişiler hedefi için en az bir kişi seçin.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SendButton.IsEnabled = false;
        try
        {
            await _api.CreateNotificationAsync(
                target == "all" ? null : _projectId,
                title,
                body,
                severity,
                target,
                selected);
            TitleInput.Clear();
            BodyInput.Clear();
            foreach (var member in _members)
            {
                member.Selected = false;
            }
            MembersList.Items.Refresh();
            await ReloadHistoryAsync();
            MessageBox.Show(
                severity == "must_read"
                    ? "Okunması zorunlu duyuru gönderildi. Kişi bazlı onay durumu izlenebilir."
                    : "Bildirim gönderildi.",
                "Gönderildi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Bildirim Gönderilemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private void TargetPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateMemberVisibility();

    private void UpdateMemberVisibility()
    {
        if (MembersList is null || TargetPicker is null)
        {
            return;
        }
        var target = (TargetPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        MembersList.Visibility = target == "users" ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string SeverityText(string value) => value switch
    {
        "info" => "Bilgi",
        "warning" => "Uyarı",
        "critical" => "Kritik",
        "must_read" => "Zorunlu",
        _ => value
    };

    private sealed class MemberChoice
    {
        public MemberChoice(string id, string name)
        {
            Id = id;
            Name = name;
        }
        public string Id { get; }
        public string Name { get; }
        public bool Selected { get; set; }
    }
}
