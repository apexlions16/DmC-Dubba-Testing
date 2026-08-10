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

        var api = new QaApiClient(new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
            Timeout = TimeSpan.FromMinutes(30)
        });

        var login = new AdminLoginWindow(api);
        if (login.ShowDialog() != true || login.SignedInUser is null)
        {
            Shutdown();
            return;
        }

        var mainWindow = new MainWindow(api, login.SignedInUser);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }
}
