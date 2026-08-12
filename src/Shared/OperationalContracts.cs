namespace DmC.Qa.Shared;

public sealed record AdminRetestResultSummary(string TesterName, string Result);

public sealed record AdminRetestItem(
    string Id,
    string? BugId,
    string BugKey,
    string? BuildVersion,
    IReadOnlyList<string> Assignees,
    IReadOnlyList<AdminRetestResultSummary> Results,
    DateTimeOffset? DeadlineAt,
    bool Closed,
    DateTimeOffset CreatedAt);
