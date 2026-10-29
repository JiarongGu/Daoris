namespace Daoris.Driver;

/// <summary>
/// The codes a start's walk passes an entry of its role's order by (D205 §4.1, §4.5; FOUNDATION1j, the foundation design §2.4): one
/// vocabulary for every walk over a role's order, said in the wait's sentence, the opening note's actor line and the machine log's
/// <c>actor.passed</c> and <c>actor.chose</c> words (<see cref="BuildPass.Word"/>).
/// </summary>
public static class StartPasses
{
    /// <summary>
    /// No agent of that name on this machine, or one that cannot do the start's work, passed before any account is asked; or one its account
    /// walk found not installed, or not at the version its pin names, which cannot run here at all (D205 point 4).
    /// </summary>
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
    /// The code of an entry whose account walk held, read from the walk's facts and never its words: an agent absent, or absent at its pin,
    /// first, since nothing of it can run (FOUNDATION1j part ii, as the reviewer's walk reads the same facts); then a model not offered, since
    /// the next entry may still run on the same accounts; then a cool-off, which waits for a time; then a sign-in; else held.
    /// </summary>
    public static string Of(HarnessSelection selection) =>
        selection.Absent is not null ? Unavailable
        : selection.NotOffered is not null ? NotOffered
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
/// One entry's try in a start plan's loop (FOUNDATION1j part ii, <see cref="StartPlan.LoopAsync{TEntry}"/>): it is taken, on its account walk's
/// selection; or it is passed, by a code of <see cref="StartPasses"/> and the sentence that says why, with the account walk's selection where
/// one was asked and held, so the wait can name the soonest cool-off.
/// </summary>
/// <param name="Taken">Whether the entry runs: the loop takes it and walks no further.</param>
/// <param name="Selection">The account walk's answer it runs on, or the one that held it; null where no account was asked.</param>
/// <param name="Code">The code it is passed by; null for an entry taken.</param>
/// <param name="Why">The walk's own sentence in English, said in the wait and the opening note's actor line.</param>
public sealed record EntryTry(bool Taken, HarnessSelection? Selection, string? Code, string? Why)
{
    /// <summary>The entry runs on <paramref name="selection"/>.</summary>
    public static EntryTry Runs(HarnessSelection selection) => new(true, selection, null, null);

    /// <summary>The entry is passed by <paramref name="code"/>, <paramref name="why"/> saying so; <paramref name="held"/> is the account walk that held it, if one was asked.</summary>
    public static EntryTry Passed(string code, string? why, HarnessSelection? held = null) => new(false, held, code, why);
}

/// <summary>
/// The plan's loop's answer (FOUNDATION1j part ii): the entry taken and the selection it runs on, null where none runs; each entry passed
/// before it, in order; the first account walk that held, and the soonest cool-off among those that held, for the wait's sentence.
/// </summary>
internal sealed record StartWalked<TEntry>(
    TEntry? Taken, HarnessSelection? Selection, IReadOnlyList<BuildPass> Passes, HarnessSelection? Held, CoolingEntry? Soonest)
    where TEntry : class;

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
/// <para><b>One loop for every walk</b> (FOUNDATION1j part ii, <see cref="LoopAsync{TEntry}"/>): the plan's own walk and a reviewer's
/// (a second opinion's, a helper's and a partner's) walk their entries through it, each entry's accounts by the plan's one ask
/// (<see cref="AccountsAsync"/>). A walk with a rule of its own over the loop gives the order to walk and which entries an earlier pass
/// holds: the reviewer's is its order by maker and its held agents, kept in <c>ReviewerChoice</c>.</para>
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
    /// <param name="fence">
    /// The agents a rule or an ask names as who may run (D205 §4.4): the role's order is theirs, each taking the role's entries for it, else
    /// <c>rule-default</c>; null for the role's own order.
    /// </param>
    /// <param name="order">The entries the caller holds already (a helper's own order, a recheck's continuation), walked as given; no entry is read.</param>
    /// <exception cref="ActorUnreadableException">The role's stored entry does not read: the start that reads it holds.</exception>
    public StartPlan(
        string work, DriverConfig config, HarnessRoster roster, AdapterSet adapters, PassMemory memory, IReadOnlyList<string>? fence = null,
        IReadOnlyList<ActorEffective>? order = null)
    {
        Work = work;
        Role = RoleOf(work);
        Order = order ?? ActorChoices.Effective(Role, config, fence: fence);
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
            var only = await AccountsAsync(Order[0].Agent, Order[0].Model, PassedFor(Order[0]), ct, counts, except).ConfigureAwait(false);
            return new SettledSelection(Role, only, Order[0], []);
        }

        using var walk = _memory.Walk(Subject, counts);
        var walked = await LoopAsync(
                Order, entry => entry, entry => TryAsync(entry, PassedFor(entry), counts, except, ct),
                said: pass =>
                {
                    if (walk.Fresh(pass)) Log?.Write("info", "actor.passed", Line(("choice", pass.Entry.From), ("agent", pass.Entry.Agent), ("code", pass.Code)));
                })
            .ConfigureAwait(false);
        if (walked.Taken is { } taken)
        {
            if (counts)
            {
                Log?.Write("info", "actor.chose", Line(("choice", taken.From), ("agent", taken.Agent), ("model", taken.Model),
                    ("passed", walked.Passes.Select(pass => pass.Word).ToList())));
            }

            return new SettledSelection(Role, walked.Selection!, taken, walked.Passes) { Adapter = _adapters.Resolve(taken.Agent) };
        }

