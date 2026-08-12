using System.Windows;

namespace DmC.Qa.Tester;

public partial class MainWindow
{
    private void PanelNavButton_Click(object sender, RoutedEventArgs e)
    {
        MainScrollViewer.ScrollToTop();
        UiMotion.FadeIn(DashboardContent, 10);
    }

    private void TaskNavButton_Click(object sender, RoutedEventArgs e) => FocusSection(TaskCard);

    private void RetestNavButton_Click(object sender, RoutedEventArgs e)
    {
        FocusSection(RetestCard);
        if (_pendingRetests.Count > 0 && !_blockingMustRead)
        {
            UiMotion.Pulse(RetestButton);
        }
    }

    private void BuildNavButton_Click(object sender, RoutedEventArgs e)
    {
        FocusSection(BuildCard);
        UiMotion.Pulse(BuildDownloadButton);
    }

    private void ReportsNavButton_Click(object sender, RoutedEventArgs e) => FocusSection(ReportsSection);

    private void FocusSection(FrameworkElement element)
    {
        element.BringIntoView();
        UiMotion.FadeIn(element, 8);
        UiMotion.Pulse(element);
    }
}
