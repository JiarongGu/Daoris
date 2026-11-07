using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// What a line of work (an entry) and its ask do as one, the page's `bridge/entries.ts`: an entry's one Accept and an ask's,
/// the plan before the press and the press, over the driver's
/// <see cref="SessionTrees.PlanEntryAsync"/>. Each part lands by its own landing, its
/// gate read as its review's Accept reads it.
/// </summary>
/// <remarks>
/// <para><b>Which parts.</b> An entry's parts whose tree offers work to land (<see cref="SessionGrouping.Lands"/>),
/// oldest first, as the Parts strip shows them; an ask's entries are its own, one per repository, never its intake.</para>
///
/// <para><b>Nothing machine-local comes back</b>: ids, names of branches and files, and the landing's sentences, as
/// <c>LAND_SESSION_TREE</c>'s.</para>
/// </remarks>
public sealed class EntriesRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>What an entry's landing changes: what each of its parts' landings does, for every session it names.</summary>
    private const StateScopes Lands = StateScopes.Sessions | StateScopes.Landing | StateScopes.Diff
        | StateScopes.Sweep | StateScopes.TreesSync;

    /// <summary>
    /// An entry's Accept (point 1): <c>ENTRY_LANDING</c> says where its parts would land, each part's plan and what superseded it;
    /// <c>LAND_ENTRY</c> lands them, leaving out only the parts the page sends in <c>leaveOut</c> (point 3).
    /// </summary>
    [DriverRoute("ENTRY_LANDING", PipeClass.Open)]
    private async Task<object?> EntryLandingAsync(EntryLandingRead press, CancellationToken cancellationToken)
    {
        var key = press.Entry;
        var (service, entries, rows, entry) = await EntryLookAsync(key, cancellationToken).ConfigureAwait(false);
        // `trees land --entry --plan` asks for the verb's words (`said`) where the page asks for the plan's fields.
        return press.Said
            ? await PlanSaidAsync(service, entries.Take(1).ToList(), rows, isAsk: false, key, cancellationToken).ConfigureAwait(false)
            : await PlanAsync(service, entry, rows, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(ServiceClient Service, IReadOnlyList<SessionEntry> Entries, IReadOnlyList<SessionGrouping> Rows, SessionEntry Entry)> EntryLookAsync(
        string key, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var (entries, rows) = await LookAsync(service, entry => entry.Key == key, cancellationToken).ConfigureAwait(false);
        return (service, entries, rows, entries.FirstOrDefault() ?? new SessionEntry(key, "", "", SessionGroup.Ended, "", []));
    }

    [DriverRoute("LAND_ENTRY", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("LAND_ENTRY", Entry = "entry")]
    [GroundRead("LAND_ENTRY", nameof(PipeReads.EntryLanding))]
    [Publishes("LAND_ENTRY", Lands, Subject = "entry")]
    private async Task<object?> LandEntryAsync(LandEntryPress press, CancellationToken cancellationToken)
    {
        var key = press.Entry;
        var (service, entries, rows, entry) = await EntryLookAsync(key, cancellationToken).ConfigureAwait(false);

        // What `trees land --entry` prints rides beside the page's fields; a refusal the driver throws is its sentence, as there.
        var plugins = new List<string>();
        var landed = await LandingAsync(Accepts(service, plugins), entry, rows, LeaveOut(press.LeaveOut), cancellationToken).ConfigureAwait(false);
        _loop.Nudge();
        // The lines it moved are registered at once where the verb asks (`follow`), said after its words; none where nothing landed.
        var followed = entries.Count > 0 ? await FollowAsync(press.Follow, service, cancellationToken).ConfigureAwait(false) : null;
        var said = entries.Count == 0 ? TreesWords.EntryNone(isAsk: false, key) : TreesWords.EntryLand([(entry.Repository, landed)], plugins).Followed(followed);
        return LandedSaid(entry.Key, entry.Repository, landed, said, followed);
    }

    /// <summary>
    /// An ask's Accept (point 4): <c>ASK_LANDING</c> is each of its entries' plans; <c>LAND_ASK</c> lands each, one after another,
    /// in its own repository, by its own rule and gates. The answer is per repository: a refusal holds only its own, and the others
    /// land.
    /// </summary>
    [DriverRoute("ASK_LANDING", PipeClass.Open)]
    private async Task<object?> AskLandingAsync(AskLandingRead press, CancellationToken cancellationToken)
    {
        var ask = press.Ask;
        var (service, entries, rows, own) = await AskLookAsync(ask, cancellationToken).ConfigureAwait(false);
        if (press.Said)
        {
            return await PlanSaidAsync(
                service, [.. own.Where(entry => PartsOf(entry, rows).Count > 0)], rows, isAsk: true, ask, cancellationToken).ConfigureAwait(false);
        }

        var plans = new List<object>();
        foreach (var entry in own.Where(entry => PartsOf(entry, rows).Count > 0))
        {
            plans.Add(await PlanAsync(service, entry, rows, cancellationToken).ConfigureAwait(false));
        }

        return new { Ask = ask, Entries = plans };
    }

    private async Task<(ServiceClient Service, IReadOnlyList<SessionEntry> Entries, IReadOnlyList<SessionGrouping> Rows, IReadOnlyList<SessionEntry> Own)> AskLookAsync(
        string ask, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var (entries, rows) = await LookAsync(service, entry => entry.Origin == $"ask:{ask}", cancellationToken).ConfigureAwait(false);
        return (service, entries, rows, EntriesOf(ask, entries));
    }

    [DriverRoute("LAND_ASK", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("LAND_ASK", Ask = "ask")]
    [GroundAsk("LAND_ASK", "ask")]
    [GroundRead("LAND_ASK", nameof(PipeReads.AskLanding))]
    [Says("LAND_ASK", "ask:ask")]
    [Publishes("LAND_ASK", Lands, Subject = "ask")]
    private async Task<object?> LandAskAsync(LandAskPress press, CancellationToken cancellationToken)
    {
        var ask = press.Ask;
        var (service, _, rows, own) = await AskLookAsync(ask, cancellationToken).ConfigureAwait(false);
        var leaveOut = LeaveOut(press.LeaveOut);
        var answers = new List<object>();
        var landings = new List<(string Repository, EntryLanding Landed)>();
        var plugins = new List<string>();
        var accepts = Accepts(service, plugins);
        var done = true;
        foreach (var entry in own.Where(entry => PartsOf(entry, rows).Count > 0))
        {
            // One repository's refusal, said or thrown, holds only its own entry (point 4): the next is landed all the same.
            EntryLanding landed;
            try
            {
                landed = await LandingAsync(accepts, entry, rows, leaveOut, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                landed = TreesWords.Unlanded(error.Message);
            }

            done &= landed.Landed;
            landings.Add((entry.Repository, landed));
            answers.Add(Landed(entry.Key, entry.Repository, landed));
        }

        _loop.Nudge();
        // What `trees land --ask` prints beside the page's fields, or its sentence where no entry of the ask has work to land.
        var followed = landings.Count > 0 ? await FollowAsync(press.Follow, service, cancellationToken).ConfigureAwait(false) : null;
        var said = landings.Count == 0 ? TreesWords.EntryNone(isAsk: true, ask) : TreesWords.EntryLand(landings, plugins).Followed(followed);
        return new { Ask = ask, Done = done && answers.Count > 0, Repositories = answers, Said = said.ToWire(), Followed = followed?.Out ?? [] };
    }

    /// <summary>The lines a landing moved, registered from the line at once where the verb asks (<c>follow</c>), as the page's next look would.</summary>
    private async Task<FollowedSaid?> FollowAsync(bool follow, ServiceClient service, CancellationToken cancellationToken) =>
        follow
            ? await TreesWords.FollowMovedAsync(service, _loop.Home, () => DriverConfig.Load(_loop.ConfigPath), _loop.Log, cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>The parts of <paramref name="entry"/> whose tree offers work to land, in the entry's order: what its Accept lands.</summary>
    public static IReadOnlyList<EntryPart> PartsOf(SessionEntry entry, IReadOnlyList<SessionGrouping> rows) => EntryAccepts.PartsOf(entry, rows);

    /// <summary>An ask's own entries (point 4): one per repository, by its name, never its intake.</summary>
    public static IReadOnlyList<SessionEntry> EntriesOf(string ask, IReadOnlyList<SessionEntry> entries) => EntryAccepts.EntriesOf(ask, entries);

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
            Clash = part.Clash is { } clash ? new { clash.By, clash.Says } : null,
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
        Parts = Parts(landed),
    };

    /// <summary><c>LAND_ENTRY</c>'s answer: <see cref="Landed"/>'s fields, and the words <c>trees land --entry</c> prints beside them.</summary>
    public static object LandedSaid(string entry, string repository, EntryLanding landed, TreesSaid said, FollowedSaid? followed = null) => new
    {
        Entry = entry,
        Repository = repository,
        Done = landed.Landed,
        landed.Message,
        landed.Branch,
        Parts = Parts(landed),
        Said = said.ToWire(),
        Followed = followed?.Out ?? [],
    };

    private static object[] Parts(EntryLanding landed) => landed.Parts.Select(part => (object)new
    {
        part.Session,
        part.Outcome,
        Done = part.Landing?.Landed,
        part.Landing?.Message,
        part.Landing?.Branch,
        LeftOut = TreesRoutes.LeftOut(part.Landing?.LeftOut),
        Conflict = TreesRoutes.Conflict(part.Landing?.Conflict),
    }).ToArray();

    /// <summary>The entries asked about, from one look whose git walk is only theirs, and their parts' rows.</summary>
    private async Task<(IReadOnlyList<SessionEntry> Entries, IReadOnlyList<SessionGrouping> Rows)> LookAsync(
        ServiceClient service, Func<SessionEntry, bool> which, CancellationToken cancellationToken)
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        return await SessionEntries.LookAsync(
            service, config, _context.Unadopted(config), _loop.Look.Latest, _loop.Look.Waits, _loop.Home, which, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The library's entry Accept over this loop's surface, which the terminal's <c>trees land --entry</c> builds over its
    /// own: a plugin says its lines on the console under its name, as a landing's do.
    /// </summary>
    /// <param name="plugins">Where a plugin's lines are also kept, for the answer's words; null keeps none.</param>
    private EntryAccepts Accepts(ServiceClient service, List<string>? plugins = null) => new(
        service, _loop.Home,
        new SessionTrees(_loop.Home, new LandingPlugins(_loop.Home, say: SayTo(plugins), log: _loop.Log, health: _loop.Health)),
        _loop.Events)
    {
        Performs = (session, unit, tip, token) => PerformedWaits.AwaitAsync(_loop, session, unit, tip, token),
    };

    /// <summary>A plugin's lines, said on the loop's output under its name and kept in <paramref name="lines"/> as the terminal says them.</summary>
    private Action<string, string> SayTo(List<string>? lines) => (plugin, line) =>
    {
        lines?.Add(TreesWords.PluginLine(plugin, line));
        _loop.Output.Append($"plugin:{plugin}", line);
    };

    private async Task<object> PlanAsync(ServiceClient service, SessionEntry entry, IReadOnlyList<SessionGrouping> rows, CancellationToken cancellationToken)
    {
        var (plan, held) = await Accepts(service).PlanAsync(entry, rows, cancellationToken).ConfigureAwait(false);
        return Plan(entry.Key, entry.Repository, plan, held);
    }

    /// <summary>
    /// What <c>trees land --entry</c> and <c>--ask</c> print for <c>--plan</c>: each entry's plan in the verb's lines, or its
    /// sentence where none has work to land here, beside nothing of the page's fields. The page does not ask for it.
    /// </summary>
    private async Task<object> PlanSaidAsync(
        ServiceClient service, IReadOnlyList<SessionEntry> entries, IReadOnlyList<SessionGrouping> rows, bool isAsk, string id,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0) return new { Said = TreesWords.EntryNone(isAsk, id).ToWire() };
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var (plan, held) = await Accepts(service).PlanAsync(entry, rows, cancellationToken).ConfigureAwait(false);
            lines.AddRange(TreesPlanWords.EntryPlanned(entry, plan, held));
        }

        return new { Said = TreesPlanWords.EntryPlan(lines).ToWire() };
    }

    /// <summary>The press on one entry, which the terminal's lands too (<see cref="EntryAccepts.LandAsync"/>).</summary>
    private static async Task<EntryLanding> LandingAsync(
        EntryAccepts accepts, SessionEntry entry, IReadOnlyList<SessionGrouping> rows, IReadOnlySet<string> leaveOut, CancellationToken cancellationToken)
    {
        try
        {
            return await accepts.LandAsync(entry, rows, leaveOut, cancellationToken).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return TreesWords.Unlanded(error.Message);
        }
    }

    /// <summary>The parts the person chose to leave out (point 3), as the page sends them; none where it sends none.</summary>
    private static IReadOnlySet<string> LeaveOut(IReadOnlyList<string>? named) => (named ?? []).ToHashSet(StringComparer.Ordinal);
}
