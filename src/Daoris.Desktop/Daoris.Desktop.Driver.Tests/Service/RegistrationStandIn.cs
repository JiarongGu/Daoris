using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The world registration following reads, in memory (WSSETUP5): the registry's rows, what each line holds, and what
/// was sent, so the follower and its terminal's words are held with no service and no git.
/// </summary>
internal class RegistrationStandIn : IRegistrationWorld
{
    public List<RegistrationRow> Rows { get; } = [];

    public Dictionary<string, LineFiles> Lines { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Worktrees { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Waiting { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<JsonObject> Sent { get; } = [];

    public List<RegistrationFollowed> Said { get; } = [];

    public int Refreshes { get; private set; }

    /// <summary>The service's words for every registration, where it refuses them all.</summary>
    public string? RefuseWith { get; set; }

    /// <summary>What a refresh answers, where it fails.</summary>
    public string? RefreshFails { get; set; }

    public bool LocalService { get; set; } = true;

    public Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RegistrationRow>>(Rows);

    public Task<LineFiles> ReadLineAsync(RegistrationRow row, CancellationToken ct) =>
        Task.FromResult(Lines.TryGetValue(row.Repository, out var files) ? files : new LineFiles(null, null, "none is set, and git names none"));

    public string? WorktreeMain(string root) => Worktrees.Contains(root) ? "the main tree" : null;

    public string? SetupWaiting(string repository) => Waiting.GetValueOrDefault(repository);

    public Task<(bool Ok, string Message)> RegisterAsync(JsonObject body, CancellationToken ct)
    {
        if (RefuseWith is not null) return Task.FromResult((false, RefuseWith));
        Sent.Add(body);
        return Task.FromResult((true, ""));
    }

    public Task<string?> RefreshAsync(CancellationToken ct)
    {
        Refreshes++;
        return Task.FromResult(RefreshFails);
    }

    public void Followed(RegistrationFollowed what) => Said.Add(what);

    /// <summary>A row with nothing declared: <paramref name="adopted"/> or not, at <paramref name="root"/>.</summary>
    public static RegistrationRow Row(string repository, string? root = "/checkouts/x", bool adopted = false) =>
        new(repository, "default", root, adopted, null, [], [], [], [], false, false, []);

    /// <summary>What a line holds, at a fixed commit on <c>main</c>.</summary>
    public static LineFiles OnLine(string? manifest, string? lanes = null) =>
        new("main", "0123456789abcdef0123456789abcdef01234567", null) { Manifest = manifest, Lanes = lanes };
}
