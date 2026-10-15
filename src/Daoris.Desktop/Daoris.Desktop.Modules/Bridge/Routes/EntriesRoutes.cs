using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// What a line of work (an entry, D167) and its ask do as one, the page's `bridge/entries.ts`: an entry's one Accept and an ask's
/// (ACCEPTALL1, D167's 2026-10-15 amendment points 1 and 4), the plan before the press and the press, over the driver's
/// <see cref="SessionTrees.PlanEntryAsync"/> and <see cref="SessionTrees.LandEntryAsync"/>. Each part lands by its own landing, its
/// gate read as its review's Accept reads it.
/// </summary>
/// <remarks>
/// <para><b>Which parts.</b> An entry's parts whose tree offers work to land (<see cref="SessionGrouping.Lands"/>, after SQUASHTIDY1f's
/// proof), oldest first, as the Parts strip shows them; an ask's entries are its own, one per repository, never its intake.</para>
///
/// <para><b>Nothing machine-local comes back</b>: ids, names of branches and files, and the landing's sentences, as
/// <c>LAND_SESSION_TREE</c>'s.</para>
/// </remarks>
public sealed class EntriesRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>What an entry's landing changes: what each of its parts' landings does (STATEHUB1b, D169), for every session it names.</summary>
    private const StateScopes Lands = StateScopes.Sessions | StateScopes.Landing | StateScopes.Diff | StateScopes.HandOff
        | StateScopes.Sweep | StateScopes.TreesSync;

    /// <summary>
    /// An entry's Accept (point 1): <c>ENTRY_LANDING</c> says where its parts would land, each part's plan and what superseded it;
    /// <c>LAND_ENTRY</c> lands them, leaving out only the parts the page sends in <c>leaveOut</c> (point 3).
    /// </summary>
    [DriverRoute("ENTRY_LANDING")]
    [DriverRoute("LAND_ENTRY")]
    [Publishes("LAND_ENTRY", Lands)]
    private async Task<object?> EntryAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var key = PayloadHelper.GetRequiredValue<string>(request.Payload, "entry");
        var service = _loop.Service ?? throw NotReady();
        var (entries, rows) = await LookAsync(service, entry => entry.Key == key, cancellationToken).ConfigureAwait(false);
        var entry = entries.FirstOrDefault() ?? new SessionEntry(key, "", "", SessionGroup.Ended, "", []);
        if (request.Type == "ENTRY_LANDING") return await PlanAsync(service, entry, rows, cancellationToken).ConfigureAwait(false);

        var landed = await LandAsync(service, entry, rows, LeaveOut(request), cancellationToken).ConfigureAwait(false);
        _loop.Nudge();
        return landed;
    }

    /// <summary>
    /// An ask's Accept (point 4): <c>ASK_LANDING</c> is each of its entries' plans; <c>LAND_ASK</c> lands each, one after another,
    /// in its own repository, by its own rule and gates. The answer is per repository: a refusal holds only its own, and the others
    /// land.
    /// </summary>
    [DriverRoute("ASK_LANDING")]
    [DriverRoute("LAND_ASK")]
    [Publishes("LAND_ASK", Lands)]
    private async Task<object?> AskAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var ask = PayloadHelper.GetRequiredValue<string>(request.Payload, "ask");
        var service = _loop.Service ?? throw NotReady();
        var (entries, rows) = await LookAsync(service, entry => entry.Origin == $"ask:{ask}", cancellationToken).ConfigureAwait(false);
        var own = EntriesOf(ask, entries);
        if (request.Type == "ASK_LANDING")
        {
            var plans = new List<object>();
            foreach (var entry in own.Where(entry => PartsOf(entry, rows).Count > 0))
            {
                plans.Add(await PlanAsync(service, entry, rows, cancellationToken).ConfigureAwait(false));
            }

            return new { Ask = ask, Entries = plans };
        }

        var leaveOut = LeaveOut(request);
        var answers = new List<object>();
        var done = true;
        foreach (var entry in own.Where(entry => PartsOf(entry, rows).Count > 0))
        {
            // One repository's refusal, said or thrown, holds only its own entry (point 4): the next is landed all the same.
            try
            {
                var landed = await LandingAsync(service, entry, rows, leaveOut, cancellationToken).ConfigureAwait(false);
                done &= landed.Landed;
                answers.Add(Landed(entry.Key, entry.Repository, landed));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                done = false;
                answers.Add(Landed(entry.Key, entry.Repository, new EntryLanding(false, $"Nothing was landed: {error.Message}", null, [])));
            }
        }

        _loop.Nudge();
        return new { Ask = ask, Done = done && answers.Count > 0, Repositories = answers };
    }

    /// <summary>The parts of <paramref name="entry"/> whose tree offers work to land, in the entry's order: what its Accept lands.</summary>
    public static IReadOnlyList<EntryPart> PartsOf(SessionEntry entry, IReadOnlyList<SessionGrouping> rows)
    {
        var offering = rows.Where(row => row.Lands is not null).Select(row => row.Session).ToHashSet(StringComparer.Ordinal);
        return [.. entry.Parts.Where(part => offering.Contains(part.Session))];
    }

    /// <summary>An ask's own entries (point 4): one per repository, by its name, never its intake.</summary>
    public static IReadOnlyList<SessionEntry> EntriesOf(string ask, IReadOnlyList<SessionEntry> entries) =>
        [.. entries.Where(entry => entry.Origin == $"ask:{ask}" && entry.Repository != "intake")
            .OrderBy(entry => entry.Repository, StringComparer.Ordinal)];

    /// <summary>
    /// <c>ENTRY_LANDING</c>'s answer: the entry, its repository, the form and where its parts land, and each part: its own plan's
    /// target, form and what it leaves out, lands and conflicts with (as <c>LANDING</c> says them), what holds it, the earlier part
    /// whose tree it shares, and the later part that superseded it.
    /// </summary>
    /// <param name="held">What holds each part, its gate's sentence, by session; absent where nothing does.</param>
    public static object Plan(string entry, string repository, EntryPlan plan, IReadOnlyDictionary<string, string>? held = null) => new
    {
        Entry = entry,
        Repository = repository,
        plan.Form,
        plan.Target,
        Parts = plan.Parts.Select(part => new
        {
            part.Session,
            part.Plan?.Form,
            part.Plan?.Target,
            part.Plan?.Own,
            part.Plan?.Plugin,
            part.Plan?.Problem,
            LeftOut = TreesRoutes.LeftOut(part.Plan?.LeftOut),
            Lands = part.Plan?.Lands?.Select(commit => new { Commit = TextBounds.ShortCommit(commit.Sha, 8), commit.Subject }).ToArray(),
            Conflict = TreesRoutes.Conflict(part.Plan?.Conflict),
            Held = held?.GetValueOrDefault(part.Session),
            part.SameTreeAs,
            Superseded = part.Superseded is { } by ? new { by.By, by.Says, by.Conflicts } : null,
        }).ToArray(),
    };

    /// <summary>
    /// <c>LAND_ENTRY</c>'s answer, and one repository's in <c>LAND_ASK</c>'s: whether every part tried landed, the sentence, the
    /// branch, and each part's outcome with its landing's own words, branch, plugin, what it left out and its conflict.
    /// </summary>
    public static object Landed(string entry, string repository, EntryLanding landed) => new
    {
        Entry = entry,
        Repository = repository,
        Done = landed.Landed,
        landed.Message,
        landed.Branch,
        Parts = landed.Parts.Select(part => new
        {
            part.Session,
            part.Outcome,
            Done = part.Landing?.Landed,
            part.Landing?.Message,
            part.Landing?.Branch,
            Plugin = part.Landing?.Plugin is { } said ? new { Id = said.Plugin, said.Pushed, said.PullRequest, said.Message, said.Failed } : null,
            LeftOut = TreesRoutes.LeftOut(part.Landing?.LeftOut),
            Conflict = TreesRoutes.Conflict(part.Landing?.Conflict),
        }).ToArray(),
    };

    /// <summary>The entries asked about, from one look whose git walk is only theirs, and their parts' rows.</summary>
    private async Task<(IReadOnlyList<SessionEntry> Entries, IReadOnlyList<SessionGrouping> Rows)> LookAsync(
        ServiceClient service, Func<SessionEntry, bool> which, CancellationToken cancellationToken)
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        // No tree is walked to find the entries (they are the records'); then only their parts' trees are.
        var look = await SessionGroups.LookAsync(service, config, _context.Door(config), _loop.Look.Latest, _loop.Home, [], cancellationToken)
            .ConfigureAwait(false);
        look = look with { Waits = _loop.Look.Waits };
        var entries = SessionGroups.ReadEntries(look).Where(which).ToList();
        var ids = entries.SelectMany(entry => entry.Parts.Select(part => part.Session)).ToList();
        var trees = new SessionTrees(_loop.Home);
        look = await SessionGroups.JudgeAsync(look, trees.Holds, trees.WorkAsync, ids, cancellationToken).ConfigureAwait(false);
        return (entries, SessionGroups.Read(look, ids));
    }

    private async Task<object> PlanAsync(ServiceClient service, SessionEntry entry, IReadOnlyList<SessionGrouping> rows, CancellationToken cancellationToken)
    {
        var (trees, subject, parts) = await PartsAsync(service, entry, rows, cancellationToken).ConfigureAwait(false);
        if (parts.Count == 0) return Plan(entry.Key, entry.Repository, new EntryPlan(LandingForm.Branch, "", []));
        var plan = await trees.PlanEntryAsync(parts, subject, cancellationToken).ConfigureAwait(false);
        var held = parts.Where(part => part.Gate?.Refusal is not null).ToDictionary(part => part.Session, part => part.Gate!.Refusal!.Message, StringComparer.Ordinal);
        return Plan(entry.Key, entry.Repository, plan, held);
    }

    private async Task<object> LandAsync(
        ServiceClient service, SessionEntry entry, IReadOnlyList<SessionGrouping> rows, IReadOnlySet<string> leaveOut, CancellationToken cancellationToken) =>
        Landed(entry.Key, entry.Repository, await LandingAsync(service, entry, rows, leaveOut, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// The press on one entry: each part's gate counted as its own press counts it, the landing, then what each landed part's
    /// conversation keeps of it and what a held gate writes to the machine log, as <c>LAND_SESSION_TREE</c> does for one.
    /// </summary>
    private async Task<EntryLanding> LandingAsync(
        ServiceClient service, SessionEntry entry, IReadOnlyList<SessionGrouping> rows, IReadOnlySet<string> leaveOut, CancellationToken cancellationToken)
    {
        var (trees, subject, parts) = await PartsAsync(service, entry, rows, cancellationToken).ConfigureAwait(false);
        if (parts.Count == 0) return new EntryLanding(false, "Nothing was landed: no part of this entry has work to land on this machine.", null, []);
        var presses = new OpinionPresses(_loop.Home, service);
        var pressed = new List<EntryLandingPart>(parts.Count);
        foreach (var part in parts)
        {
            var quest = entry.Parts.First(each => each.Session == part.Session).Quest;
            pressed.Add(part with { Gate = await presses.PressedAsync(part.Session, part.Gate!, null, part.Tree, quest, cancellationToken).ConfigureAwait(false) });
        }

        var landed = await trees.LandEntryAsync(pressed, subject, leaveOut, cancellationToken, async token => await LinesRoutes.InUseAsync(service, token))
            .ConfigureAwait(false);
        foreach (var part in landed.Parts)
        {
            if (part.Landing is not { } its) continue;
            if (its.Landed) _loop.Events.Keep(part.Session, LandingRules.Note(its), line => _loop.Output.Append(part.Session, line));
            var gate = pressed.First(each => each.Session == part.Session);
            if (_loop.Log is { } log && LandingGateWords.Held(part.Session, trees.Owner(gate.Tree), gate.Gate!, its, ReviewDoors.Screen) is { } held)
            {
                SessionLog.WriteLanding(log, held);
            }
        }

        return landed;
    }

    /// <summary>
    /// The entry's parts with work, each with its tree here and its gate, and the subject the entry is named for: its first part's,
    /// the chain's first quest walked up as a chain's is (WSR5), so every part lands on the branch named for it.
    /// </summary>
    private async Task<(SessionTrees Trees, LandingSubject Subject, IReadOnlyList<EntryLandingPart> Parts)> PartsAsync(
        ServiceClient service, SessionEntry entry, IReadOnlyList<SessionGrouping> rows, CancellationToken cancellationToken)
    {
        // A rule's plugin says its lines on the console under its name, as a landing's do (D64 §4, D100).
        var trees = new SessionTrees(_loop.Home, new LandingPlugins(
            _loop.Home, say: (plugin, line) => _loop.Output.Append($"plugin:{plugin}", line), log: _loop.Log, health: _loop.Health));
        var parts = new List<EntryLandingPart>();
        foreach (var part in PartsOf(entry, rows))
        {
            var (tree, _) = await service.SessionGroundAsync(part.Session, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tree)) continue;
            var gate = await trees.GateAsync(tree, part.Quest, new ServiceReviewWorld(service), part.Session, cancellationToken).ConfigureAwait(false);
            parts.Add(new EntryLandingPart(part.Session, tree) { Gate = gate });
        }

        var lead = entry.Parts.Count > 0 ? entry.Parts[0] : null;
        var subject = lead is null ? new LandingSubject(entry.Key, null, null)
            : await LandingRules.SubjectAsync(lead.Session, lead.Quest, quest => service.FindQuestAsync(quest, cancellationToken),
                _loop.Events.Openings([lead.Session]).GetValueOrDefault(lead.Session)).ConfigureAwait(false);
        return (trees, subject, parts);
    }

    /// <summary>The parts the person chose to leave out (point 3), as the page sends them; none where it sends none.</summary>
    private static IReadOnlySet<string> LeaveOut(IpcRequest request) =>
        request.Payload is { } payload && payload.TryGetProperty("leaveOut", out var named) && named.ValueKind == JsonValueKind.Array
            ? named.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
}
