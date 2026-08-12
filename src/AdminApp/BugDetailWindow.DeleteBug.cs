using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class BugDetailWindow
{
    private Button? _deleteBugButton;

    private void InitializePermanentBugDeleteAction()
    {
        if (!_canAdminister || _deleteBugButton is not null || RetestButton.Parent is not Grid headerGrid)
        {
            return;
        }

        headerGrid.Children.Remove(RetestButton);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(actions, 2);

        RetestButton.Margin = new Thickness(0, 0, 8, 0);
        actions.Children.Add(RetestButton);

        _deleteBugButton = new Button
        {
            Content = "🗑 Raporu Tamamen Sil",
            Background = new SolidColorBrush(Color.FromRgb(58, 16, 24)),
            Foreground = new SolidColorBrush(Color.FromRgb(255, 166, 177)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(130, 43, 58)),
            Padding = new Thickness(13, 9, 13, 9),
            ToolTip = "Raporu, event geçmişini, yeniden testleri ve bütün kanıtlarını kalıcı olarak siler."
        };
        _deleteBugButton.Click += DeleteBugButton_Click;
        actions.Children.Add(_deleteBugButton);
        headerGrid.Children.Add(actions);
    }

    private async void DeleteBugButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_canAdminister || _detail is null || _deleteBugButton is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"{_detail.Key} • {_detail.Title}\n\n" +
            "Bu hata raporu; video/görselleri, yeniden test kayıtları ve hareket geçmişiyle birlikte tamamen silinecek. " +
            "Silinen kayıt Hata Merkezi'nde artık görünmeyecek.\n\nDevam edilsin mi?",
            "Hata Raporunu Tamamen Sil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Stop,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _deleteBugButton.IsEnabled = false;
        try
        {
            _playerTimer.Stop();
            VideoPlayer.Stop();
            VideoPlayer.Close();
            VideoPlayer.Source = null;
            _currentVideoPath = null;

            foreach (var evidence in _detail.Evidence)
            {
                TryDeleteLocalEvidenceCache(EvidenceCachePath(evidence));
            }

            await _api.DeleteBugForCurrentBackendAsync(_bugId);
            Changed = true;
            MessageBox.Show(
                "Hata raporu ve bağlı bütün kayıtlar kalıcı olarak silindi.",
                "Rapor Silindi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) when (ex is QaApiException or HttpRequestException or IOException or TaskCanceledException)
        {
            MessageBox.Show(
                $"Hata raporu tamamen silinemedi. Kayıt korunmuştur.\n\n{ex.Message}",
                "Rapor Silinemedi",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _deleteBugButton.IsEnabled = true;
        }
    }
}
