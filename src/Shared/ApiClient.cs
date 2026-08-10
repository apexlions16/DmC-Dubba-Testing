using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DmC.Qa.Shared;

public sealed class QaApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public QaApiClient(HttpClient http)
    {
        _http = http;
    }

    public void SetDevelopmentUser(string userId)
    {
        _http.DefaultRequestHeaders.Remove("X-User-Id");
        _http.DefaultRequestHeaders.Add("X-User-Id", userId);
    }

    public void SetDeviceCredential(string deviceId, string credential)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Device", $"{deviceId}.{credential}");
        _http.DefaultRequestHeaders.Remove("X-User-Id");
    }

    public void ClearAuthentication()
    {
        _http.DefaultRequestHeaders.Authorization = null;
        _http.DefaultRequestHeaders.Remove("X-User-Id");
    }

    public async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(
        string displayName,
        string installationId,
        string? deviceName,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "auth/enroll",
            new
            {
                display_name = displayName,
                installation_id = installationId,
                device_name = deviceName
            },
            _json,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DeviceEnrollmentResponse>(_json, cancellationToken)
               ?? throw new QaApiException("Cihaz eşleştirme yanıtı okunamadı.");
    }

    public async Task<CurrentUserSummary> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("me", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CurrentUserSummary>(_json, cancellationToken)
               ?? throw new QaApiException("Kullanıcı oturumu doğrulanamadı.");
    }

    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("projects", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ProjectSummary>>(_json, cancellationToken)
               ?? [];
    }

    public async Task<IReadOnlyList<TaskSummary>> GetMyTasksAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("tasks/mine", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<TaskSummary>>(_json, cancellationToken)
               ?? [];
    }

    public async Task<BuildSummary?> GetCurrentBuildAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"projects/{projectId}/builds/current", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<BuildSummary>(_json, cancellationToken);
    }

    public async Task<IssueProgress?> GetProjectProgressAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"projects/{projectId}/analytics/summary", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<IssueProgress>(_json, cancellationToken);
    }

    public async Task<BugCreatedResponse> SubmitBugAsync(
        object payload,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("bugs", payload, _json, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<BugCreatedResponse>(_json, cancellationToken)
               ?? throw new QaApiException("Hata raporu yanıtı okunamadı.");
    }

    public Task<EvidenceUploadResponse> UploadBugEvidenceAsync(
        string bugId,
        string filePath,
        CancellationToken cancellationToken = default)
        => UploadEvidenceAsync($"bugs/{bugId}/evidence", filePath, cancellationToken);

    public Task<EvidenceUploadResponse> UploadRetestEvidenceAsync(
        string retestRequestId,
        string filePath,
        CancellationToken cancellationToken = default)
        => UploadEvidenceAsync($"retests/{retestRequestId}/evidence", filePath, cancellationToken);

    private async Task<EvidenceUploadResponse> UploadEvidenceAsync(
        string endpoint,
        string filePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new QaApiException("Yüklenecek video veya kanıt dosyası bulunamadı.");
        }

        await using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeForFile(filePath));

        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        using var response = await _http.PostAsync(endpoint, form, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<EvidenceUploadResponse>(_json, cancellationToken)
               ?? throw new QaApiException("Video yükleme yanıtı okunamadı.");
    }

    private static string MediaTypeForFile(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".avi" => "video/x-msvideo",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
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
                var detailText = detail.ValueKind == JsonValueKind.String
                    ? detail.GetString()
                    : detail.ToString();
                throw new QaApiException(string.IsNullOrWhiteSpace(detailText) ? fallback : detailText!);
            }
        }
        catch (JsonException)
        {
        }

        throw new QaApiException(fallback);
    }
}

public sealed class QaApiException : Exception
{
    public QaApiException(string message) : base(message)
    {
    }
}
