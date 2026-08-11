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
}
