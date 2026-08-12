using System.Text.Json;

namespace DmC.Qa.Shared;

public sealed record AdminBugListItem(
    string Id,
    string Key,
    string Title,
    string BugType,
    string? Trigger,
    string Status,
    string? RootCause,
    string ReporterId,
    string ReporterName,
    string? ReportedBuildId,
    string? BuildVersion,
    string? TaskId,
    string? TaskTitle,
    double? BugTimestampSeconds,
    int EvidenceCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BugEventItem(
    string Id,
    string EventType,
    string ActorName,
    string? PreviousStatus,
    string? NewStatus,
    string? BuildVersion,
    string? Note,
    JsonElement? Metadata,
    DateTimeOffset CreatedAt);

public sealed record EvidenceItem(
    string Id,
    string Filename,
    string MediaType,
    long? SizeBytes,
    string? Sha256,
    DateTimeOffset CreatedAt);

public sealed record RetestResultItem(
    string TesterName,
    string Result,
    string? BuildVersion,
    string Comment,
    DateTimeOffset CreatedAt);

public sealed record RetestHistoryItem(
    string Id,
    string BuildId,
    string? BuildVersion,
    string Note,
    DateTimeOffset? DeadlineAt,
    bool Closed,
    IReadOnlyList<string> Assignees,
    IReadOnlyList<RetestResultItem> Results,
    DateTimeOffset CreatedAt);

public sealed record AdminBugDetail(
    string Id,
    string Key,
    string ProjectId,
    string? SectionId,
    string? SectionName,
    string? TaskId,
    string? TaskTitle,
    string? ReportedBuildId,
    string? BuildVersion,
    string ReporterId,
    string ReporterName,
    string Title,
    string BugType,
    string? Trigger,
    string Description,
    int? ReproAttempts,
    int? ReproHits,
    double? BugTimestampSeconds,
    string Status,
    string? RootCause,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<BugEventItem> Events,
    IReadOnlyList<EvidenceItem> Evidence,
    IReadOnlyList<RetestHistoryItem> Retests);

public sealed record AdminBuildItem(
    string Id,
    string ProjectId,
    string Version,
    string Title,
    string Description,
    string InstallationInstructions,
    string Changelog,
    string? OriginalFilename,
    long? SizeBytes,
    string? Sha256,
    string Status,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt);

public sealed record AdminTaskItem(
    string Id,
    string ProjectId,
    string? SectionId,
    string? RequiredBuildId,
    string? BuildVersion,
    string Title,
    string Description,
    DateTimeOffset? DeadlineAt,
    string Status,
    IReadOnlyList<string> AssigneeIds,
    IReadOnlyList<string> Assignees,
    DateTimeOffset CreatedAt);

public sealed record ProjectMemberItem(
    string Id,
    string DisplayName,
    string Role,
    bool Enabled);

public sealed record SectionItem(string Id, string Name, int SortOrder);

public sealed record AnalyticsBucket(
    string Key,
    int Count,
    int Resolved,
    decimal ResolutionPercentage);

public sealed record MyBugItem(
    string Id,
    string Key,
    string ProjectId,
    string Title,
    string BugType,
    string Status,
    string? RootCause,
    DateTimeOffset CreatedAt);

public sealed record InboxNotificationItem(
    string Id,
    string? ProjectId,
    string Title,
    string Body,
    string Severity,
    bool RequiresAcknowledgement,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ReadAt,
    DateTimeOffset? AcknowledgedAt);

public sealed record AdminNotificationItem(
    string Id,
    string? ProjectId,
    string Title,
    string Body,
    string Severity,
    DateTimeOffset CreatedAt,
    int RecipientCount,
    int ReadCount,
    int AcknowledgedCount);

public sealed record CreateResult(string Id);
public sealed record BuildCreateResult(string Id, string Version, string Status);
public sealed record RetestCreateResult(string Id, string BugId, string BuildId);
public sealed record StorageInfo(string Mode, string Location);
