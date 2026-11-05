using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// What the run makes of each look the watch reports (<see cref="LoopRun"/>): its news kept and told, what it judged kept whole on
/// the loop for the screens and slices that read it, and the tick forwarded only where something it says moved, since every tick the
/// page receives asks its lists again, and a machine with one held quest would otherwise send one every poll.
/// </summary>
/// <remarks>Called by the watch alone, one look at a time, so the signatures it keeps hold no lock.</remarks>
internal sealed class LookTold(
    DriverLoop loop, LoopState shared, IEventBus eventBus, ServiceClient service, ServiceHttp doors, News news,
    CancellationToken ct)
{
    private readonly AttentionWatch _attention = new();

    // A quest's park is said with its last session's facts, and Overview's row waits from its end.
    private readonly QuestParkReader _parks = new();
    private readonly TickErrors _failures = new();
    private string? _considered;
    private string _waited = "";
    private string _judged = "";
    private string _said = "";
    private string? _asked;
    private string? _active;
    private string? _registered;

    /// <summary>One look's report, with the choices it ran under.</summary>
    public async Task ToldAsync(TickReport report, DriverConfig ticked)
    {
        _failures.Ran();

        // Observed either way, so turning notifications back on does not then announce everything that happened while they were off.
        var read = await _parks.LookAsync(
            report, ticked.ForgivenAt, token => service.SessionList.Kept.JsonAsync(token), ct)
            .ConfigureAwait(false);
        // Told once across restarts by the said-set, not by a first look's baseline.
        var attend = AttentionNews.Told(news, _attention.Observe(report, read), DateTimeOffset.UtcNow);
        // Kept in Notifications whatever the switch says: recording is not telling.
        loop.Notices.Keep(attend);
        foreach (var item in attend.Where(each => NotifyRule.Tells(each, ticked.Notify))) await AttentionTold.TellAsync(eventBus, item).ConfigureAwait(false);

        // Forwarded when the look did something or what it says changed: the considerations, a standing answer the Overview renders;
        // the parks read again, since a park read a look after its verdict still has to reach the row its wait is said on; the waits,
        // which an intake held on its accounts moves with no consideration; what waits on the person at a second opinion; and the
        // look's lines, where one said again at every look is no change.
        var considered = Considerations.Signature(report.Considerations);
        var changed = considered != _considered || read is not null;
        _considered = considered;
        var waited = TickSignatures.Waits(report.Waits);
        changed |= waited != _waited;
        _waited = waited;
        var opinions = report.JudgedOpinions.Cast<OpinionJudged>().ToList();
        var judged = TickSignatures.Judged(opinions);
        changed |= judged != _judged;
        _judged = judged;
        var said = TickSignatures.Lines(report.Events);
        changed |= said != _said;
        _said = said;

        Keep(report, opinions);

        // The asks, the sessions and the registry, which another door moves with nothing above moving: an ask a terminal or a
        // teammate's sync made, a conversation started or ended from the main window, a folder imported from a terminal. A host that
        // does not answer changes nothing.
        var asked = await AskedAsync(service, ct).ConfigureAwait(false);
        if (asked is not null && asked != _asked)
        {
            changed = true;
            _asked = asked;
        }

        var active = await ActiveAsync(service, ct).ConfigureAwait(false);
        if (active is not null && active != _active)
        {
            changed = true;
            _active = active;
        }

        var registered = await RegisteredAsync(ct).ConfigureAwait(false);
        var registryMoved = false;
        if (RegistryHeard(loop.Facts, registered, _registered))
        {
            changed = true;
            registryMoved = _registered is not null;
            _registered = registered;
        }

        // What this look moved of the rules, the registry and the knowledge index, told as the scopes' own changes: the page's tick
        // asks none of them again, since a press's change reaches it the same way.
        var indexMoved = loop.IndexRead.Moved(await IndexFeed.ReadAsync(doors, ct).ConfigureAwait(false));
        if (Scopes(loop.RulesRead.Read(loop.Home).Moved, registryMoved, indexMoved) is var moved && moved != StateScopes.None)
        {
            StatePublisher.Publish(eventBus, moved);
        }

        if (report.PlannedAnything || changed)
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_TICK", new
            {
                // What the look considered, the trust holds and the waits are the considerations' slice's, pushed when they move; the
                // tick says only that something moved.
                Events = report.Events,
            }).ConfigureAwait(false);
        }
    }

    /// <summary>A failed look, said once until a look runs again, and named where the driver can name it.</summary>
    public Task FailedAsync(Exception error) => _failures.Failed(error) is { } said
        ? eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { said.Code, said.Message })
        : Task.CompletedTask;

    /// <summary>
    /// What a look moved that the page reads by pull or a slice reads by scope, as scopes: the rules, where their reading moved (a
    /// retry line said again each look moves nothing); the registry, where its signature moved; and the repositories, where the
    /// knowledge index fed further. Each first reading is the page's own.
    /// </summary>
    public static StateScopes Scopes(bool rulesMoved, bool registryMoved, bool indexMoved) =>
        (rulesMoved ? StateScopes.Rules : StateScopes.None)
        | (registryMoved ? StateScopes.Registry : StateScopes.None)
        | (indexMoved ? StateScopes.Repositories : StateScopes.None);

    /// <summary>
    /// Whether the registry's signature a look read is a change. A change also wakes the machine's facts: a repository imported from a
    /// terminal moves no ledger family, so the pushed lines saw it only at the facts' deadline. The first signature heard (null before)
    /// is not a change to wake for.
    /// </summary>
    public static bool RegistryHeard(MachineFacts facts, string? registered, string? last)
    {
        if (registered is null || registered == last) return false;
        if (last is not null) facts.Wake(HelpFacts.Machine, HelpFacts.Whole);
        return true;
    }

    /// <summary>
    /// What the look judged, kept whole on the loop whether or not the tick is forwarded, so a grant, a retry or a group is judged
    /// against what the look held and nothing it stopped holding; then the slices that judge it asked to, each pushing only what moved.
    /// </summary>
    private void Keep(TickReport report, List<OpinionJudged> opinions)
    {
        // What the look held for the harness's trust: the only grants the screen may confirm.
        loop.Trust.Record(report.Untrusted);
        // What it parked by its strikes, so Ask Daoris proposes a retry only of a quest the drawer would offer Retry on.
        loop.Parked.Record(report.Considerations);
        // Every verdict, for the session list's groups, with the cool-offs it held starts on.
        loop.Look.Record(report.Considerations, report.Waits);
        loop.Judged.Record(opinions);
        // The considerations as the page reads them, each with its park and the wait holding it on a cooling account.
        var sitting = report.Considerations
            .Select(consideration => TickShown.Quest(
                consideration,
                _parks.Latest.FirstOrDefault(park =>
                    string.Equals(park.Quest, consideration.Quest.Id, StringComparison.OrdinalIgnoreCase)),
                report.Waits.FirstOrDefault(wait =>
                    wait.Quests.Contains(consideration.Quest.Id, StringComparer.OrdinalIgnoreCase))))
            .ToArray();
        loop.RecordConsidered(sitting);
        // The driver's state judged again: what runs and what waits for review move at a look, and a terminal's edit of `driver.json`
        // is read here. A wake only asks; the slice's worker judges.
        loop.DriverSlice.Wake();
        // Who needs the person: what the look kept above and the helpers' asks it wrote, read at the slice's judgement.
        loop.Slices.Find(AttentionStateSlice.Name)?.Wake();
    }

    /// <summary>What the asks say this look, or null when the host could not answer — never a change.</summary>
    private static async Task<string?> AskedAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return Asks.Signature(await service.AsksAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Which sessions are active and how, or null when the host could not answer — never a change.</summary>
    private static async Task<string?> ActiveAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return ActiveSessions.Signature(await service.ActiveSessionsAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// What the registry holds, or null when the host could not answer — never a change. The workspace it places each repository in is
    /// kept for the standing judge (<see cref="LoopState.Placed"/>), and the first answer, and a lane moving, tell the at-work judge,
    /// since the hint's standing waits on the registry.
    /// </summary>
    private async Task<string?> RegisteredAsync(CancellationToken ct)
    {
        try
        {
            var registry = await service.RegistryAsync(ct).ConfigureAwait(false);
            var placed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var each in registry)
            {
                if (each.Workspace is { Length: > 0 } workspace) placed.TryAdd(each.Repository, workspace);
            }

            shared.Placed = placed;
            if (loop.HintStanding.Hear(registry)) loop.AtWork.JudgeStandings();
            return Repositories.Signature(registry);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
