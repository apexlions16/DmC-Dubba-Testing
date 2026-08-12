namespace DmC.Qa.Shared;

public sealed record ClientReleaseHistoryItem(
    string Id,
    string Channel,
    string Version,
    string Title,
    string Notes,
    string ArtifactUrl,
    string Sha256,
    string? MinimumVersion,
    bool Mandatory,
    DateTimeOffset PublishedAt);

public sealed record ClientReleasePublishResult(
    string Id,
    string Channel,
    string Version,
    string Title,
    string Sha256,
    long SizeBytes,
    string ArtifactUrl);
