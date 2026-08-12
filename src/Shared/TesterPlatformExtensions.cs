using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DmC.Qa.Shared;

public static class TesterPlatformExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static async Task<CurrentBuildDetail?> GetCurrentBuildDetailAsync(
        this PlatformApiClient client,
        string projectId,
        CancellationToken cancellationToken = default)
    {
        using var http = CreateHttp(client);
        using var response = await http.GetAsync($"projects/{projectId}/builds/current-detail", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CurrentBuildDetail>(Json, cancellationToken);
    }

    public static async Task<SharedTaskDetail?> GetSharedTaskDetailAsync(
        this PlatformApiClient client,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        using var http = CreateHttp(client);
        using var response = await http.GetAsync($"tasks/{taskId}/shared", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SharedTaskDetail>(Json, cancellationToken);
    }

    private static HttpClient CreateHttp(PlatformApiClient client)
    {
        var http = new HttpClient { BaseAddress = client.BaseAddress, Timeout = TimeSpan.FromMinutes(30) };
        if (!string.IsNullOrWhiteSpace(client.DeviceAuthorization))
        {
            var split = client.DeviceAuthorization!.Split(' ', 2);
            if (split.Length == 2)
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(split[0], split[1]);
            }
        }
        return http;
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
}
