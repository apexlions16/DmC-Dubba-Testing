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

    public Uri BaseAddress => _http.BaseAddress
                              ?? throw new InvalidOperationException("API adresi ayarlanmamış.");

    public string? DeviceAuthorization { get; private set; }

    public void SetDevelopmentUser(string userId)
    {
        _http.DefaultRequestHeaders.Remove("X-User-Id");
        _http.DefaultRequestHeaders.Add("X-User-Id", userId);
    }

    public void SetDeviceCredential(string deviceId, string credential)
    {
        DeviceAuthorization = $"Device {deviceId}.{credential}";
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Device", $"{deviceId}.{credential}");
        _http.DefaultRequestHeaders.Remove("X-User-Id");
    }

    public void ClearAuthentication()
    {
        DeviceAuthorization = null;
        _http.DefaultRequestHeaders.Authorization = null;
        _http.DefaultRequestHeaders.Remove("X-User-Id");
    }

    public async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(
        string displayName,
        string installationId,
        string? deviceName,
        string clientKind = "tester",
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "auth/enroll",
            new
            {
                display_name = displayName,
                installation_id = installationId,
                device_name = deviceName,
                client_kind = clientKind
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

    public async Task<IReadOnlyList<AdminUserSummary>> GetAdminUsersAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("admin/users", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<AdminUserSummary>>(_json, cancellationToken) ?? [];
    }

    public async Task<AdminUserSummary> CreateAdminUserAsync(
        string displayName,
        string role,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "admin/users",
            new { display_name = displayName, role },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var created = await response.Content.ReadFromJsonAsync<AdminUserSummary>(_json, cancellationToken);
        if (created is not null)
        {
            return created;
        }

        var users = await GetAdminUsersAsync(cancellationToken);
        return users.FirstOrDefault(user =>
                   string.Equals(user.DisplayName, displayName, StringComparison.CurrentCultureIgnoreCase))
               ?? throw new QaApiException("Oluşturulan kullanıcı sunucudan okunamadı.");
    }

    public async Task SetUserEnabledAsync(
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PutAsJsonAsync(
            $"admin/users/{userId}",
            new { enabled },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<int> ResetUserDevicesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync($"admin/users/{userId}/devices/reset", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.TryGetProperty("revoked_device_count", out var count)
            ? count.GetInt32()
            : 0;
    }

    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("projects", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ProjectSummary>>(_json, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<TaskSummary>> GetMyTasksAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("tasks/mine", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<TaskSummary>>(_json, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<RetestAssignment>> GetMyRetestsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("retests/mine", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<RetestAssignment>>(_json, cancellationToken) ?? [];
    }

    public async Task<RetestSubmitResponse> SubmitRetestAsync(
        string retestRequestId,
        string testedBuildId,
        RetestResult result,
        string comment,
        CancellationToken cancellationToken = default)
    {
        var technicalResult = result switch
        {
            RetestResult.Passed => "passed",
            RetestResult.Failed => "failed",
            _ => "uncertain"
        };
        using var response = await _http.PostAsJsonAsync(
            $"retests/{retestRequestId}/submit",
            new { result = technicalResult, tested_build_id = testedBuildId, comment },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<RetestSubmitResponse>(_json, cancellationToken)
               ?? throw new QaApiException("Yeniden test sonucu kaydedilemedi.");
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
        => UploadEvidenceAsync(
            directInitEndpoint: $"bugs/{bugId}/evidence/init",
            directFinalizeEndpoint: $"bugs/{bugId}/evidence/finalize",
            localMultipartEndpoint: $"bugs/{bugId}/evidence",
            filePath,
            cancellationToken);

    public Task<EvidenceUploadResponse> UploadRetestEvidenceAsync(
        string retestRequestId,
        string filePath,
        CancellationToken cancellationToken = default)
        => UploadEvidenceAsync(
            directInitEndpoint: $"retests/{retestRequestId}/evidence/init",
            directFinalizeEndpoint: $"retests/{retestRequestId}/evidence/finalize",
            localMultipartEndpoint: $"retests/{retestRequestId}/evidence",
            filePath,
            cancellationToken);

    private async Task<EvidenceUploadResponse> UploadEvidenceAsync(
        string directInitEndpoint,
        string directFinalizeEndpoint,
        string localMultipartEndpoint,
        string filePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new QaApiException("Yüklenecek video veya kanıt dosyası bulunamadı.");
        }

        var mediaType = DirectFileTransfer.MediaTypeForFile(filePath);
        if (DirectFileTransfer.TryResolveFilesBase(BaseAddress, out var filesBase))
        {
            var sha256 = await DirectFileTransfer.ComputeSha256Async(filePath, cancellationToken);
            var init = await DirectFileTransfer.InitializeAsync(
                filesBase,
                directInitEndpoint,
                DeviceAuthorization,
                filePath,
                mediaType,
                sha256,
                cancellationToken);
            await DirectFileTransfer.PutFileAsync(
                init.UploadUri,
                filePath,
                mediaType,
                cancellationToken: cancellationToken);
            return await DirectFileTransfer.FinalizeAsync<EvidenceUploadResponse>(
                filesBase,
                directFinalizeEndpoint,
                DeviceAuthorization,
                init,
                filePath,
                mediaType,
                sha256,
                cancellationToken);
        }

        await using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", Path.GetFileName(filePath));
        using var response = await _http.PostAsync(localMultipartEndpoint, form, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<EvidenceUploadResponse>(_json, cancellationToken)
               ?? throw new QaApiException("Video yükleme yanıtı okunamadı.");
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
