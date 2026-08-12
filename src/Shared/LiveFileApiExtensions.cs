using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DmC.Qa.Shared;

public static class LiveFileApiExtensions
{
    public static async Task UploadBuildForCurrentBackendAsync(
        this PlatformApiClient client,
        string projectId,
        string buildId,
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!DirectFileTransfer.TryResolveFilesBase(client.BaseAddress, out var filesBase))
        {
            await client.UploadBuildAsync(projectId, buildId, filePath, progress, cancellationToken);
            return;
        }

        if (!File.Exists(filePath))
        {
            throw new QaApiException("Yüklenecek test sürümü dosyası bulunamadı.");
        }

        var mediaType = DirectFileTransfer.MediaTypeForFile(filePath);
        var sha256 = await DirectFileTransfer.ComputeSha256Async(filePath, cancellationToken);
        var init = await DirectFileTransfer.InitializeAsync(
            filesBase,
            $"projects/{projectId}/builds/{buildId}/file/init",
            client.DeviceAuthorization,
            filePath,
            mediaType,
            sha256,
            cancellationToken);
        await DirectFileTransfer.PutFileAsync(
            init.UploadUri,
            filePath,
            mediaType,
            progress,
            cancellationToken);
        await DirectFileTransfer.FinalizeAsync<DirectUploadFinalizeResult>(
            filesBase,
            $"projects/{projectId}/builds/{buildId}/file/finalize",
            client.DeviceAuthorization,
            init,
            filePath,
            mediaType,
            sha256,
            cancellationToken);
    }

    public static async Task DownloadBuildForCurrentBackendAsync(
        this PlatformApiClient client,
        string projectId,
        string buildId,
        string destinationPath,
        string? expectedSha256,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!DirectFileTransfer.TryResolveFilesBase(client.BaseAddress, out var filesBase))
        {
            await client.DownloadBuildAsync(
                projectId,
                buildId,
                destinationPath,
                expectedSha256,
                progress,
                cancellationToken);
            return;
        }

        await DirectFileTransfer.DownloadAsync(
            filesBase,
            $"projects/{projectId}/builds/{buildId}/download",
            client.DeviceAuthorization,
            destinationPath,
            expectedSha256,
            progress,
            cancellationToken);
        await client.RecordBuildEventAsync(projectId, buildId, "download_completed", cancellationToken);
    }

    public static async Task DownloadEvidenceForCurrentBackendAsync(
        this PlatformApiClient client,
        string assetId,
        string destinationPath,
        string? expectedSha256 = null,
        CancellationToken cancellationToken = default)
    {
        if (!DirectFileTransfer.TryResolveFilesBase(client.BaseAddress, out var filesBase))
        {
            await client.DownloadEvidenceAsync(assetId, destinationPath, cancellationToken);
            return;
        }

        await DirectFileTransfer.DownloadAsync(
            filesBase,
            $"evidence/{assetId}/download",
            client.DeviceAuthorization,
            destinationPath,
            expectedSha256,
            cancellationToken: cancellationToken);
    }

    public static async Task DeleteEvidenceForCurrentBackendAsync(
        this PlatformApiClient client,
        string assetId,
        CancellationToken cancellationToken = default)
    {
        if (!DirectFileTransfer.TryResolveFilesBase(client.BaseAddress, out var filesBase))
        {
            throw new QaApiException("Kanıtı kalıcı silme işlemi yalnızca canlı Supabase + Hugging Face backend'inde kullanılabilir.");
        }

        using var http = CreateLiveHttp(filesBase, client.DeviceAuthorization, TimeSpan.FromMinutes(5));
        using var response = await http.DeleteAsync($"evidence/{assetId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public static async Task DeleteBugForCurrentBackendAsync(
        this PlatformApiClient client,
        string bugId,
        CancellationToken cancellationToken = default)
    {
        if (!DirectFileTransfer.TryResolveFilesBase(client.BaseAddress, out var filesBase))
        {
            throw new QaApiException("Hata raporunu kalıcı silme işlemi yalnızca canlı Supabase + Hugging Face backend'inde kullanılabilir.");
        }

        using var http = CreateLiveHttp(filesBase, client.DeviceAuthorization, TimeSpan.FromMinutes(10));
        using var response = await http.DeleteAsync($"bugs/{bugId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public static async Task DeleteProjectForCurrentBackendAsync(
        this PlatformApiClient client,
        string projectId,
        string exactProjectName,
        CancellationToken cancellationToken = default)
    {
        if (!DirectFileTransfer.TryResolveFilesBase(client.BaseAddress, out var filesBase))
        {
            throw new QaApiException("Projeyi kalıcı silme işlemi yalnızca canlı Supabase + Hugging Face backend'inde kullanılabilir.");
        }

        using var http = CreateLiveHttp(filesBase, client.DeviceAuthorization, TimeSpan.FromMinutes(30));
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"projects/{projectId}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { project_name = exactProjectName }),
                Encoding.UTF8,
                "application/json")
        };
        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public static async Task ArchiveBuildForCurrentBackendAsync(
        this PlatformApiClient client,
        string projectId,
        string buildId,
        CancellationToken cancellationToken = default)
    {
        if (!DirectFileTransfer.TryResolveFilesBase(client.BaseAddress, out var filesBase))
        {
            await client.ArchiveBuildAsync(projectId, buildId, cancellationToken);
            return;
        }

        using var http = CreateLiveHttp(filesBase, client.DeviceAuthorization, TimeSpan.FromMinutes(10));
        using var response = await http.PostAsync(
            $"projects/{projectId}/builds/{buildId}/archive",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static HttpClient CreateLiveHttp(Uri baseAddress, string? authorization, TimeSpan timeout)
    {
        var http = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = timeout
        };
        ApplyAuthorization(http, authorization);
        return http;
    }

    private static void ApplyAuthorization(HttpClient http, string? authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return;
        }
        var split = authorization.Split(' ', 2);
        if (split.Length == 2)
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(split[0], split[1]);
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        var fallback = $"Sunucu isteği başarısız oldu ({(int)response.StatusCode}).";
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                var text = detail.ValueKind == JsonValueKind.String ? detail.GetString() : detail.ToString();
                throw new QaApiException(string.IsNullOrWhiteSpace(text) ? fallback : text!);
            }
        }
        catch (JsonException)
        {
        }
        throw new QaApiException(fallback);
    }
}
