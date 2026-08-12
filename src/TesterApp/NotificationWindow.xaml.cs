using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using DmC.Qa.Shared;

namespace DmC.Qa.Tester;

public partial class NotificationWindow : Window
{
    private readonly PlatformApiClient _api;
    private List<InboxNotificationItem> _items = [];

    public NotificationWindow(PlatformApiClient api)
    {
        InitializeComponent();
        _api = api;
        Loaded += NotificationWindow_Loaded;
    }

    public bool AcknowledgementChanged { get; private set; }

    private async void NotificationWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            _items = (await _api.GetMyNotificationsAsync()).ToList();
            NotificationList.ItemsSource = _items.Select(item => new NotificationRow(item)).ToList();
            if (_items.Count > 0)
            {
                NotificationList.SelectedIndex = 0;
            }
            else
            {
                TitleText.Text = "Bildirim yok";
                BodyText.Text = "Şu anda yeni bir bildiriminiz bulunmuyor.";
                MetaText.Text = string.Empty;
                AcknowledgeButton.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(ex.Message, "Bildirimler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void NotificationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NotificationList.SelectedItem is not NotificationRow row)
        {
            return;
        }
        var item = row.Source;
        TitleText.Text = item.Title;
        BodyText.Text = item.Body;
        MetaText.Text = $"{TurkishUi.Severity(item.Severity)} • {TurkishUi.Date(item.CreatedAt)}";
        AcknowledgeButton.Visibility = item.RequiresAcknowledgement && item.AcknowledgedAt is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (item.ReadAt is null)
        {
            try
            {
                await _api.MarkNotificationReadAsync(item.Id);
            }
            catch
            {
            }
        }
    }

    private async void AcknowledgeButton_Click(object sender, RoutedEventArgs e)
    {
        if (NotificationList.SelectedItem is not NotificationRow row)
        {
            return;
        }
        AcknowledgeButton.IsEnabled = false;
        try
        {
            await _api.AcknowledgeNotificationAsync(row.Source.Id);
            AcknowledgementChanged = true;
            await ReloadAsync();
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Duyuru Onaylanamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            AcknowledgeButton.IsEnabled = true;
        }
    }

    private sealed class NotificationRow
    {
        public NotificationRow(InboxNotificationItem source)
        {
            Source = source;
            Title = source.AcknowledgedAt is null && source.RequiresAcknowledgement
                ? $"⚠ {source.Title}"
                : source.Title;
            Meta = $"{TurkishUi.Severity(source.Severity)} • {TurkishUi.Date(source.CreatedAt)}";
        }
        public InboxNotificationItem Source { get; }
        public string Title { get; }
        public string Meta { get; }
    }
}
