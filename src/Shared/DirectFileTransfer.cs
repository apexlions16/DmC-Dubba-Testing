using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace DmC.Qa.Shared;

internal static class DirectFileTransfer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private const long SinglePutMaximumBytes = 5L * 1024 * 1024 * 1024;

    public static bool TryResolveFilesBase(Uri apiBase, out Uri filesBase)
    {
        const string marker = "/functions/v1/qa-api/";
        var absolute = apiBase.AbsoluteUri;
        var index = absolute.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            filesBase = apiBase;
            return false;
        }

        filesBase = new Uri(
            string.Concat(
                absolute.AsSpan(0, index),
                "/functions/v1/qa-files/",
                absolute.AsSpan(index + marker.Length)),
            UriKind.Absolute);
        return true;
    }

    public static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<DirectUploadInit> InitializeAsync(
        Uri filesBase,
        string endpoint,
        string? deviceAuthorization,
        string filePath,
        string mediaType,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(filePath);
        using var http = CreateApiHttp(filesBase, deviceAuthorization, TimeSpan.FromMinutes(2));
        using var response = await http.PostAsJsonAsync(
            endpoint,
            new
            {
                filename = info.Name,
                size_bytes = info.Length,
                sha256,
                media_type = mediaType
            },
            Json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DirectUploadInit>(Json, cancellationToken)
               ?? throw new QaApiException("Dosya yükleme oturumu oluşturulamadı.");
    }

    public static async Task PutFileAsync(
        Uri uploadUri,
        string filePath,
        string mediaType,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(filePath);
        if (info.Length > SinglePutMaximumBytes)
        {
            throw new QaApiException(
                "Bu dosya 5 GB sınırını aşıyor. Büyük dosya için multipart yükleme kullanılmalıdır.");
        }

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromHours(12)
        };
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var content = new ProgressStreamContent(stream, progress, cancellationToken);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUri)
        {
            Content = content
        };
        using var response = await http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new QaApiException($"HF dosya yüklemesi başarısız oldu ({(int)response.StatusCode}).");
        }
        progress?.Report(100);
    }

    public static async Task<T> FinalizeAsync<T>(
        Uri filesBase,
        string endpoint,
        string? deviceAuthorization,
        DirectUploadInit init,
        string filePath,
        string mediaType,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(filePath);
        using var http = CreateApiHttp(filesBase, deviceAuthorization, TimeSpan.FromMinutes(2));
        using var response = await http.PostAsJsonAsync(
            endpoint,
            new
            {
                storage_path = init.StoragePath,
                filename = init.Filename,
                media_type = mediaType,
                size_bytes = info.Length,
                sha256
            },
            Json,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
               ?? throw new QaApiException("Dosya yükleme tamamlama yanıtı okunamadı.");
    }

    public static async Task DownloadAsync(
        Uri filesBase,
        string endpoint,
        string? deviceAuthorization,
        string destinationPath,
        string? expectedSha256 = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Uri directUri;
        using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
        using (var api = new HttpClient(handler)
        {
            BaseAddress = filesBase,
            Timeout = TimeSpan.FromMinutes(2)
        })
        {
            ApplyAuthorization(api, deviceAuthorization);
            using var redirect = await api.GetAsync(endpoint, cancellationToken);
            if (redirect.StatusCode != HttpStatusCode.Found &&
                redirect.StatusCode != HttpStatusCode.TemporaryRedirect &&
                redirect.StatusCode != HttpStatusCode.PermanentRedirect)
            {
                await EnsureSuccessAsync(redirect, cancellationToken);
                throw new QaApiException("Dosya indirme yönlendirmesi alınamadı.");
            }
            directUri = redirect.Headers.Location
                        ?? throw new QaApiException("Dosya indirme adresi alınamadı.");
        }

        using var direct = new HttpClient { Timeout = TimeSpan.FromHours(12) };
        using var response = await direct.GetAsync(
            directUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new QaApiException($"HF dosya indirmesi başarısız oldu ({(int)response.StatusCode}).");
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = destinationPath + ".part";
        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var sha = SHA256.Create();
            var total = response.Content.Headers.ContentLength;
            var buffer = new byte[1024 * 1024];
            long copied = 0;
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                sha.TransformBlock(buffer, 0, read, null, 0);
                copied += read;
                if (total is > 0)
                {
                    progress?.Report(Math.Min(100, copied * 100d / total.Value));
                }
            }
            sha.TransformFinalBlock([], 0, 0);
            await output.FlushAsync(cancellationToken);

            var actual = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(expectedSha256) &&
                !string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new QaApiException("İndirilen dosyanın SHA-256 doğrulaması başarısız oldu.");
            }

            File.Move(temporaryPath, destinationPath, true);
            progress?.Report(100);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
            throw;
        }
    }

    public static string MediaTypeForFile(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".avi" => "video/x-msvideo",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".zip" => "application/zip",
            ".7z" => "application/x-7z-compressed",
            ".rar" => "application/vnd.rar",
            _ => "application/octet-stream"
        };

    private static HttpClient CreateApiHttp(Uri baseAddress, string? deviceAuthorization, TimeSpan timeout)
    {
        var http = new HttpClient { BaseAddress = baseAddress, Timeout = timeout };
        ApplyAuthorization(http, deviceAuthorization);
        return http;
    }

    private static void ApplyAuthorization(HttpClient http, string? deviceAuthorization)
    {
        if (string.IsNullOrWhiteSpace(deviceAuthorization))
        {
            return;
        }
        var split = deviceAuthorization.Split(' ', 2);
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
            while ((read = await _source.ReadAsync(buffer.AsMemory(0, buffer.Length), _cancellationToken)) > 0)
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

internal sealed record DirectUploadInit(
    string UploadUrl,
    string StoragePath,
    string Filename,
    string? UploadId = null)
{
    public Uri UploadUri => new(UploadUrl, UriKind.Absolute);
}

internal sealed record DirectUploadFinalizeResult(string Status, string StoragePath);
