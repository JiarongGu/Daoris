namespace Daoris.Driver;

/// <summary>
/// The codes a start's walk passes an entry of its role's order by (D205 §4.1, §4.5; FOUNDATION1j, the foundation design §2.4): one
/// vocabulary for every walk over a role's order, said in the wait's sentence, the opening note's actor line and the machine log's
/// <c>actor.passed</c> and <c>actor.chose</c> words (<see cref="BuildPass.Word"/>).
/// </summary>
public static class StartPasses
{
    /// <summary>No agent of that name on this machine, or one that cannot do the start's work: passed before any account is asked.</summary>
    public const string Unavailable = "unavailable";

    /// <summary>Its harness has no mode for the start's permission prompts: passed before any account is asked.</summary>
    public const string NoPosture = "no-posture";

    /// <summary>The pipe door, for a repository that has not adopted Daoris: passed before any account is asked.</summary>
    public const string NoConnector = "no-connector";

    /// <summary>Every account that may run it was passed for the entry's model (D205 §3.3): never a cool-off.</summary>
    public const string NotOffered = AgentOffers.NotOffered;

    /// <summary>Its accounts wait for a cool-off (D125).</summary>
    public const string Cooling = "cooling";

    /// <summary>The accounts it may use are not signed in (ROSTER1).</summary>
    public const string SignedOut = "signed-out";

    /// <summary>Its account walk held it for anything else, its own sentence saying what.</summary>
    public const string Held = "held";

    /// <summary>Every code, the closed set.</summary>
    public static IReadOnlyList<string> Codes { get; } = [Unavailable, NoPosture, NoConnector, NotOffered, Cooling, SignedOut, Held];

    /// <summary>
    /// The code of an entry whose account walk held, read from the walk's facts and never its words: a model not offered first, since the
    /// next entry may still run on the same accounts; then a cool-off, which waits for a time; then a sign-in; else held.
    /// </summary>
    public static string Of(HarnessSelection selection) =>
        selection.NotOffered is not null ? NotOffered
        : selection.Cooling is not null ? Cooling
        : selection.SignedOut is not null ? SignedOut
        : Held;
}

/// <summary>
/// A start plan's answer (the foundation design §2.4, FOUNDATION1j): the role whose order was walked, the entry taken (null where the start
/// waits), the account walk's selection it opens on, and each entry passed before it, in order, with its code. The start opens on it as
/// settled: no caller walks again after it, and a re-ask at the open is a probe that takes no count of its own.
/// </summary>
/// <param name="Role">The role whose effective order was walked.</param>
/// <param name="Selection">The account walk's answer for the entry taken, its account and harness; where none can run, the wait's.</param>
/// <param name="Chosen">The entry taken, with its agent, model and effort, and the choice its record keeps; null where the start waits.</param>
/// <param name="Passes">Each entry passed before it, in order, with its code (<see cref="StartPasses"/>).</param>
public sealed record SettledSelection(string Role, HarnessSelection Selection, ActorEffective? Chosen, IReadOnlyList<BuildPass> Passes)
{
    /// <summary>The taken entry's agent as the walk resolved it; null for an order of one entry, whose agent the account walk is asked by name.</summary>
    public ISessionAdapter? Adapter { get; init; }

    /// <summary>
    /// The start's one count on its account (<see cref="HarnessSelection.Number"/>), taken by the roster at the selection under its gate, so
    /// the next start in the same look spreads (D197; PARALLEL1c, D181 point 3); 0 for a probe and for an agent with no accounts. A refusal at
    /// the open gives it back once (<see cref="HarnessRoster.Release"/>).
    /// </summary>
    public long Reservation => Selection.Number;
}

/// <summary>
/// One start's plan (the foundation design §2.4, FOUNDATION1j part i): the seeded role of the start's work (<see cref="RoleOf"/>), that role's
/// effective order (<see cref="ActorChoices.Effective"/>) read once for the start, and the one walk over it, choice by choice, D197's account
/// walk inside each choice before the next. Its answer is a <see cref="SettledSelection"/>.
/// </summary>
/// <remarks>
/// <para><b>The walk</b> (D205 §4.1): an entry naming no agent of this machine is passed <c>unavailable</c>, and one the caller's
/// <see cref="Gate"/> refuses by the gate's code, each before any account is asked; every other entry's accounts are asked, judged for its
/// model, and the first whose walk allows a start is taken. With none, the start holds on the wait's sentence, in the role's words (§4.5),
/// the soonest cool-off beside it. An order of one entry is today's walk exactly: its one account walk asked, its own hold or refusal
/// unchanged, nothing passed or said.</para>
/// <para><b>The reservation</b>: the plan takes no count of its own. Each account walk it asks counts the start where it chooses an
/// account, under the roster's gate, and only where the start counts; a choice whose walk held counted nothing. So a start that counts
/// holds exactly one reservation, the taken choice's, carried on its settled selection; a probe (<c>counts: false</c>, a re-ask among
/// them) holds none, says no pass and remembers none (<see cref="PassMemory"/>).</para>
/// <para><b>The role</b> is the work's seeded role and its order today's actor choices: no role catalogue is read, which row x adopts
/// (C5-3.6).</para>
/// </remarks>
internal sealed class StartPlan
{
    private readonly DriverConfig _config;
    private readonly HarnessRoster _roster;
    private readonly AdapterSet _adapters;
    private readonly PassMemory _memory;

