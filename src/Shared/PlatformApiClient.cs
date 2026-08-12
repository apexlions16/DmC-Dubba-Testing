using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace DmC.Qa.Shared;

public sealed class PlatformApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public PlatformApiClient(Uri baseAddress)
    {
        _http = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromHours(2)
        };
    }

    public Uri BaseAddress => _http.BaseAddress!;
    public string? DeviceAuthorization { get; private set; }

    public void SetDeviceCredential(string deviceId, string credential)
    {
        DeviceAuthorization = $"Device {deviceId}.{credential}";
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Device", $"{deviceId}.{credential}");
    }

    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default)
        => await GetListAsync<ProjectSummary>("projects", cancellationToken);

    public async Task<CreateResult> CreateProjectAsync(
        string key,
        string name,
        string sectionLabel,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "projects",
            new { key, name, section_label = sectionLabel },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return new CreateResult(document.RootElement.GetProperty("id").GetString()!);
    }

    public Task<IReadOnlyList<ProjectMemberItem>> GetProjectMembersAsync(
        string projectId,
        CancellationToken cancellationToken = default)
        => GetListAsync<ProjectMemberItem>($"admin/projects/{projectId}/members", cancellationToken);

    public async Task ReplaceProjectMembersAsync(
        string projectId,
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PutAsJsonAsync(
            $"admin/projects/{projectId}/members",
            new { user_ids = userIds.Distinct().ToArray() },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<SectionItem>> GetSectionsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
        => GetListAsync<SectionItem>($"admin/projects/{projectId}/sections", cancellationToken);

    public async Task<SectionItem> CreateSectionAsync(
        string projectId,
        string name,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            $"admin/projects/{projectId}/sections",
            new { name, sort_order = sortOrder },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SectionItem>(_json, cancellationToken)
               ?? throw new QaApiException("Bölüm oluşturma yanıtı okunamadı.");
    }

    public Task<IReadOnlyList<MyBugItem>> GetMyBugsAsync(
        string? projectId = null,
        CancellationToken cancellationToken = default)
        => GetListAsync<MyBugItem>(
            string.IsNullOrWhiteSpace(projectId) ? "bugs/mine" : $"bugs/mine?project_id={Uri.EscapeDataString(projectId)}",
            cancellationToken);

    public Task<IReadOnlyList<AdminBugListItem>> GetAdminBugsAsync(
        string projectId,
        string? status = null,
        string? bugType = null,
        string? reporterId = null,
        string? buildId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string?>
        {
            ["status"] = status,
            ["bug_type"] = bugType,
            ["reporter_id"] = reporterId,
            ["build_id"] = buildId,
            ["search"] = search
        };
        var query = string.Join("&", values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value!)}"));
        var path = $"admin/projects/{projectId}/bugs" + (query.Length == 0 ? string.Empty : $"?{query}");
        return GetListAsync<AdminBugListItem>(path, cancellationToken);
    }

    public async Task<AdminBugDetail> GetBugDetailAsync(
        string bugId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"admin/bugs/{bugId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminBugDetail>(_json, cancellationToken)
               ?? throw new QaApiException("Hata detayı okunamadı.");
    }

    public async Task ChangeBugStatusAsync(
        string bugId,
        string status,
        string? note,
        string? rootCause,
        string? buildId = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            $"bugs/{bugId}/status",
            new { status, note, root_cause = rootCause, build_id = buildId },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<RetestCreateResult> RequestRetestAsync(
        string bugId,
        string buildId,
        IEnumerable<string> assigneeUserIds,
        string note,
        DateTimeOffset? deadlineAt,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            $"bugs/{bugId}/retests",
            new
            {
                build_id = buildId,
                assignee_user_ids = assigneeUserIds.Distinct().ToArray(),
                note,
                deadline_at = deadlineAt
            },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<RetestCreateResult>(_json, cancellationToken)
               ?? throw new QaApiException("Yeniden test talebi oluşturulamadı.");
    }

    public Task<IReadOnlyList<AdminBuildItem>> GetBuildsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
        => GetListAsync<AdminBuildItem>($"admin/projects/{projectId}/builds", cancellationToken);

    public async Task<BuildCreateResult> CreateBuildAsync(
        string projectId,
        string version,
        string title,
        string description,
        string installationInstructions,
        string changelog,
        IEnumerable<string>? fixCandidateBugIds = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            $"projects/{projectId}/builds",
            new
            {
                version,
                title,
                description,
                installation_instructions = installationInstructions,
                changelog,
                fix_candidate_bug_ids = fixCandidateBugIds?.Distinct().ToArray() ?? []
            },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<BuildCreateResult>(_json, cancellationToken)
               ?? throw new QaApiException("Test sürümü oluşturulamadı.");
    }

    public async Task UploadBuildAsync(
        string projectId,
        string buildId,
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new QaApiException("Yüklenecek test sürümü dosyası bulunamadı.");
        }

        await using var stream = File.OpenRead(filePath);
        using var content = new ProgressStreamContent(stream, progress, cancellationToken);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var form = new MultipartFormDataContent();
        form.Add(content, "file", Path.GetFileName(filePath));
        using var response = await _http.PostAsync(
            $"projects/{projectId}/builds/{buildId}/file",
            form,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task PublishBuildAsync(
        string projectId,
        string buildId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync(
            $"projects/{projectId}/builds/{buildId}/publish",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ArchiveBuildAsync(
        string projectId,
        string buildId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync(
            $"projects/{projectId}/builds/{buildId}/archive",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DownloadBuildAsync(
        string projectId,
        string buildId,
        string destinationPath,
        string? expectedSha256,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            $"projects/{projectId}/builds/{buildId}/download",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var target = File.Create(destinationPath);
        using var sha = SHA256.Create();
        var buffer = new byte[1024 * 1024];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sha.TransformBlock(buffer, 0, read, null, 0);
            copied += read;
            if (total is > 0)
            {
                progress?.Report(Math.Min(100, copied * 100d / total.Value));
            }
        }
        sha.TransformFinalBlock([], 0, 0);
        var actual = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(expectedSha256)
            && !string.Equals(actual, expectedSha256.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            target.Close();
            File.Delete(destinationPath);
            throw new QaApiException("İndirilen test sürümünün SHA-256 doğrulaması başarısız oldu.");
        }
        progress?.Report(100);
        await RecordBuildEventAsync(projectId, buildId, "download_completed", cancellationToken);
    }

    public async Task RecordBuildEventAsync(
        string projectId,
        string buildId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync(
            $"projects/{projectId}/builds/{buildId}/events/{eventType}",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<AdminTaskItem>> GetTasksAsync(
        string projectId,
        CancellationToken cancellationToken = default)
        => GetListAsync<AdminTaskItem>($"admin/projects/{projectId}/tasks", cancellationToken);

    public async Task<CreateResult> CreateTaskAsync(
        string projectId,
        string title,
        string description,
        string? sectionId,
        string? requiredBuildId,
        DateTimeOffset? deadlineAt,
        IEnumerable<string> assigneeIds,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            $"projects/{projectId}/tasks",
            new
            {
                title,
                description,
                section_id = sectionId,
                required_build_id = requiredBuildId,
                deadline_at = deadlineAt,
                assignee_user_ids = assigneeIds.Distinct().ToArray()
            },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return new CreateResult(document.RootElement.GetProperty("id").GetString()!);
    }

    public async Task SetTaskStatusAsync(
        string taskId,
        string status,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PutAsJsonAsync(
            $"admin/tasks/{taskId}/status",
            new { status },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<InboxNotificationItem>> GetMyNotificationsAsync(CancellationToken cancellationToken = default)
        => GetListAsync<InboxNotificationItem>("notifications/mine", cancellationToken);

    public async Task MarkNotificationReadAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync($"notifications/{id}/read", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task AcknowledgeNotificationAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync($"notifications/{id}/acknowledge", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<AdminNotificationItem>> GetAdminNotificationsAsync(
        string? projectId,
        CancellationToken cancellationToken = default)
        => GetListAsync<AdminNotificationItem>(
            string.IsNullOrWhiteSpace(projectId)
                ? "admin/notifications"
                : $"admin/notifications?project_id={Uri.EscapeDataString(projectId)}",
            cancellationToken);

    public async Task<CreateResult> CreateNotificationAsync(
        string? projectId,
        string title,
        string body,
        string severity,
        string targetType,
        IEnumerable<string>? userIds = null,
        string? taskId = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "admin/notifications",
            new
            {
                project_id = projectId,
                title,
                body,
                severity,
                target_type = targetType,
                user_ids = userIds?.Distinct().ToArray() ?? [],
                task_id = taskId,
                expires_at = expiresAt
            },
            _json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return new CreateResult(document.RootElement.GetProperty("id").GetString()!);
    }

    public Task<IReadOnlyList<AnalyticsBucket>> GetAnalyticsAsync(
        string projectId,
        string dimension,
        CancellationToken cancellationToken = default)
        => GetListAsync<AnalyticsBucket>(
            $"admin/projects/{projectId}/analytics/drilldown?dimension={Uri.EscapeDataString(dimension)}",
            cancellationToken);

    public async Task DownloadEvidenceAsync(
        string assetId,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            $"evidence/{assetId}/download",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destinationPath);
        await input.CopyToAsync(output, cancellationToken);
    }

    public async Task<StorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("admin/storage/info", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<StorageInfo>(_json, cancellationToken)
               ?? throw new QaApiException("Depolama bilgisi okunamadı.");
    }

    public Uri NotificationsWebSocketUri()
    {
        var builder = new UriBuilder(BaseAddress)
        {
            Scheme = BaseAddress.Scheme == "https" ? "wss" : "ws",
            Path = "ws/notifications",
            Query = string.Empty
        };
        return builder.Uri;
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<T>>(_json, cancellationToken) ?? [];
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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

    private sealed class ProgressStreamContent : HttpContent
    {
        private readonly Stream _source;
        private readonly IProgress<double>? _progress;
        private readonly CancellationToken _cancellationToken;

        public ProgressStreamContent(Stream source, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            _source = source;
            _progress = progress;
            _cancellationToken = cancellationToken;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var total = _source.Length;
            var buffer = new byte[1024 * 1024];
            long copied = 0;
            int read;
            while ((read = await _source.ReadAsync(buffer, _cancellationToken)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read), _cancellationToken);
                copied += read;
                _progress?.Report(total == 0 ? 100 : Math.Min(100, copied * 100d / total));
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _source.Length;
            return true;
        }
    }
}
