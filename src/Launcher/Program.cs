using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;

namespace DmC.Qa.Launcher;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    [STAThread]
    public static async Task Main()
    {
        var root = AppContext.BaseDirectory;
        var settings = LoadSettings(root);
        var versionsDir = Path.Combine(root, "versions");
        Directory.CreateDirectory(versionsDir);

        var currentStatePath = Path.Combine(root, "current.json");
        var current = ReadJson<CurrentState>(currentStatePath);

        try
        {
            using var http = new HttpClient
            {
                BaseAddress = new Uri(settings.ApiBaseUrl),
                Timeout = TimeSpan.FromMinutes(30)
            };

            var latest = await http.GetFromJsonAsync<ReleaseManifest>(
                $"client/releases/latest?channel={Uri.EscapeDataString(settings.Channel)}",
                Json);

            if (latest is not null && (current is null || !string.Equals(current.Version, latest.Version, StringComparison.OrdinalIgnoreCase)))
            {
                current = await DownloadAndActivateAsync(http, root, versionsDir, settings, latest);
                File.WriteAllText(currentStatePath, JsonSerializer.Serialize(current, Json));
                File.WriteAllText(
                    Path.Combine(root, "pending-release-notes.json"),
                    JsonSerializer.Serialize(latest, Json));
            }
        }
        catch (Exception ex)
        {
            // Network/update failure must not block QA if a known-good payload already exists.
            WriteLog(root, $"Update check failed: {ex}");
            if (current is null)
            {
                MessageBox.Show(
                    "QA Client ilk kurulumu tamamlanamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.",
                    "Game QA",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
        }

        if (current is null)
        {
            MessageBox.Show("Çalıştırılabilir QA Client sürümü bulunamadı.", "Game QA", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var payloadPath = Path.Combine(versionsDir, current.Version, settings.PayloadExe);
        if (!File.Exists(payloadPath))
        {
            MessageBox.Show(
                $"QA Client dosyası bulunamadı: {settings.PayloadExe}",
                "Game QA",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = payloadPath,
            WorkingDirectory = Path.GetDirectoryName(payloadPath)!,
            UseShellExecute = true
        });
    }

    private static async Task<CurrentState> DownloadAndActivateAsync(
        HttpClient http,
        string root,
        string versionsDir,
        LauncherSettings settings,
        ReleaseManifest release)
    {
        var stagingRoot = Path.Combine(root, ".update-staging");
        Directory.CreateDirectory(stagingRoot);

        var zipPath = Path.Combine(stagingRoot, $"{release.Version}.zip");
        var extractPath = Path.Combine(stagingRoot, release.Version);
        var finalPath = Path.Combine(versionsDir, release.Version);

        if (File.Exists(zipPath)) File.Delete(zipPath);
        if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
        Directory.CreateDirectory(extractPath);

        using (var response = await http.GetAsync(release.ArtifactUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(zipPath);
            await input.CopyToAsync(output);
        }

        var actualSha = await ComputeSha256Async(zipPath);
        if (!actualSha.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Downloaded update SHA-256 does not match release manifest.");
        }

        ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);
        var expectedExe = Path.Combine(extractPath, settings.PayloadExe);
        if (!File.Exists(expectedExe))
        {
            throw new InvalidDataException($"Update does not contain {settings.PayloadExe}.");
        }

        if (Directory.Exists(finalPath))
        {
            Directory.Delete(finalPath, true);
        }
        Directory.Move(extractPath, finalPath);
        File.Delete(zipPath);

        return new CurrentState(release.Version, DateTimeOffset.UtcNow);
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static LauncherSettings LoadSettings(string root)
    {
        var path = Path.Combine(root, "launcher.settings.json");
        var fileSettings = ReadJson<LauncherSettings>(path);
        if (fileSettings is not null) return fileSettings;

        var baseUrl = Environment.GetEnvironmentVariable("GAME_QA_API") ?? "http://localhost:7860/";
        return new LauncherSettings(baseUrl, "stable", "DmC.Qa.Tester.exe");
    }

    private static T? ReadJson<T>(string path)
    {
        if (!File.Exists(path)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
        }
        catch
        {
            return default;
        }
    }

    private static void WriteLog(string root, string text)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(root, "launcher.log"),
                $"[{DateTimeOffset.Now:O}] {text}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never stop launch fallback.
        }
    }

    private sealed record LauncherSettings(string ApiBaseUrl, string Channel, string PayloadExe);
    private sealed record CurrentState(string Version, DateTimeOffset ActivatedAt);
    private sealed record ReleaseManifest(
        string Channel,
        string Version,
        string Title,
        string Notes,
        string ArtifactUrl,
        string Sha256,
        string? MinimumVersion,
        bool Mandatory);
}
