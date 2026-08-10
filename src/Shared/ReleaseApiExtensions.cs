using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DmC.Qa.Shared;

public static class ReleaseApiExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static async Task<IReadOnlyList<ClientReleaseHistoryItem>> GetClientReleaseHistoryAsync(
        this PlatformApiClient client,
        string channel,
        CancellationToken cancellationToken = default)
    {
        using var http = CreateHttp(client);
        using var response = await http.GetAsync(
            $"client/releases?channel={Uri.EscapeDataString(channel)}",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ClientReleaseHistoryItem>>(Json, cancellationToken) ?? [];
    }

    public static async Task<ClientReleasePublishResult> PublishClientReleaseAsync(
        this PlatformApiClient client,
        string filePath,
        string channel,
        string version,
        string title,
        string notes,
        string? minimumVersion,
        bool mandatory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new QaApiException("Yayınlanacak istemci ZIP dosyası bulunamadı.");
        }

        using var http = CreateHttp(client, TimeSpan.FromHours(2));
        await using var source = File.OpenRead(filePath);
        using var fileContent = new ProgressStreamContent(source, progress, cancellationToken);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", Path.GetFileName(filePath));
        form.Add(new StringContent(channel), "channel");
        form.Add(new StringContent(version), "version");
        form.Add(new StringContent(title), "title");
        form.Add(new StringContent(notes), "notes");
        if (!string.IsNullOrWhiteSpace(minimumVersion)) form.Add(new StringContent(minimumVersion), "minimum_version");
        form.Add(new StringContent(mandatory ? "true" : "false"), "mandatory");

        using var response = await http.PostAsync("client/releases/publish", form, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ClientReleasePublishResult>(Json, cancellationToken)
               ?? throw new QaApiException("İstemci sürümü yayın yanıtı okunamadı.");
    }

    private static HttpClient CreateHttp(PlatformApiClient client, TimeSpan? timeout = null)
    {
        var http = new HttpClient { BaseAddress = client.BaseAddress, Timeout = timeout ?? TimeSpan.FromMinutes(30) };
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
