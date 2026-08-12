using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using DmC.Qa.Shared;

namespace DmC.Qa.Admin;

public partial class App : Application
{
    private static readonly string CrashDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameQaAdmin");
    private static readonly string CrashLogPath = Path.Combine(CrashDirectory, "crash.log");

    public App()
    {
        TurkishUi.ApplyCulture();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            LoadRuntimeTheme();

            var baseUri = ApiEndpointResolver.Resolve();

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
        catch (Exception ex)
        {
            WriteCrashLog("Başlangıç", ex);
            MessageBox.Show(
                $"Yönetim Merkezi başlatılırken beklenmeyen bir hata oluştu.\n\n{ex.Message}\n\nAyrıntılı kayıt:\n{CrashLogPath}",
                "DmC QA • Başlatma Hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void LoadRuntimeTheme()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "/DmC.Qa.Admin;component/Themes/DarkComboBox.xaml",
                UriKind.Relative)
        });
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "/DmC.Qa.Admin;component/Themes/DarkInputs.xaml",
                UriKind.Relative)
        });
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("Arayüz", e.Exception);
        MessageBox.Show(
            $"Yönetim Merkezi'nde beklenmeyen bir hata oluştu. Hata kaydı oluşturuldu.\n\n{e.Exception.Message}\n\nKayıt:\n{CrashLogPath}",
            "DmC QA • Beklenmeyen Hata",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            WriteCrashLog("Çalışma zamanı", exception);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog("Arka plan görevi", e.Exception);
        e.SetObserved();
    }

    private static void WriteCrashLog(string source, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(CrashDirectory);
            File.AppendAllText(
                CrashLogPath,
                $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{new string('-', 90)}{Environment.NewLine}");
        }
        catch
        {
            // Hata kaydı mekanizması ikinci bir hataya neden olmamalı.
        }
    }
}