    /// <param name="work">The start's work, one of the participant's work codes (<c>build</c>, <c>intake</c>, …).</param>
    /// <param name="config">The configuration this start reads: its role's order and the account walk's.</param>
    /// <param name="roster">The account walk, which counts a start where it chooses its account.</param>
    /// <param name="adapters">The machine's agents, by which an entry's agent is known and resolved.</param>
    /// <param name="memory">What each walk last passed, so a pass is said when it is new.</param>
    /// <exception cref="ActorUnreadableException">The role's stored entry does not read: the start that reads it holds.</exception>
    public StartPlan(string work, DriverConfig config, HarnessRoster roster, AdapterSet adapters, PassMemory memory)
    {
        Work = work;
        Role = RoleOf(work);
        Order = ActorChoices.Effective(Role, config);
        (_config, _roster, _adapters, _memory) = (config, roster, adapters, memory);
    }

    /// <summary>The start's work.</summary>
    public string Work { get; }

    /// <summary>The role whose order the start walks: the work's seeded role.</summary>
    public string Role { get; }

    /// <summary>The role's effective order, read once for this start: its walk and its re-ask at the open read the same.</summary>
    public IReadOnlyList<ActorEffective> Order { get; }

    /// <summary>The repository's circle, for its pinned version (D49 §4).</summary>
    public string? Workspace { get; init; }

    /// <summary>Driven work or a conversation: driven work never starts on an account kept for conversations (D130 §4.6).</summary>
    public StartKind Kind { get; init; } = StartKind.Driven;

    /// <summary>Whose passes are remembered between walks (<see cref="PassMemory"/>): <c>quest:&lt;id&gt;</c>, <c>ask:&lt;id&gt;</c>.</summary>
    public required string Subject { get; init; }

    /// <summary>What the machine log's lines name beside the role, as <c>("quest", id)</c>; none where the start has nothing to name yet.</summary>
    public (string Key, object? Value)? Named { get; init; }

    /// <summary>The machine log the walk's <c>actor.chose</c>, <c>actor.passed</c> and <c>actor.reasked</c> lines go to; none says nothing.</summary>
    public MachineLog? Log { get; init; }

    /// <summary>
    /// The caller's own reason to pass an entry whose agent this machine has, before any of its accounts is asked: a code of
    /// <see cref="StartPasses"/> and the sentence that says why, or null where its accounts are asked.
    /// </summary>
    public Func<ActorEffective, ISessionAdapter, (string Code, string Why)?>? Gate { get; init; }

    /// <summary>
    /// The seeded role of a work (the foundation design §2.2's table; C5-3.6): the role whose order a start of that work walks until row x reads
    /// the role catalogue. Design work walks Build's while no role takes it. The works are spelled here as the participant's work codes
    /// spell them, since Roles sits below Participants; <c>StartPlanTests</c> holds the two alike.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">No work of that name.</exception>
    public static string RoleOf(string work) => work switch
    {
        "build" or "design" => "build",
        "chat" => "chat",
        "ask-daoris" => "ask-daoris",
        "intake" => "intake",
        "help" => "helper",
        "partner" => "partner",
        "review" => "opinion",
        "brainstorm" => "brainstorm",
        _ => throw new ArgumentOutOfRangeException(nameof(work), work, "no work of that name: a start's work is one of the participant's work codes"),
    };

