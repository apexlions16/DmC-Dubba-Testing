using System.Net.Http;
using System.Windows;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class App : Application
{
    public App()
    {
        TurkishUi.ApplyCulture();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var apiBaseUrl = Environment.GetEnvironmentVariable("GAME_QA_API") ?? "http://localhost:7860/";
        if (!apiBaseUrl.EndsWith('/'))
        {
            apiBaseUrl += "/";
        }
        var baseUri = new Uri(apiBaseUrl);

        var api = new QaApiClient(new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromMinutes(30)
        });

        var login = new AdminLoginWindow(api);
        if (login.ShowDialog() != true
            || login.SignedInUser is null
            || string.IsNullOrWhiteSpace(login.DeviceId)
            || string.IsNullOrWhiteSpace(login.DeviceCredential))
        {
            Shutdown();
            return;
        }

        var platformApi = new PlatformApiClient(baseUri);
        platformApi.SetDeviceCredential(login.DeviceId, login.DeviceCredential);

        var mainWindow = new MainWindow(api, platformApi, login.SignedInUser);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }
}
