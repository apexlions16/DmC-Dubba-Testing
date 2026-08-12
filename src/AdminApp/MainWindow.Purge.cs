using System.Windows;

namespace DmC.Qa.Admin;

public partial class MainWindow
{
    private void OpenPurgeWindow()
    {
        if (!IsAdminRole(_signedInUser.Role))
        {
            MessageBox.Show(
                "Kalıcı silme işlemleri yalnızca yöneticilere açıktır.",
                "Yetki Gerekli",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var window = new PurgeWindow(_platformApi, _currentProject)
        {
            Owner = this
        };
        window.ShowDialog();
        if (window.ProjectPurged)
        {
            _ = LoadProjectsAsync();
        }
    }
}
