using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Ask Daoris's housekeeping (HELPMODE1c2a; the Ask Daoris modes design §4.1): three typed tools over Daoris's own branches and
/// trees, each a request the driver judges and answers (<see cref="HousekeepingBox"/>). None takes a path or a command, none
/// forces, and none drops a recovery ref. Each acts only as the person granted it, which the driver reads at every call.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="box">Where the request is written and its answer read, under the home.</param>
[McpServerToolType]
public sealed class HousekeepingTools(IntakeScope? intake = null, SessionLedger? ledger = null, HousekeepingBox? box = null)
{
    private const string Granted =
        "Ask Daoris only, and only where the person granted Daoris's housekeeping under Ask Daoris's configured access: "
        + "otherwise it is refused, saying so. ";

    [McpServerTool(Name = "housekeeping_inspect")]
    [Description(
        Granted
        + "Look at Daoris's own session branches and the branches its landings made, in one repository or every one of this "
        + "conversation's workspace: each with what it holds, and whether the cleanup would remove it now or keeps it and why. "
        + "It changes nothing. Name a branch it lists as cleared to `housekeeping_clean_up`.")]
    public Task<string> InspectAsync(
        [Description("Optional: the repository by its registered name. Left out, every repository of this conversation's workspace with a checkout here.")]
        string? repository = null,
        CancellationToken ct = default) =>
        AskAsync(HousekeepingBox.Inspect, repository, [], ct);

    [McpServerTool(Name = "housekeeping_clean_up")]
    [Description(
        Granted
        + "Remove the branches named, with their trees, where the cleanup clears them: session branches whose work landed, is "
        + "empty or is carried, and branches a landing made whose work reached the line. Each is judged again right before it "
        + "goes and never forced: one it no longer clears is kept and said, and a branch a running or waiting session holds is "
        + "kept. No recovery ref is dropped. Targets are a repository and branch names as `housekeeping_inspect` "
        + "listed them, never a path. Say what you removed, and what was kept and why.")]
    public Task<string> CleanUpAsync(
        [Description("The repository by its registered name.")] string repository,
        [Description("The branches to remove, as `housekeeping_inspect` listed them as cleared, such as `daoris/s-…`.")] string[] branches,
        CancellationToken ct = default) =>
        AskAsync(HousekeepingBox.CleanUp, repository, branches, ct);

    [McpServerTool(Name = "housekeeping_discard_branch")]
    [Description(
        Granted
        + "Discard one session branch by its name, with its tree where it still has one, as the branch's own discard does "
        + "unforced: it goes only where the cleanup's proof holds, so work no branch of the person's holds is kept and said; "
        + "a branch a running or waiting session holds is kept. It is judged again before it goes, and never forced. The "
        + "target is a repository and a branch name, never a path.")]
    public Task<string> DiscardBranchAsync(
        [Description("The repository by its registered name.")] string repository,
        [Description("The session branch, as git names it: `daoris/s-…`.")] string branch,
        CancellationToken ct = default) =>
        AskAsync(HousekeepingBox.DiscardBranch, repository, [branch], ct);

    private async Task<string> AskAsync(string operation, string? repository, IReadOnlyList<string> branches, CancellationToken ct)
    {
        var housekeeping = box ?? HousekeepingBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return await housekeeping.AskAsync(record, session, operation, repository, branches, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
    }
}