        // The wait's sentence (D205 §4.5): judged again at every look, the first choice that can run, in order, taken.
        var wait = $"{ActorWords.Name(Role)} waits: none of its choices can run now. {string.Join("; ", walked.Passes.Select(pass => pass.Said))}.";
        return new SettledSelection(Role, (walked.Held ?? new HarnessSelection(wait)) with { Refusal = wait, Cooling = walked.Soonest }, null, walked.Passes);
    }

    /// <summary>
    /// The plan's one loop (D205 §4.1; FOUNDATION1j part ii): the entries in <paramref name="order"/>, each tried once, the first that runs
    /// taken and nothing after it asked; each other passed in order with its code and said once (<paramref name="said"/>). A walk's own rule
    /// over the loop is <paramref name="holds"/>: an entry it holds, by the passes before it, is neither tried nor passed.
    /// </summary>
    /// <typeparam name="TEntry">What the caller walks: the plan's own entries, or a walk's own record of each (a reviewer's, with its family).</typeparam>
    /// <param name="choiceOf">The role's entry each is, which its pass names.</param>
    /// <param name="tryEntry">One entry tried: the plan's own before-the-accounts passes and account walk, or a walk's own over the same ask.</param>
    public static async Task<StartWalked<TEntry>> LoopAsync<TEntry>(
        IEnumerable<TEntry> order, Func<TEntry, ActorEffective> choiceOf, Func<TEntry, Task<EntryTry>> tryEntry,
        Func<TEntry, IReadOnlyList<BuildPass>, bool>? holds = null, Action<BuildPass>? said = null)
        where TEntry : class
    {
        var passes = new List<BuildPass>();
        HarnessSelection? held = null;
        CoolingEntry? soonest = null;
        foreach (var entry in order)
        {
            if (holds?.Invoke(entry, passes) == true) continue;
            var tried = await tryEntry(entry).ConfigureAwait(false);
            if (tried.Taken) return new StartWalked<TEntry>(entry, tried.Selection, passes, held, soonest);
            if (tried.Selection is { } selection)
            {
                held ??= selection;
                if (selection.Cooling is { } cooling && (soonest is null || cooling.Until < soonest.Until)) soonest = cooling;
            }

            var pass = new BuildPass(choiceOf(entry), tried.Code ?? StartPasses.Held, tried.Why);
            passes.Add(pass);
            said?.Invoke(pass);
        }

        return new StartWalked<TEntry>(null, null, passes, held, soonest);
    }

    /// <summary>
    /// The plan's one ask of D197's account walk for an entry (§4.1): <paramref name="agent"/>'s accounts for this start's workspace and kind,
    /// each judged by its reading for <paramref name="model"/>, those this start already passed for it left out. Where it chooses an account
    /// and the start <paramref name="counts"/>, the roster counts the start under its gate: the start's one reservation. A probe counts nothing.
    /// </summary>
    /// <param name="except">The start's own count, left out of the accounts' facts for a re-ask.</param>
    public Task<HarnessSelection> AccountsAsync(
        string agent, string? model, IReadOnlyCollection<string>? passed, CancellationToken ct, bool counts = true, long except = 0) =>
        _roster.SelectAsync(agent, _config, Workspace, chosen: null, Kind, ct, model, passed, except, counts);

    /// <summary>
    /// The plan's own try of an entry: an agent this machine lacks passed <c>unavailable</c>, and one the caller's gate refuses by the gate's
    /// code, each before any account is asked; then the entry's account walk, an entry whose every ready account does not offer its model passed
    /// <c>not-offered</c> (D205 §3.3), never cooled.
    /// </summary>
    private async Task<EntryTry> TryAsync(ActorEffective entry, IReadOnlyCollection<string>? passed, bool counts, long except, CancellationToken ct)
    {
        if (!_adapters.Names.Contains(entry.Agent, StringComparer.OrdinalIgnoreCase))
        {
            return EntryTry.Passed(StartPasses.Unavailable, $"`{entry.Agent}` is not an agent on this machine");
        }

        if (Gate?.Invoke(entry, _adapters.Resolve(entry.Agent)) is { } gated) return EntryTry.Passed(gated.Code, gated.Why);
        var selection = await AccountsAsync(entry.Agent, entry.Model, passed, ct, counts, except).ConfigureAwait(false);
        return selection.Allowed ? EntryTry.Runs(selection) : EntryTry.Passed(StartPasses.Of(selection), selection.Refusal, selection);
    }

    /// <summary>
    /// The agent a continuation of <paramref name="record"/> goes on with (the foundation design §2.4's first row and §7 row 5; D205 point 1
    /// and its PRESSMODEL1 note): its record's role and asked choice, read as the agent that choice was asked of, else the agent the record
    /// opened on, else its role's first entry, the role the record's own (<c>actor</c>) or, where it names none this build knows, the seeded
    /// role of <paramref name="work"/>. Never the machine's <c>adapter</c> key, which a role read <i>as before</i> answers itself
    /// (<see cref="ActorChoices.FirstAgent"/>).
    /// </summary>
    /// <param name="record">The record it goes on from; null for none, which reads the work's role.</param>
    /// <param name="work">The work a record naming no role was opened for: a driven quest's, unless the caller says a conversation's.</param>
    /// <exception cref="DriverException">The role's stored entry does not read, or the role is off: the continuation holds, saying so.</exception>
    public static string ContinuedOn(PriorSession? record, DriverConfig config, string work = "build")
    {
        if (record?.Asked?.Agent is { Length: > 0 } asked) return asked;
        if (record?.Adapter is { Length: > 0 } opened) return opened;
        var role = record?.Actor is { } actor && ActorChoices.Ids.Contains(actor, StringComparer.Ordinal) ? actor : RoleOf(work);
        return ActorChoices.FirstAgent(role, config)
            ?? throw new DriverException($"{ActorWords.Name(role)} is off on this machine, so a record of its work does not go on until it is given a choice.");
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
