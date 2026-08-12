using System.Windows;

namespace DmC.Qa.Admin;

public partial class MainWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        UiMotion.Reveal(MainTabs, 18);
    }

    private void OverviewNav_Click(object sender, RoutedEventArgs e) => SwitchWorkspace(0, "GENEL BAKIŞ");
    private void BugsNav_Click(object sender, RoutedEventArgs e) => SwitchWorkspace(1, "HATA RAPORLARI");
    private void RetestsNav_Click(object sender, RoutedEventArgs e) => SwitchWorkspace(2, "YENİDEN TESTLER");
    private void TestManagementNav_Click(object sender, RoutedEventArgs e) => SwitchWorkspace(3, "TEST YÖNETİMİ");
    private void BuildsNav_Click(object sender, RoutedEventArgs e) => SwitchWorkspace(4, "BUILD MERKEZİ");

    private void NotificationsNav_Click(object sender, RoutedEventArgs e)
    {
        UiMotion.Pulse(MainTabs);
        OpenNotifications();
    }

    private void ReleaseNav_Click(object sender, RoutedEventArgs e)
    {
        UiMotion.Pulse(MainTabs);
        OpenReleaseCenter();
    }

    private void ProjectSettingsNav_Click(object sender, RoutedEventArgs e)
    {
        OpenProjectManagement();
    }

    private async void NewTaskButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenTaskCreateAsync();
    }

    private void NewBuildButton_Click(object sender, RoutedEventArgs e)
    {
        OpenBuildCenter();
    }

    private void SwitchWorkspace(int index, string title)
    {
        if (index < 0 || index >= MainTabs.Items.Count)
        {
            return;
        }

        MainTabs.SelectedIndex = index;
        WorkspaceEyebrow.Text = title;
        UiMotion.Reveal(MainTabs, 16);
    }
}
