using System.Windows;

namespace DmC.Qa.Tester;

public partial class MainWindow
{
    private void PanelNavButton_Click(object sender, RoutedEventArgs e)
    {
        MainScrollViewer.ScrollToTop();
        UiMotion.Pulse(DashboardContent);
    }

    private void TaskNavButton_Click(object sender, RoutedEventArgs e)
    {
        TaskCard.BringIntoView();
        UiMotion.Pulse(TaskCard);
    }

    private void RetestNavButton_Click(object sender, RoutedEventArgs e)
    {
        RetestCard.BringIntoView();
        UiMotion.Pulse(RetestCard);
        RetestButton_Click(sender, e);
    }

    private void BuildNavButton_Click(object sender, RoutedEventArgs e)
    {
        BuildCard.BringIntoView();
        UiMotion.Pulse(BuildCard);
    }

    private void ReportsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ReportsSection.BringIntoView();
        UiMotion.Pulse(ReportsSection);
    }
}
