using System.IO;
using System.Text.Json;
using System.Windows;

namespace DmC.Qa.Tester;

public partial class MainWindow : Window
{
    private static readonly string StateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameQaClient");

    private static readonly string StateFile = Path.Combine(StateDirectory, "client.json");

    public MainWindow()
    {
        InitializeComponent();
        LoadLocalProfile();
    }

    private void LoadLocalProfile()
    {
        if (!File.Exists(StateFile))
        {
            EnrollmentOverlay.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var state = JsonSerializer.Deserialize<ClientState>(File.ReadAllText(StateFile));
            if (state is null || string.IsNullOrWhiteSpace(state.DisplayName))
            {
                EnrollmentOverlay.Visibility = Visibility.Visible;
                return;
            }

            TesterNameText.Text = state.DisplayName;
            EnrollmentOverlay.Visibility = Visibility.Collapsed;
        }
        catch
        {
            EnrollmentOverlay.Visibility = Visibility.Visible;
        }
    }

    private void EnrollButton_Click(object sender, RoutedEventArgs e)
    {
        var displayName = DisplayNameInput.Text.Trim();
        if (displayName.Length < 2)
        {
            EnrollmentError.Text = "Lütfen ekipte kullanılan adınızı yazın.";
            return;
        }

        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(
            StateFile,
            JsonSerializer.Serialize(new ClientState(displayName), new JsonSerializerOptions { WriteIndented = true }));

        TesterNameText.Text = displayName;
        EnrollmentError.Text = string.Empty;
        EnrollmentOverlay.Visibility = Visibility.Collapsed;

        // Next implementation step: exchange the name + device fingerprint with the backend
        // for a device-bound credential, then load project/task/build/retest data.
    }

    private sealed record ClientState(string DisplayName);
}
