using System.Text.Json;

namespace DmC.Qa.Shared;

public sealed record PurgePreview(
    string ProjectId,
    string ProjectName,
    string Status,
    int BugReports,
    int EvidenceFiles,
    int Retests,
    int Tasks,
    int Builds,
    int ProjectMembers,
    int Sections,
    long EstimatedStorageBytes);

public sealed record PurgeRequestCreated(
    string Id,
    string ProjectId,
    string ProjectName,
    string ConfirmationCode,
    JsonElement Preview,
    string Warning);

public sealed record PurgeRequestItem(
    string Id,
    string ProjectId,
    string ProjectName,
    string RequestedBy,
    string Status,
    JsonElement Preview,
    DateTimeOffset RequestedAt);
