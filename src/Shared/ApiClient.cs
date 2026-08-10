using System.Net.Http.Json;
using System.Text.Json;

namespace DmC.Qa.Shared;

public sealed class QaApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public QaApiClient(HttpClient http)
    {
        _http = http;
    }

    public void SetUser(string userId)
    {
        _http.DefaultRequestHeaders.Remove("X-User-Id");
        _http.DefaultRequestHeaders.Add("X-User-Id", userId);
    }

    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default)
        => await _http.GetFromJsonAsync<List<ProjectSummary>>("projects", _json, cancellationToken)
           ?? [];

    public async Task<IReadOnlyList<TaskSummary>> GetMyTasksAsync(CancellationToken cancellationToken = default)
        => await _http.GetFromJsonAsync<List<TaskSummary>>("tasks/mine", _json, cancellationToken)
           ?? [];

    public Task<BuildSummary?> GetCurrentBuildAsync(string projectId, CancellationToken cancellationToken = default)
        => _http.GetFromJsonAsync<BuildSummary>($"projects/{projectId}/builds/current", _json, cancellationToken);

    public Task<IssueProgress?> GetProjectProgressAsync(string projectId, CancellationToken cancellationToken = default)
        => _http.GetFromJsonAsync<IssueProgress>($"projects/{projectId}/analytics/summary", _json, cancellationToken);

    public async Task<string> SubmitBugAsync(object payload, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("bugs", payload, _json, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("key").GetString()
               ?? throw new InvalidOperationException("Bug response did not include a public key.");
    }
}
