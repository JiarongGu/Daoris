using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>sync</c> proposal (HELP10, D109): Settings → Workspace → Session branches → *Bring up to date*, and
/// <c>daoris-driver trees sync</c> — each repository's line fast-forwarded from <c>origin</c>, the branches still at work
/// replayed onto it, the landed branches whose work reached it deleted. Its target a repository, or none for every one
/// with a checkout here.
/// </summary>
/// <remarks>
/// <para><b>Two presses, as the screen's.</b> D109 fetches nothing until the person presses: looking reaches the network
/// as them. So the card's first Apply is the look — <c>TREES_SYNC_PLAN</c>'s own list, through
/// <see cref="IHelpDoors.SyncPlanAsync"/> — whose rows are kept in the proposal's file (<see cref="HelpProposal.Listed"/>)
/// and shown on the card, which stays; the second is <c>TREES_SYNC</c>'s own press, through
/// <see cref="IHelpDoors.SyncAsync"/>, on the rows that look listed as moving and on nothing else. The press does not
/// fetch again, and judges each row again right before it acts.</para>
///
/// <para>The rows are the screen's (<c>settings/Sync.tsx</c>): a line that moves and has a line, a branch that replays,
/// a landed branch that may go — each keyed <c>repository:branch</c>, the line's by its branch — and each said in the
/// terminal's words (<see cref="SyncWords"/>, <see cref="LandedWords"/>).</para>
/// </remarks>
internal sealed class HelpSyncProposals : IHelpProposalKind
{
    public string Kind => "sync";

    public string Tool => "sync_propose";

    public IReadOnlyList<string> Doors { get; } = ["sync"];

