using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class AdminLoginWindow : Window
{
    private static readonly string StateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameQaAdmin");

    private static readonly string StateFile = Path.Combine(StateDirectory, "client.json");
    private static readonly byte[] CredentialEntropy = Encoding.UTF8.GetBytes("GameQaAdmin.DeviceCredential.v1");

    private readonly QaApiClient _api;
    private AdminClientState _state = new();

    public AdminLoginWindow(QaApiClient api)
    {
        InitializeComponent();
        _api = api;
        LoadState();
        Loaded += AdminLoginWindow_Loaded;
    }

    public CurrentUserSummary? SignedInUser { get; private set; }
    public string? DeviceId { get; private set; }
    public string? DeviceCredential { get; private set; }

    private void LoadState()
    {
        Directory.CreateDirectory(StateDirectory);
        if (File.Exists(StateFile))
        {
            try
            {
                _state = JsonSerializer.Deserialize<AdminClientState>(File.ReadAllText(StateFile)) ?? new();
            }
            catch
            {
                _state = new();
            }
        }

        if (string.IsNullOrWhiteSpace(_state.InstallationId))
        {
            _state.InstallationId = Guid.NewGuid().ToString("N");
            SaveState();
        }

        DisplayNameInput.Text = _state.DisplayName;
    }

    private async void AdminLoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.DeviceId) || string.IsNullOrWhiteSpace(_state.ProtectedCredential))
        {
            return;
        }

        var credential = UnprotectCredential(_state.ProtectedCredential);
        if (string.IsNullOrWhiteSpace(credential))
        {
            ClearStoredCredential();
            StatusText.Text = "Bu bilgisayardaki güvenli oturum bilgisi okunamadı. Adınızı yeniden girin.";
            return;
        }

        _api.SetDeviceCredential(_state.DeviceId, credential);
        try
        {
            var user = await _api.GetCurrentUserAsync();
            if (!IsManagementRole(user.Role))
            {
                _api.ClearAuthentication();
                ClearStoredCredential();
                StatusText.Text = "Bu hesabın Yönetim Merkezi kullanma yetkisi yok.";
                return;
            }

            DeviceId = _state.DeviceId;
            DeviceCredential = credential;
            CompleteLogin(user);
        }
        catch (QaApiException ex)
        {
            _api.ClearAuthentication();
            ClearStoredCredential();
            StatusText.Text = ex.Message;
        }
        catch (HttpRequestException)
        {
            StatusText.Text = "Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.";
        }
        catch (TaskCanceledException)
        {
            StatusText.Text = "Sunucu zamanında yanıt vermedi. Lütfen tekrar deneyin.";
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var name = DisplayNameInput.Text.Trim();
        if (name.Length < 2)
        {
            StatusText.Text = "Lütfen yönetici veya geliştirici hesabınızdaki adınızı yazın.";
            return;
        }

        LoginButton.IsEnabled = false;
        StatusText.Text = "Cihaz eşleştiriliyor...";

        try
        {
            var enrollment = await _api.EnrollDeviceAsync(
                name,
                _state.InstallationId,
                Environment.MachineName,
                clientKind: "admin");

            _state.DisplayName = enrollment.DisplayName;
            _state.DeviceId = enrollment.DeviceId;
            _state.ProtectedCredential = ProtectCredential(enrollment.Credential);
            SaveState();

            _api.SetDeviceCredential(enrollment.DeviceId, enrollment.Credential);
            var user = await _api.GetCurrentUserAsync();
            if (!IsManagementRole(user.Role))
            {
                _api.ClearAuthentication();
                ClearStoredCredential();
                StatusText.Text = "Bu hesabın Yönetim Merkezi kullanma yetkisi yok.";
                return;
            }

            DeviceId = enrollment.DeviceId;
            DeviceCredential = enrollment.Credential;
            CompleteLogin(user);
        }
        catch (QaApiException ex)
        {
            StatusText.Text = ex.Message;
        }
        catch (HttpRequestException)
        {
            StatusText.Text = "Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.";
        }
        catch (TaskCanceledException)
        {
            StatusText.Text = "Sunucu zamanında yanıt vermedi. Lütfen tekrar deneyin.";
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void CompleteLogin(CurrentUserSummary user)
    {
        SignedInUser = user;
        _state.DisplayName = user.DisplayName;
        SaveState();
        DialogResult = true;
        Close();
    }

    private static bool IsManagementRole(string role)
        => role is "developer" or "admin" or "super_admin";

    private void ClearStoredCredential()
    {
        _state.DeviceId = null;
        _state.ProtectedCredential = null;
        DeviceId = null;
        DeviceCredential = null;
        SaveState();
    }

    private void SaveState()
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(
            StateFile,
            JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ProtectCredential(string credential)
    {
        var clear = Encoding.UTF8.GetBytes(credential);
        var encrypted = ProtectedData.Protect(clear, CredentialEntropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string? UnprotectCredential(string protectedCredential)
    {
        try
        {
            var encrypted = Convert.FromBase64String(protectedCredential);
            var clear = ProtectedData.Unprotect(encrypted, CredentialEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clear);
        }
        catch
        {
            return null;
        }
    }

    private sealed class AdminClientState
    {
        public string DisplayName { get; set; } = string.Empty;
        public string InstallationId { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public string? ProtectedCredential { get; set; }
    }
}
