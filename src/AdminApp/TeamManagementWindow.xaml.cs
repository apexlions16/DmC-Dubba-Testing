using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class TeamManagementWindow : Window
{
    private readonly QaApiClient _api;
    private List<UserRow> _rows = [];

    public TeamManagementWindow(QaApiClient api)
    {
        InitializeComponent();
        _api = api;
        Loaded += TeamManagementWindow_Loaded;
    }

    private UserRow? SelectedUser => UsersGrid.SelectedItem as UserRow;

    private async void TeamManagementWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshUsersAsync();
    }

    private async Task RefreshUsersAsync()
    {
        SetBusy(true);
        try
        {
            var users = await _api.GetAdminUsersAsync();
            _rows = users
                .Select(user => new UserRow(
                    user.Id,
                    user.DisplayName,
                    user.Role,
                    TurkishUi.Role(user.Role),
                    user.Enabled,
                    user.Enabled ? "Etkin" : "Devre Dışı",
                    user.ActiveDevices,
                    TurkishUi.Date(user.CreatedAt, includeTime: false)))
                .ToList();
            UsersGrid.ItemsSource = _rows;
            StatusText.Text = $"{_rows.Count} kullanıcı yüklendi.";
            UpdateSelectionButtons();
        }
        catch (QaApiException ex)
        {
            StatusText.Text = ex.Message;
        }
        catch (HttpRequestException)
        {
            StatusText.Text = "Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void CreateUserButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NewUserNameInput.Text.Trim();
        if (name.Length < 2)
        {
            StatusText.Text = "Lütfen ekip üyesinin adını yazın.";
            return;
        }

        if (RolePicker.SelectedItem is not ComboBoxItem selectedRole || selectedRole.Tag is not string role)
        {
            StatusText.Text = "Lütfen kullanıcı rolünü seçin.";
            return;
        }

        SetBusy(true);
        try
        {
            await _api.CreateAdminUserAsync(name, role);
            NewUserNameInput.Clear();
            StatusText.Text = $"{name} oluşturuldu.";
            await RefreshUsersAsync();
        }
        catch (QaApiException ex)
        {
            StatusText.Text = ex.Message;
        }
        catch (HttpRequestException)
        {
            StatusText.Text = "Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ToggleEnabledButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedUser;
        if (selected is null)
        {
            StatusText.Text = "Önce bir kullanıcı seçin.";
            return;
        }

        var nextEnabled = !selected.Enabled;
        var action = nextEnabled ? "etkinleştirmek" : "devre dışı bırakmak";
        if (MessageBox.Show(
                $"{selected.DisplayName} kullanıcısını {action} istiyor musunuz?",
                "Kullanıcı Durumu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await _api.SetUserEnabledAsync(selected.Id, nextEnabled);
            await RefreshUsersAsync();
        }
        catch (QaApiException ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ResetDeviceButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedUser;
        if (selected is null)
        {
            StatusText.Text = "Önce bir kullanıcı seçin.";
            return;
        }

        if (MessageBox.Show(
                $"{selected.DisplayName} için mevcut cihaz eşleşmesini sıfırlamak istiyor musunuz?\n\n"
                + "Bu işlemden sonra mevcut cihazdaki oturum geçersiz olur ve kullanıcı yeniden eşleşme yapmalıdır.",
                "Cihaz Eşleşmesini Sıfırla",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var revoked = await _api.ResetUserDevicesAsync(selected.Id);
            StatusText.Text = $"{selected.DisplayName} için {revoked} cihaz oturumu sıfırlandı.";
            await RefreshUsersAsync();
        }
        catch (QaApiException ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshUsersAsync();
    }

    private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionButtons();
    }

    private void UpdateSelectionButtons()
    {
        var selected = SelectedUser;
        ToggleEnabledButton.IsEnabled = selected is not null;
        ResetDeviceButton.IsEnabled = selected is not null;
        ToggleEnabledButton.Content = selected?.Enabled == true
            ? "Hesabı Devre Dışı Bırak"
            : "Hesabı Etkinleştir";
    }

    private void SetBusy(bool busy)
    {
        CreateUserButton.IsEnabled = !busy;
        RolePicker.IsEnabled = !busy;
        NewUserNameInput.IsEnabled = !busy;
        if (busy)
        {
            ToggleEnabledButton.IsEnabled = false;
            ResetDeviceButton.IsEnabled = false;
        }
        else
        {
            UpdateSelectionButtons();
        }
    }

    private sealed record UserRow(
        string Id,
        string DisplayName,
        string Role,
        string RoleText,
        bool Enabled,
        string EnabledText,
        int ActiveDevices,
        string CreatedAtText);
}
