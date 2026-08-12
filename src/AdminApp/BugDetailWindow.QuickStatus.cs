using System.Windows;
using System.Windows.Controls;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class BugDetailWindow
{
    private Button? _quickWorkButton;
    private Button? _quickResolveButton;
    private Button? _quickReopenButton;

    private void InitializeQuickStatusActions()
    {
        if (RetestButton.Parent is not Grid headerGrid)
        {
            return;
        }

        headerGrid.Children.Remove(RetestButton);

        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(actions, 2);

        _quickWorkButton = CreateQuickStatusButton("▶ Çalışılıyor", "in_progress", null);
        _quickResolveButton = CreateQuickStatusButton("✓ Çözüldü", "resolved", "SuccessButton");
        _quickReopenButton = CreateQuickStatusButton("↺ Yeniden Aç", "reopened", null);

        RetestButton.Margin = new Thickness(0, 4, 8, 4);
        actions.Children.Add(_quickWorkButton);
        actions.Children.Add(RetestButton);
        actions.Children.Add(_quickResolveButton);
        actions.Children.Add(_quickReopenButton);
        headerGrid.Children.Add(actions);
    }

    private Button CreateQuickStatusButton(string text, string status, string? styleKey)
    {
        var button = new Button
        {
            Content = text,
            Tag = status,
            MinWidth = 104,
            IsEnabled = _canAdminister,
            Margin = new Thickness(0, 4, 8, 4)
        };
        if (!string.IsNullOrWhiteSpace(styleKey) && TryFindResource(styleKey) is Style style)
        {
            button.Style = style;
        }
        button.Click += QuickStatusButton_Click;
        return button;
    }

    private async void QuickStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_canAdminister || _detail is null || sender is not Button button || button.Tag is not string status)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            await _api.ChangeBugStatusAsync(
                _detail.Id,
                status,
                status switch
                {
                    "resolved" => "Yönetici hızlı aksiyondan sorunu çözüldü olarak işaretledi.",
                    "reopened" => "Yönetici sorunu yeniden açtı.",
                    "in_progress" => "Yönetici sorunu çalışma kuyruğuna aldı.",
                    _ => null
                },
                string.IsNullOrWhiteSpace(RootCausePicker.Text) ? null : RootCausePicker.Text.Trim(),
                _detail.ReportedBuildId);
            Changed = true;
            await ReloadAsync();
            UiMotion.Pulse(button);
        }
        catch (QaApiException ex)
        {
            MessageBox.Show(ex.Message, "Durum Değiştirilemedi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            button.IsEnabled = _canAdminister;
        }
    }

    private void UpdateQuickStatusActions()
    {
        if (_detail is null)
        {
            return;
        }

        var resolved = string.Equals(_detail.Status, "resolved", StringComparison.OrdinalIgnoreCase);
        var working = string.Equals(_detail.Status, "in_progress", StringComparison.OrdinalIgnoreCase);

        if (_quickResolveButton is not null)
        {
            _quickResolveButton.Visibility = resolved ? Visibility.Collapsed : Visibility.Visible;
        }
        if (_quickReopenButton is not null)
        {
            _quickReopenButton.Visibility = resolved ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_quickWorkButton is not null)
        {
            _quickWorkButton.Visibility = working || resolved ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
