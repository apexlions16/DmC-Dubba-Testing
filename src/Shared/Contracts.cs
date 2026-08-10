namespace DmC.Qa.Shared;

public enum BugStatus
{
    New,
    Acknowledged,
    InProgress,
    RetestRequired,
    Resolved,
    OnHold,
    Duplicate,
    NotABug,
    WontFix,
    Reopened
}

public enum RetestResult
{
    Passed,
    Failed,
    Uncertain
}

public enum BuildStatus
{
    Uploading,
    Candidate,
    Current,
    Superseded,
    Archived
}

public enum NotificationSeverity
{
    Info,
    Warning,
    Critical,
    MustRead
}

public sealed record DeviceEnrollmentResponse(
    string UserId,
    string DisplayName,
    string Role,
    string DeviceId,
    string Credential);

public sealed record CurrentUserSummary(
    string Id,
    string DisplayName,
    string Role);

public sealed record ProjectSummary(
    string Id,
    string Key,
    string Name,
    string SectionLabel,
    string Status);

public sealed record BuildSummary(
    string Id,
    string Version,
    string Title,
    string Description,
    string InstallationInstructions,
    string Changelog,
    long? SizeBytes,
    string? Sha256,
    DateTimeOffset? PublishedAt);

public sealed record TaskSummary(
    string Id,
    string ProjectId,
    string? SectionId,
    string? RequiredBuildId,
    string Title,
    string Description,
    DateTimeOffset? DeadlineAt);

public sealed record BugSummary(
    string Id,
    string Key,
    string ProjectId,
    string? TaskId,
    string Title,
    string BugType,
    string? Trigger,
    BugStatus Status,
    string? RootCause,
    DateTimeOffset CreatedAt);

public sealed record BugCreatedResponse(
    string Id,
    string Key,
    string Status);

public sealed record EvidenceUploadResponse(
    string Id,
    string Filename,
    string MediaType,
    long? SizeBytes,
    string? Sha256,
    string StoragePath);

public sealed record RetestAssignment(
    string Id,
    string BugId,
    string BugKey,
    string BuildId,
    string BuildVersion,
    string Title,
    string Note,
    DateTimeOffset? DeadlineAt);

public sealed record IssueProgress(
    int TotalReports,
    int ValidKnownIssues,
    int Resolved,
    int InProgress,
    int RetestRequired,
    int NewOrUnstarted,
    int OnHold,
    int Excluded,
    decimal ResolutionPercentage);

public sealed record NotificationItem(
    string Id,
    string? ProjectId,
    string Title,
    string Body,
    NotificationSeverity Severity,
    bool RequiresAcknowledgement,
    DateTimeOffset CreatedAt);

public sealed record ClientReleaseManifest(
    string Channel,
    string Version,
    string Title,
    string Notes,
    Uri ArtifactUri,
    string Sha256,
    string? MinimumVersion,
    bool Mandatory);
