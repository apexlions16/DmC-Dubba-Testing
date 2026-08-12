namespace DmC.Qa.Shared;

public sealed record CurrentBuildDetail(
    string Id,
    string Version,
    string Title,
    string Description,
    string InstallationInstructions,
    string Changelog,
    string? OriginalFilename,
    long? SizeBytes,
    string? Sha256,
    DateTimeOffset? PublishedAt);

public sealed record SharedTaskDetail(
    string Id,
    string Title,
    string Description,
    DateTimeOffset? DeadlineAt,
    string Status,
    IReadOnlyList<string> Assignees,
    int KnownReportCount);
