using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using DmC.Qa.Shared;

var payload = Encoding.UTF8.GetBytes("GameQA direct download Windows file-lock regression smoke");
var expectedSha = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
var redirectServer = new TcpListener(IPAddress.Loopback, 0);
var contentServer = new TcpListener(IPAddress.Loopback, 0);
redirectServer.Start();
contentServer.Start();

var redirectPort = ((IPEndPoint)redirectServer.LocalEndpoint).Port;
var contentPort = ((IPEndPoint)contentServer.LocalEndpoint).Port;
var directUrl = $"http://127.0.0.1:{contentPort}/payload";
var redirectBase = new Uri($"http://127.0.0.1:{redirectPort}/", UriKind.Absolute);

var redirectTask = ServeOnceAsync(
    redirectServer,
    $"HTTP/1.1 302 Found\r\nLocation: {directUrl}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
    null);
var contentTask = ServeOnceAsync(
    contentServer,
    $"HTTP/1.1 200 OK\r\nContent-Length: {payload.Length}\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n",
    payload);

var root = Path.Combine(Path.GetTempPath(), "GameQaDirectFileSmoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var destination = Path.Combine(root, "kanıt.bin");

try
{
    await DirectFileTransfer.DownloadAsync(
        redirectBase,
        "download",
        null,
        destination,
        expectedSha);

    await Task.WhenAll(redirectTask, contentTask);

    if (!File.Exists(destination))
    {
        throw new InvalidOperationException("İndirme tamamlandı ancak hedef dosya oluşmadı.");
    }
    if (File.Exists(destination + ".part"))
    {
        throw new InvalidOperationException("Geçici .part dosyası indirme sonrasında kaldı.");
    }
    if (!File.ReadAllBytes(destination).SequenceEqual(payload))
    {
        throw new InvalidOperationException("Hedef dosya içeriği beklenen veriyle eşleşmiyor.");
    }

    Console.WriteLine("DirectFileTransfer Windows file-lock regression smoke: OK");
}
finally
{
    redirectServer.Stop();
    contentServer.Stop();
    try
    {
        Directory.Delete(root, true);
    }
    catch
    {
    }
}

static async Task ServeOnceAsync(TcpListener listener, string headers, byte[]? body)
{
    using var client = await listener.AcceptTcpClientAsync();
    await using var stream = client.GetStream();

    var requestBuffer = new byte[4096];
    var received = new List<byte>();
    while (true)
    {
        var read = await stream.ReadAsync(requestBuffer);
        if (read == 0)
        {
            break;
        }
        received.AddRange(requestBuffer.AsSpan(0, read).ToArray());
        if (Encoding.ASCII.GetString(received.ToArray()).Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            break;
        }
    }

    var headerBytes = Encoding.ASCII.GetBytes(headers);
    await stream.WriteAsync(headerBytes);
    if (body is not null)
    {
        await stream.WriteAsync(body);
    }
    await stream.FlushAsync();
}