    public HelpProposal Read(HelpProposal proposal, JsonElement file)
    {
        if (!file.TryGetProperty("listed", out var listed) || listed.ValueKind != JsonValueKind.Array) return proposal with { Listed = null };
        var rows = new List<HelpSyncRow>();
        foreach (var row in listed.EnumerateArray())
        {
            if (Text(row, "key") is not { } key || Text(row, "step") is not { } step) continue;
            rows.Add(new HelpSyncRow(key, step, row.TryGetProperty("moves", out var moves) && moves.ValueKind == JsonValueKind.True, Text(row, "says") ?? ""));
        }

        return proposal with { Listed = rows };
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var repository = Repository(proposal);
        if (repository is not null && !facts.Repositories.Contains(repository, StringComparer.OrdinalIgnoreCase))
        {
            return new HelpPlan($"`{repository}` is not registered on this machine — use a repository's name as Projects lists it.", "", "", null);
        }

        var scope = repository is null ? "every repository with a checkout here" : $"`{repository}`";
        var terminal = "daoris-driver trees sync" + (repository is null ? "" : $" --repository {repository}");
        if (proposal.Listed is not { } rows)
        {
            return new HelpPlan(null,
                $"Bring {scope} up to date after a pull request merged. Look for updates first: Daoris fetches each line from "
                + "`origin`, as you — only origin's own refs move — and this card then lists what the press would do: the line "
                + "fast-forwarded, the branches still at work replayed onto it, the landed branches whose work reached it deleted.",
                terminal, null) { Sync = new HelpSyncPlan(false, []) };
        }

        return new HelpPlan(null,
            $"Bring {scope} up to date: {rows.Count(row => row.Moves)} thing(s) change, only the rows below that move, each judged "
            + "again right before it acts. Daoris fetches nothing more, and never pushes.",
            $"{terminal} --yes", null) { Sync = new HelpSyncPlan(true, rows) };
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var proposal = applying.Proposal;
        var repository = Repository(proposal);
        return proposal.Listed is { } rows
            ? await BringAsync(applying, repository, rows, ct).ConfigureAwait(false)
            : await LookAsync(applying, repository, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The first press: <c>TREES_SYNC_PLAN</c>'s list, fetching each line as the person, its rows kept in the proposal's
    /// file so the card shows them and the second press acts on them. A list with nothing to do settles the card.
    /// </summary>
    private static async Task<HelpApplied> LookAsync(HelpApplying applying, string? repository, CancellationToken ct)
    {
        var id = applying.Id;
        SyncPlan listed;
        try
        {
            listed = await applying.Doors.SyncPlanAsync(repository, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return applying.Settled(false, $"Not looked: `#{id}` (`{applying.Plan.Terminal}`) — {error.Message}", error.Message);
        }

        var rows = Rows(listed);
        var moving = rows.Where(row => row.Moves).ToList();
        if (moving.Count == 0)
        {
            // As the terminal says it: a repository with no checkout here has nothing to bring up to date.
            var why = rows.Count > 0 ? string.Join("; ", rows.Select(row => row.Says))
                : listed.Apart.Count > 0 ? "no repository with a checkout here holds a branch of Daoris's"
                : repository is null ? "no repository has a checkout here" : $"`{repository}` has no checkout here";
            const string Nothing = "nothing to bring up to date";
            return applying.Settled(false, $"Looked for updates (`#{id}`): {Nothing} — {why}.{Besides(listed)}", Nothing);
        }

        Keep(applying.Home, id, rows);
        return new HelpApplied(false,
            $"Looked for updates (`#{id}`): {moving.Count} thing(s) would change — {string.Join("; ", moving.Select(row => row.Says))}. "
            + $"The card now lists them; nothing moves until the person applies it.{Besides(listed)}");
    }

    /// <summary>How many repositories the look left apart are named before the rest are counted.</summary>
    private const int ApartNamed = 12;

    /// <summary>
    /// What the rows do not say (WSR7, D112): what was not fetched, said once, since a row carries only a mark; and the
    /// repositories the look left apart, holding no branch of Daoris's, which a proposal naming one looks at.
    /// </summary>
    internal static string Besides(SyncPlan listed)
    {
        var said = new List<string>();
        var notFetched = SyncWords.NotFetched(listed.Lines, DateTimeOffset.UtcNow);
        if (notFetched.Count > 0)
        {
            said.Add(string.Join(" ", notFetched.Select(line => line.Trim()))["trees: ".Length..]);
        }

        if (listed.Apart.Count > 0)
        {
            var named = string.Join(", ", listed.Apart.Take(ApartNamed).Select(each => each.Repository))
                + (listed.Apart.Count > ApartNamed ? $" and {listed.Apart.Count - ApartNamed} more" : "");
            said.Add($"Not looked at, since they hold no branch of Daoris's ({listed.Apart.Count}): {named}; a proposal naming one looks at it.");
        }

        return said.Count == 0 ? "" : " " + string.Join(" ", said);
    }

    /// <summary>The second press: <c>TREES_SYNC</c>'s own, on the listed rows that move, said as the terminal says what it did.</summary>
    private static async Task<HelpApplied> BringAsync(HelpApplying applying, string? repository, IReadOnlyList<HelpSyncRow> rows, CancellationToken ct)
    {
        var (plan, id) = (applying.Plan, applying.Id);
        var only = rows.Where(row => row.Moves).Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        SyncDone done;
        try
        {
            done = await applying.Doors.SyncAsync(repository, only, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
        }

        // What the proofs cleared and did not happen: a conflict, or a line or branch that moved since the look.
        var missed = done.Lines.Where(result => result.Pull.Moves && !result.Moved).Select(result => result.Message)
            .Concat(done.Rebases.Where(result => result.Item.Replays && !result.Replayed).Select(result => result.Message))
            .Concat(done.Deletes.Where(result => result.Item.Removable && !result.Removed).Select(result => result.Message))
            .ToList();
        var (moved, replayed, removed) = (done.Lines.Count(r => r.Moved), done.Rebases.Count(r => r.Replayed), done.Deletes.Count(r => r.Removed));
        var said = $"{moved} line(s) moved, {replayed} branch(es) replayed, {removed} removed"
            + (missed.Count > 0 ? $"; {missed.Count} did not happen: {string.Join("; ", missed)}" : "");
        return moved + replayed + removed > 0
            ? applying.Settled(true, $"Applied: `#{id}` — {said} (`{plan.Terminal}`).", missed.Count > 0 ? said : null)
            : applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {said}.", said);
    }

    /// <summary>
    /// The rows a look lists, in the screen's selection (<c>settings/Sync.tsx</c>): each line, branch and landed branch,
    /// whether the press would move it, and the terminal's sentence for it.
    /// </summary>
    internal static IReadOnlyList<HelpSyncRow> Rows(SyncPlan plan) =>
    [
        .. plan.Lines.Select(pull => new HelpSyncRow($"{pull.Repository}:{pull.Line}", "line", pull.Moves && pull.Line is not null, SyncWords.Describe(pull))),
        .. plan.Rebases.Select(item => new HelpSyncRow($"{item.Repository}:{item.Branch}", "replay", item.Replays, SyncWords.Describe(item))),
        .. plan.Deletes.Select(item => new HelpSyncRow($"{item.Repository}:{item.Branch}", "delete", item.Removable, LandedWords.Describe(item))),
    ];

    /// <summary>The rows a look listed, kept in the proposal's file beside every field the service wrote, written beside and renamed.</summary>
    private static void Keep(string home, string id, IReadOnlyList<HelpSyncRow> rows)
    {
        var path = Path.Combine(FolderOf(home), $"{id}.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        node["listed"] = new JsonArray([.. rows.Select(row => (JsonNode)new JsonObject
        {
            ["key"] = row.Key, ["step"] = row.Step, ["moves"] = row.Moves, ["says"] = row.Says,
        })]);
        AtomicFile.WriteText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
    }

    private static string? Repository(HelpProposal proposal) => proposal.Target?.Trim() is { Length: > 0 } named ? named : null;
}

/// <summary>One row a look listed (HELP10): its key as the press takes it, its step, whether the press moves it, and its sentence.</summary>
/// <param name="Key">`repository:branch`, the line's by its branch, as <c>TREES_SYNC</c>'s `only` takes it.</param>
/// <param name="Step">`line`, `replay` or `delete`.</param>
/// <param name="Says">The terminal's sentence for it (<see cref="SyncWords"/>, <see cref="LandedWords"/>).</param>
public sealed record HelpSyncRow(string Key, string Step, bool Moves, string Says);

/// <summary>What a sync card shows (HELP10): whether the person has looked, and the rows the look listed.</summary>
public sealed record HelpSyncPlan(bool Looked, IReadOnlyList<HelpSyncRow> Rows);

public sealed partial record HelpProposal
{
    /// <summary>The rows a sync proposal's look listed (HELP10), kept in its file; null before the person looked.</summary>
    public IReadOnlyList<HelpSyncRow>? Listed { get; init; }
}

public sealed partial record HelpPlan
{
    /// <summary>What a sync card shows (HELP10); null for every other kind.</summary>
    public HelpSyncPlan? Sync { get; init; }
}

public partial interface IHelpDoors
{
    /// <summary>
    /// <c>TREES_SYNC_PLAN</c>'s own list (WSR6, D109): each line fetched from <c>origin</c>, as the person, and what the
    /// press would do — for <paramref name="repository"/> alone, or every repository with a checkout here.
    /// </summary>
    Task<SyncPlan> SyncPlanAsync(string? repository, CancellationToken ct);

    /// <summary><c>TREES_SYNC</c>'s own press: only the rows in <paramref name="only"/>, each judged again, fetching nothing.</summary>
    Task<SyncDone> SyncAsync(string? repository, IReadOnlySet<string> only, CancellationToken ct);
}
