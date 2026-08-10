using System.Windows;

namespace DmC.Qa.Admin;

public partial class MainWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        AttachButton("Kalıcı Silme Talebi Oluştur", (_, _) => OpenPurgeWindow());
    }

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
