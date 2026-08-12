using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DmC.Qa.Shared;

public static class PurgeApiExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static async Task<PurgePreview> GetPurgePreviewAsync(this PlatformApiClient client, string projectId, CancellationToken cancellationToken = default)
    {
        using var http = CreateHttp(client);
        using var response = await http.GetAsync($"admin/projects/{projectId}/purge-preview", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PurgePreview>(Json, cancellationToken)
               ?? throw new QaApiException("Kalıcı silme önizlemesi okunamadı.");
    }

    public static async Task<PurgeRequestCreated> CreatePurgeRequestAsync(
        this PlatformApiClient client,
        string projectId,
        string projectName,
        bool deleteStorage,
        bool deleteDatabase,
        bool deleteAuditTombstone,
        CancellationToken cancellationToken = default)
    {
        using var http = CreateHttp(client);
        using var response = await http.PostAsJsonAsync(
            $"admin/projects/{projectId}/purge-requests",
            new { project_name = projectName, delete_storage = deleteStorage, delete_database = deleteDatabase, delete_audit_tombstone = deleteAuditTombstone },
            Json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PurgeRequestCreated>(Json, cancellationToken)
               ?? throw new QaApiException("Kalıcı silme talebi yanıtı okunamadı.");
    }

    public static async Task<IReadOnlyList<PurgeRequestItem>> GetPurgeRequestsAsync(this PlatformApiClient client, CancellationToken cancellationToken = default)
    {
        using var http = CreateHttp(client);
        using var response = await http.GetAsync("admin/purge-requests", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<PurgeRequestItem>>(Json, cancellationToken) ?? [];
    }

    public static async Task ApprovePurgeRequestAsync(
        this PlatformApiClient client,
        string requestId,
        string projectName,
        string confirmationCode,
        CancellationToken cancellationToken = default)
    {
        using var http = CreateHttp(client);
        using var response = await http.PostAsJsonAsync(
            $"admin/purge-requests/{requestId}/approve",
            new { project_name = projectName, confirmation_code = confirmationCode },
            Json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static HttpClient CreateHttp(PlatformApiClient client)
    {
        var http = new HttpClient { BaseAddress = client.BaseAddress, Timeout = TimeSpan.FromHours(2) };
        if (!string.IsNullOrWhiteSpace(client.DeviceAuthorization))
        {
            var split = client.DeviceAuthorization!.Split(' ', 2);
            if (split.Length == 2) http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(split[0], split[1]);
        }
        return http;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
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
        catch (JsonException) { }
        throw new QaApiException(fallback);
    }
}
