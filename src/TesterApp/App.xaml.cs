using System.IO;
using System.Windows;
using System.Windows.Threading;
using DmC.Qa.Shared;

namespace DmC.Qa.Tester;

public partial class App : Application
{
    private static readonly string CrashDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameQaClient");
    private static readonly string CrashLogPath = Path.Combine(CrashDirectory, "crash.log");

    public App()
    {
        TurkishUi.ApplyCulture();
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

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            WriteCrashLog("Başlangıç", ex);
            MessageBox.Show(
                $"Test uygulaması başlatılırken beklenmeyen bir hata oluştu.\n\n{ex.Message}\n\nAyrıntılı kayıt:\n{CrashLogPath}",
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
                "/DmC.Qa.Tester;component/Themes/DarkComboBox.xaml",
                UriKind.Relative)
        });
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("Arayüz", e.Exception);
        MessageBox.Show(
            $"Uygulamada beklenmeyen bir hata oluştu. Oturum kapatılmadan önce hata kaydı oluşturuldu.\n\n{e.Exception.Message}\n\nKayıt:\n{CrashLogPath}",
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
            // Crash logger uygulamanın kendisini ikinci kez düşürmemeli.
        }
    }
}