    /// <summary>
    /// The plan's walk (D205 §4.1), its answer the settled selection the start opens on. For the start's re-ask at the open (§3.3, §3.5(f)):
    /// <paramref name="refused"/>, the entry and account its agent refused, passed as if known before the spawn, and
    /// <paramref name="counts"/> false, so it takes no count of its own and ranks without the start's own (<paramref name="except"/>).
    /// </summary>
    public async Task<SettledSelection> ResolveAsync(
        CancellationToken ct, bool counts = true, (ActorEffective Entry, string? Account)? refused = null, long except = 0)
    {
        IReadOnlyCollection<string>? PassedFor(ActorEffective entry) =>
            refused is { Entry: var at, Account: { } account } && at == entry ? [account] : null;
        if (Order.Count == 1)
        {
            var only = await _roster
                .SelectAsync(Order[0].Agent, _config, Workspace, chosen: null, Kind, ct, Order[0].Model, PassedFor(Order[0]), except, counts)
                .ConfigureAwait(false);
            return new SettledSelection(Role, only, Order[0], []);
        }

        var passes = new List<BuildPass>();
        HarnessSelection? held = null;
        CoolingEntry? soonest = null;
        using var walk = _memory.Walk(Subject, counts);
        foreach (var entry in Order)
        {
            string? code = null;
            string? why = null;
            ISessionAdapter? adapter = null;
            if (!_adapters.Names.Contains(entry.Agent, StringComparer.OrdinalIgnoreCase))
            {
                (code, why) = (StartPasses.Unavailable, $"`{entry.Agent}` is not an agent on this machine");
            }
            else
            {
                adapter = _adapters.Resolve(entry.Agent);
                if (Gate?.Invoke(entry, adapter) is { } gated) (code, why) = gated;
            }

            if (code is null)
            {
                var selection = await _roster
                    .SelectAsync(entry.Agent, _config, Workspace, chosen: null, Kind, ct, entry.Model, PassedFor(entry), except, counts)
                    .ConfigureAwait(false);
                if (selection.Allowed)
                {
                    if (counts)
                    {
                        Log?.Write("info", "actor.chose", Line(("choice", entry.From), ("agent", entry.Agent), ("model", entry.Model),
                            ("passed", passes.Select(pass => pass.Word).ToList())));
                    }

                    return new SettledSelection(Role, selection, entry, passes) { Adapter = adapter };
                }

                held ??= selection;
                if (selection.Cooling is { } cooling && (soonest is null || cooling.Until < soonest.Until)) soonest = cooling;
                // An entry whose every ready account does not offer its model is passed `not-offered` (D205 §3.3), never cooled.
                (code, why) = (StartPasses.Of(selection), selection.Refusal);
            }

            var thisPass = new BuildPass(entry, code, why);
            passes.Add(thisPass);
            if (walk.Fresh(thisPass)) Log?.Write("info", "actor.passed", Line(("choice", entry.From), ("agent", entry.Agent), ("code", code)));
        }

        // The wait's sentence (D205 §4.5): judged again at every look, the first choice that can run, in order, taken.
        var wait = $"{ActorWords.Name(Role)} waits: none of its choices can run now. {string.Join("; ", passes.Select(pass => pass.Said))}.";
        return new SettledSelection(Role, (held ?? new HarnessSelection(wait)) with { Refusal = wait, Cooling = soonest }, null, passes);
    }

    /// <summary>
    /// The start's re-ask where its agent refused the entry's model at the open (D205 §3.3, §3.5(f)): the plan walked again as if the refusal
    /// had been known before the spawn, a probe ranking without the start's own count (<paramref name="selection"/>'s reservation). Its
    /// answer is a later entry to set on the same session where the walk names the same agent on the same account
    /// (<see cref="ReaskMove.Same"/>); anything else is null, and the opened record ends <c>choice-not-offered</c>.
    /// </summary>
    /// <param name="refused">The entry the open's agent refused.</param>
    /// <param name="selection">The selection the start opened on.</param>
    public async Task<(ActorAsked Asked, string Choice)?> ReaskAsync(ActorEffective refused, HarnessSelection selection, CancellationToken ct)
    {
        var again = await ResolveAsync(ct, counts: false, refused: (refused, selection.Profile), except: selection.Number).ConfigureAwait(false);
        var same = ReaskMove.Same(refused, selection.Profile, again.Chosen, again.Selection.Profile);
        Log?.Write("info", "actor.reasked", Line(("refused", refused.From), ("answer", again.Chosen?.From), ("same", same)));
        return same ? (again.Chosen!.Asked, again.Chosen.From) : null;
    }

    /// <summary>A machine log line's data: the role, what the start names, then the line's own.</summary>
    private IReadOnlyList<(string Key, object? Value)> Line(params (string Key, object? Value)[] data) =>
        Named is { } named ? [("actor", Role), named, .. data] : [("actor", Role), .. data];
}
