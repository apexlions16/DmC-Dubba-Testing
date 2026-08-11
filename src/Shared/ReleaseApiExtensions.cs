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
        var baseAddress = ResolveReleaseBase(client.BaseAddress) ?? client.BaseAddress;
        using var http = CreateHttp(baseAddress, client.DeviceAuthorization);
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

        var releaseBase = ResolveReleaseBase(client.BaseAddress);
        if (releaseBase is null)
        {
            return await PublishLegacyAsync(
                client,
                filePath,
                channel,
                version,
                title,
                notes,
                minimumVersion,
                mandatory,
                progress,
                cancellationToken);
        }

        var info = new FileInfo(filePath);
        var sha256 = await DirectFileTransfer.ComputeSha256Async(filePath, cancellationToken);
        using var http = CreateHttp(releaseBase, client.DeviceAuthorization, TimeSpan.FromMinutes(5));

        using var initResponse = await http.PostAsJsonAsync(
            "client/releases/init",
            new
            {
                channel,
                version,
                title,
                filename = info.Name,
                size_bytes = info.Length,
                sha256
            },
            Json,
            cancellationToken);
        await EnsureSuccessAsync(initResponse, cancellationToken);
        var init = await initResponse.Content.ReadFromJsonAsync<ReleaseUploadInit>(Json, cancellationToken)
                   ?? throw new QaApiException("İstemci sürümü yükleme oturumu oluşturulamadı.");

        await DirectFileTransfer.PutFileAsync(
            new Uri(init.UploadUrl, UriKind.Absolute),
            filePath,
            "application/zip",
            progress,
            cancellationToken);

        using var finalizeResponse = await http.PostAsJsonAsync(
            "client/releases/finalize",
            new
            {
                release_id = init.ReleaseId,
                storage_path = init.StoragePath,
                channel,
                version,
                title,
                notes,
                minimum_version = minimumVersion,
                mandatory,
                size_bytes = info.Length,
                sha256
            },
            Json,
            cancellationToken);
        await EnsureSuccessAsync(finalizeResponse, cancellationToken);
        return await finalizeResponse.Content.ReadFromJsonAsync<ClientReleasePublishResult>(Json, cancellationToken)
               ?? throw new QaApiException("İstemci sürümü yayın yanıtı okunamadı.");
    }

    private static async Task<ClientReleasePublishResult> PublishLegacyAsync(
        PlatformApiClient client,
        string filePath,
        string channel,
        string version,
        string title,
        string notes,
        string? minimumVersion,
        bool mandatory,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var http = CreateHttp(client.BaseAddress, client.DeviceAuthorization, TimeSpan.FromHours(2));
        await using var source = File.OpenRead(filePath);
        using var fileContent = new ProgressStreamContent(source, progress, cancellationToken);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", Path.GetFileName(filePath));
        form.Add(new StringContent(channel), "channel");
        form.Add(new StringContent(version), "version");
        form.Add(new StringContent(title), "title");
        form.Add(new StringContent(notes), "notes");
        if (!string.IsNullOrWhiteSpace(minimumVersion))
        {
            form.Add(new StringContent(minimumVersion), "minimum_version");
        }
        form.Add(new StringContent(mandatory ? "true" : "false"), "mandatory");

        using var response = await http.PostAsync("client/releases/publish", form, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ClientReleasePublishResult>(Json, cancellationToken)
               ?? throw new QaApiException("İstemci sürümü yayın yanıtı okunamadı.");
    }

    private static Uri? ResolveReleaseBase(Uri apiBase)
    {
        const string marker = "/functions/v1/qa-api/";
        var absolute = apiBase.AbsoluteUri;
        var index = absolute.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }
        return new Uri(
            string.Concat(
                absolute.AsSpan(0, index),
                "/functions/v1/qa-releases/",
                absolute.AsSpan(index + marker.Length)),
            UriKind.Absolute);
    }

    private static HttpClient CreateHttp(
        Uri baseAddress,
        string? deviceAuthorization,
        TimeSpan? timeout = null)
    {
        var http = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = timeout ?? TimeSpan.FromMinutes(30)
        };
        if (!string.IsNullOrWhiteSpace(deviceAuthorization))
        {
            var split = deviceAuthorization.Split(' ', 2);
            if (split.Length == 2)
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(split[0], split[1]);
            }
        }
        return http;
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

    private sealed record ReleaseUploadInit(
        string ReleaseId,
        string UploadUrl,
        string StoragePath,
        string Filename);

    private sealed class ProgressStreamContent : HttpContent
    {
        private readonly Stream _source;
        private readonly IProgress<double>? _progress;
        private readonly CancellationToken _cancellationToken;

        public ProgressStreamContent(
            Stream source,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
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
