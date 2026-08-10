using System.IO;
using System.Text.Json;
using System.Windows;

namespace DmC.Qa.Tester;

public partial class MainWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        _buildDownloadButton ??= BuildDownloadButton;
        UpdateActionAvailability();
        UiMotion.FadeIn(DashboardContent, 18);
        ShowPendingReleaseNotes();
    }

    private void ShowPendingReleaseNotes()
    {
        try
        {
            var launcherRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
            var path = Path.Combine(launcherRoot, "pending-release-notes.json");
            if (!File.Exists(path))
            {
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var version = root.TryGetProperty("version", out var versionNode) ? versionNode.GetString() : null;
            var title = root.TryGetProperty("title", out var titleNode) ? titleNode.GetString() : null;
            var notes = root.TryGetProperty("notes", out var notesNode) ? notesNode.GetString() : null;
            var channel = root.TryGetProperty("channel", out var channelNode) ? channelNode.GetString() : null;
            var mandatory = root.TryGetProperty("mandatory", out var mandatoryNode) && mandatoryNode.ValueKind == JsonValueKind.True;

            MessageBox.Show(
                $"{title ?? "Yeni QA İstemci Sürümü"}\n\n" +
                $"Sürüm: {version ?? "—"}\nKanal: {(channel == "beta" ? "Beta" : "Kararlı")}\n" +
                $"{(mandatory ? "Bu sürüm zorunlu güncelleme olarak yayınlandı.\n" : string.Empty)}\n" +
                $"Güncelleme Notları:\n{notes ?? "Bu sürüm için not girilmedi."}",
                "QA İstemcisi Güncellendi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            File.Delete(path);
        }
        catch
        {
            // Güncelleme notlarının okunamaması istemcinin çalışmasını engellemez.
        }
    }
}
