namespace Daoris.Driver;

/// <summary>
/// Choosing the harness and the account a start runs as, and the resume or carry-on of an earlier session's account.
/// </summary>
internal sealed class RosterSelection(HarnessRoster roster)
{
    /// <summary>
    /// May a session spawn on this harness, and as which account? The two refusals mirror each other
    /// deliberately — a missing binary and a logged-out profile are the same kind of answer, and each
    /// <b>names the action that fixes it</b> rather than failing bare.
    /// </summary>
    /// <remarks>
    /// <para><b>One set</b> (POOL1a2, D197 points 1 and 2; the account-pool design §2.4): every workspace's start reads the agent's
    /// set (<see cref="HarnessSettings.Set"/>), its whole order begun at the order's first. A pick is the one account, never moved;
    /// an agent with no account of Daoris's holds its start for a sign-in.</para>
    /// <para><b>The walk</b>: <see cref="AccountRotation.Order"/>, by <c>use</c> — the goal's (the default) or D125's —
    /// and the first account still ready runs: not cooling, not refused, not signed out. It says which step chose it
    /// (<see cref="HarnessSelection.Choice"/>) and, where it ran somewhere other than where its scope begins, why
    /// (<see cref="HarnessSelection.Rotated"/>). When no account is ready the start waits: before any probe while each is
    /// cooling or refused, with one sentence naming the first reset.</para>
    /// <para><b>Counted as chosen</b>: the goal reads Daoris's sessions as of the driver's last look (<see cref="Look"/>)
    /// and every start chosen since, so starts in one look spread. A start's choice and its count are made one at a time,
    /// under a gate no question is asked under (PARALLEL1c, D181 point 3): a question on one account never holds a start on
    /// another, and an account whose lock stays held past its patience is left unknown for that walk, never counted.</para>
    /// </remarks>
    /// <param name="workspace">The repository's circle, for its pinned version (D49 §4); every workspace reads the one set.</param>
    /// <param name="chosen">The person's pick for this session, when they made one.</param>
    /// <param name="kind">Driven work or a conversation: driven work never starts on the scope's kept account (§4.6).</param>
    /// <param name="model">
    /// The model the start asks (D205 §4.1, MODELROLE1c2): each account's offer reading is judged for it (<see cref="AgentOffers"/>), and
    /// one that does not offer it or refused it is passed for this start, never cooled; null for account default, which no reading passes.
    /// </param>
    /// <param name="passed">The accounts this start already passed for this model (§4.1's per-start set): never a cool-off, never stored.</param>
    /// <param name="except">The start's own count (§4.1's re-ask), left out of the accounts' facts so its own load does not bias the order.</param>
    public Task<HarnessSelection> SelectAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen,
        StartKind kind = StartKind.Driven, CancellationToken ct = default, string? model = null,
        IReadOnlyCollection<string>? passed = null, long except = 0, bool counts = true)
    {
        Interlocked.Increment(ref _selected);
        return roster.WalkAsync(adapter, config, workspace, chosen, kind, counts, ownHome: false, ct, new OfferAsk(model, passed, except));
    }

    private long _selected;

    /// <summary>How many walks <see cref="SelectAsync"/> was asked: a test holds how many walks the account rule asks (FOUNDATION1a-i).</summary>
    internal long Selected => Interlocked.Read(ref _selected);

    /// <summary>
    /// A handed quest's start (MIXAGENT1a-ii, D163 point 1): the account the hand-over named is asked for first, as a person's pick
    /// is. Where it cannot run (cooling, refused, signed out, gone), the walk goes on within the SAME agent's accounts, as for any
    /// start, and never back to the agent the quest was handed from; the answer says which account it passed, so the start's note can.
    /// </summary>
    /// <param name="account">The account the hand-over named, or null for the walk alone.</param>
    /// <returns>The selection, and the account named that could not run it, or null where it did or none was named.</returns>
    /// <param name="model">The model the start asks, its accounts judged by their readings for it (<see cref="SelectAsync"/>).</param>
    /// <param name="passed">The accounts this start already passed for that model.</param>
    public async Task<(HarnessSelection Selection, string? Passed)> SelectHandedAsync(
        string adapter, DriverConfig config, string? workspace, string? account, StartKind kind, CancellationToken ct = default,
        string? model = null, IReadOnlyCollection<string>? passed = null, long except = 0, bool counts = true)
    {
        var named = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        if (named is null) return (await SelectAsync(adapter, config, workspace, chosen: null, kind, ct, model, passed, except, counts).ConfigureAwait(false), null);

        var asked = await SelectAsync(adapter, config, workspace, chosen: named, kind, ct, model, passed, except, counts).ConfigureAwait(false);
        if (asked.Allowed) return (asked, null);

        return (await SelectAsync(adapter, config, workspace, chosen: null, kind, ct, model, passed, except, counts).ConfigureAwait(false), named);
    }

    /// <summary>
    /// The account a resume runs on (MSG1g, D137 §2.2's account paragraph): its record's own, asked for by name as a chat's
    /// picker names one, since its conversation lives in that account's configuration home (D131 §1); never the walk's choice
    /// for a start. Judged by <see cref="ResumeAccount.Judge"/> with what this roster knows of the account before any probe.
    /// </summary>
    /// <remarks>
    /// <para><b>Ready</b>, it is selected as a pick is, so a sign-in the agent last said is gone is learned from that reading
    /// (ROSTER1). <b>Cooling</b>, the resume is held with its cool-off, nothing counted, so a look says the wait once and a screen
    /// shows it waiting for an account (TOOL4g), unless the person chose a new session (<paramref name="newSession"/>).
    /// <b>Unable to run there at all</b> (gone, off the list, kept for conversations, refused, signed out), or left by the
    /// person's choice, the walk picks as for any start (D130 §16.3), which passes the account by itself; where
    /// <paramref name="line"/> does not walk, as for a conversation that nothing carries on by itself, nothing is walked or
    /// counted, and the answer is a refusal saying what held it.</para>
    /// <para><b>The rule is <see cref="LineAccount.Decide"/>'s</b> (FOUNDATION1a-i, the foundation design §3.2): this reads the own account as
    /// it always did, hands the rule the walk to ask lazily (<see cref="LineAccount.DecideAsync"/>), asks the own account by name where the
    /// rule says it resumes there, and says each answer in the words it always said it in.</para>
    /// </remarks>
    /// <param name="account">The account the record ran on, or null for the tool's own sign-in.</param>
    /// <param name="newSession">The person chose to go on in a new session rather than wait for a cooling account.</param>
    /// <param name="limited">
    /// The record an account's limit ended (LIMITRESUME1b, D206; the limit-resume design §3.3): its account, ready, has its maker's reading
    /// asked first as the walk asks a just-reset account, a reading still out held as cooling; and, cooling, its words never wait for it
    /// (<see cref="ResumeChoice.Limited"/>), since they go where the line goes.
    /// </param>
    /// <param name="model">
    /// The record's asked model (D205 §3.5(a), MODELROLE1c2): an own account whose reading refuses it cannot carry the words, which go to
    /// a new record on another account of the same choice, the own account passed, as an own account signed out does.
    /// </param>
    /// <param name="line">
    /// What kind of line the resume is for (<see cref="LineKind"/>): whether an account that cannot carry the words is replaced by the walk's
    /// pick; whether its line can carry its words to a new part by itself (LIMITSWITCH1, D137's note of 2026-10-25: a driven record whose
    /// quest is open or taken, and since LIMITSWITCH2 an ended conversation and a closed quest's session, whose runner goes on in a new part of
    /// the same line), so its words never wait for a cooling own account while another account of the line's choices is ready
    /// (<see cref="ResumeChoice.Switched"/>) and wait otherwise for the earliest reset of any; and whether the switch's walk counts the start
    /// it picks as chosen (<see cref="SelectAsync"/>'s <c>counts</c>): a driven carry-on opens on that pick, so it does; a conversation's and
    /// a closed quest's session are opened by the ended move, which walks and counts once itself (LIMITSWITCH2). Ask Daoris's room never
    /// switches (it goes elsewhere only at a limit, D205 §3.5(e)), and a record whose own account cannot carry the words for another reason
    /// is the person's press. The default is a driven record that cannot switch.
    /// </param>
    /// <param name="picked">
    /// The person picked <paramref name="account"/> for this record (FOUNDATION1a-ii, kept on its binding in the desktop store): the rule
    /// holds it to that account as a pick (<see cref="LineConstraint.Pick"/>), which waits while it cools and is never moved. False for an agent's
    /// pick, the curve's account and a record from before, which walk their agent's accounts as they did.
    /// </param>
    public async Task<ResumeChoice> ResumeAsync(
        string adapter, DriverConfig config, string? workspace, string? account, StartKind kind, bool newSession = false,
        LineKind line = default, CancellationToken ct = default, bool limited = false, string? model = null, bool picked = false)
    {
        var resolved = roster.Adapters.Resolve(adapter);
        var trigger = new LineTrigger(newSession, limited);
        var named = string.IsNullOrWhiteSpace(account) ? null : account.Trim();

        // The rule asks the walk at three places, each as today's resume asked it (FOUNDATION1a-i): an adapter with no accounts walks as a
        // start does, counted; the switch's walk, counted only where the line's carry-on opens on its pick; and the roster's own walk, with
        // no account chosen, for an account that cannot carry the words, which passes the own account where the model is not offered there.
        Task<HarnessSelection> Walk(LineAsk ask) => ask.Place switch
        {
            LineWalkPlace.NoToolchain => SelectAsync(adapter, config, workspace, chosen: null, kind, ct),
            LineWalkPlace.Switch => SelectAsync(adapter, config, workspace, chosen: null, kind, ct, model, counts: ask.Counts),
            _ => SelectAsync(adapter, config, workspace, chosen: null, kind, ct, model, ask.NotOffered && named is not null ? [named] : null),
        };

        if (resolved.Toolchain is not { } toolchain)
        {
            // An adapter with no accounts has nothing to ask for: it spawns as it always did.
            var spawned = await LineAccount.DecideAsync(LineOwn.Without, trigger, LineConstraint.None, line, Walk).ConfigureAwait(false);
            return new ResumeChoice(((LineAnswer.Resume)spawned).Walked!, NextHold.Ready) { Answer = spawned };
        }

        // The own account's reading judged for the asked model before anything is asked of it (D205 §3.5(a)): one that refuses it is an
        // account that cannot carry the words, never ended or cooled.
        var unoffered = !string.IsNullOrWhiteSpace(account) && model is not null
            && AgentOffers.Offers(roster.Home, resolved.Name, (await roster.ReportAsync(resolved.Name, config, refresh: false, ct).ConfigureAwait(false))?.Version,
                account.Trim(), roster.Clock()).Session.Judge(model) is not null;

        var owner = roster.Adapters.OwnerOf(resolved.Name);
        // A record on a Daoris account is judged against the set a start walks (POOL1a2, the account-pool design §2.4), whatever its
        // workspace; one that began in the tool's own home is judged against no scope (F1, ResumeAccount.Judge).
        var settled = roster.Settings.Set(owner);
        var scope = named is null ? settled : Walked(settled, owner, toolchain);
        IReadOnlyCollection<string> present;
        try
        {
            present = named is null ? [] : HarnessSettings.Profiles(roster.Home, owner);
        }
        catch (DriverException)
        {
            present = [];
        }

        var names = AccountNames.Of(roster.Home, owner);
        var before = roster.Before(owner, named, roster.Clock(), names);
        var own = ResumeAccount.Judge(named, scope, kind, before, present);
        // LIMITRESUME1b (the design §3.3 step 1, round 2's B2): a limited record's own account, ready by its cool-off, is asked its maker's
        // reading first, where its door carries none of its windows, and judged again on what that left; where its door carries them, the
        // attempt is the probe. USAGEONE1's skip of a resume's probe stays for the person's words.
        if (limited && named is not null && own == NextHold.Ready)
        {
            await roster.FreshenAsync(adapter, owner, [named], config, ct).ConfigureAwait(false);
            before = roster.Before(owner, named, roster.Clock(), names);
            own = ResumeAccount.Judge(named, scope, kind, before, present);
        }

        // The rule (LIMITSWITCH1, D137's note of 2026-10-25, point 7 amended): a record's words never wait for its own account while another
        // account of the line's choices is ready where the line can carry them to a new part by itself, and wait otherwise for the earliest
        // reset (D206 point 2); LIMITSWITCH2 gave a conversation and a closed quest's session the same, their runner opening the part.
        // A recorded account pick is the rule's constraint (FOUNDATION1a-ii, §3.2 step 4): the account the person picked and nothing else.
        var constraint = picked && named is not null ? LineConstraint.Pick(named) : LineConstraint.None;
        var ownState = new LineOwn(own, unoffered, before.Cooling);
        var answer = await LineAccount.DecideAsync(ownState, trigger, constraint, line, Walk).ConfigureAwait(false);
        if (answer is LineAnswer.Resume)
        {
            // Ready before any probe: asked for by name, as a pick is; a record that began in the tool's own home goes on there, as a
            // harness keeps a conversation in the home it began in (D175 point 3), where every new start takes a Daoris account.
            var asked = await ChooseAsync(adapter, config, workspace, chosen: named, kind, counts: true, ct, own: named is null).ConfigureAwait(false);
            if (asked is not { Allowed: false, NotReady: AccountReadiness.SignedOut }) return new ResumeChoice(asked, NextHold.Ready) { Answer = answer };

            // What the probe learned, the rule read again: the own account is signed out.
            answer = await LineAccount.DecideAsync(ownState with { Hold = NextHold.SignedOut }, trigger, constraint, line, Walk)
                .ConfigureAwait(false);
        }

        return Said(answer);

        // What the rule answered, said in the words each answer has always had. CONVNOTE2g: the conversation's line by code beside the hold's
        // English, composed here, where the names it says are read.
        ResumeChoice Said(LineAnswer answered) => answered switch
        {
            LineAnswer.Elsewhere { Switched: true } switched => new ResumeChoice(switched.Selection, switched.Why) { Switched = true, Answer = answered },

            // The own account's reset is the earliest where the walk found none sooner: its hold says its conversation is there.
            LineAnswer.Wait { Set: ResetSet.Switch, OwnFirst: false } sooner => Waiting(sooner),
            // A pick waits for its own account too: the account the person picked, whose reset is the wait, as the own account's is above.
            LineAnswer.Wait { OwnFirst: true } ownWait => new ResumeChoice(
                new HarnessSelection(ResumeWords.Waits(ownWait.Earliest, roster.Zone, names)) { Cooling = ownWait.Earliest }, ownWait.Why)
            {
                Waiting = ResumeWords.Waiting(ownWait.Earliest, roster.Zone, names, ResumeWords.NotYet),
                Answer = answered,
            },
            LineAnswer.Elsewhere elsewhere => Moved(elsewhere.Selection, elsewhere.Why, elsewhere.NotOffered, answered),
            LineAnswer.Wait { Walked: { } walked } wait => Moved(walked, wait.Why, wait.NotOffered, answered),
            LineAnswer.Hold { Walked: { } walked } hold => Moved(walked, hold.Why, hold.NotOffered, answered),

            // Where the own account does not carry the words and the line walks none, a refusal saying what held it.
            LineAnswer.Hold hold => Moved(
                new HarnessSelection(hold.NotOffered
                    ? $"Its own account `{AccountNames.Said(names, named!)}` does not offer `{model}`."
                    : ResumeWords.Line(hold.Why, newSession)?.Note ?? "Its own account cannot carry it.") { Cooling = hold.Cooling },
                hold.Why, hold.NotOffered, answered),
            _ => throw new InvalidOperationException($"the account rule answered {answered} where a resume reads another."),
        };

        ResumeChoice Waiting(LineAnswer.Wait sooner)
        {
            var waits = ResumeWords.LimitWaits(sooner.Earliest, roster.Zone, names);
            return new ResumeChoice(new HarnessSelection(waits.Note) { Cooling = sooner.Earliest, RefusalNote = waits }, sooner.Why)
            {
                Waiting = ResumeWords.LimitWaits(sooner.Earliest, roster.Zone, names, ResumeWords.NotYet),
                Answer = sooner,
            };
        }

        // Where the own account does not offer the model, the walk takes the same choice's other accounts with the own one passed (§3.5(a)).
        ResumeChoice Moved(HarnessSelection selection, NextHold held, bool notOffered, LineAnswer answered) => new(selection, held)
        {
            NewSession = newSession && held == NextHold.Cooling,
            Limited = limited,
            NotOffered = notOffered,
            Answer = answered,
        };
    }

    /// <summary>
    /// The account a resume's words take where they are carried to a NEW record, its own conversation unable to go on (OWNSPACE1a,
    /// D175 point 3): the resume's own where that is a Daoris account; where it was the tool's own home, the walk's, since only
    /// the record that began there goes on there. A new record is a new start, so it takes the walk as any start does (D130
    /// §16.2), and holds where the walk holds.
    /// </summary>
    /// <param name="account">The account a hand-over named, which the walk asks for first, or null.</param>
    /// <returns>The selection, and the account named that could not run it, as <see cref="SelectHandedAsync"/> answers.</returns>
    public async Task<(HarnessSelection Selection, string? Passed)> CarriedOnAsync(
        ResumeChoice resume, string adapter, DriverConfig config, string? workspace, string? account, CancellationToken ct = default,
        string? model = null) =>
        // The carry-on walks the asked choice's accounts, each judged by its reading for the record's model (D205 §3.5(a)).
        resume.Selection is { Allowed: true, Profile: null }
            ? await SelectHandedAsync(adapter, config, workspace, account, StartKind.Driven, ct, model).ConfigureAwait(false)
            : (resume.Selection, null);

    /// <summary>
    /// <see cref="SelectAsync"/>, or with <paramref name="counts"/> false the same answer for a panel, which starts nothing
    /// and so is never counted as a start chosen.
    /// </summary>
    /// <param name="own">A resume of a record that began in the tool's own home, which goes on there (D175 point 3).</param>
    internal Task<HarnessSelection> ChooseAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen, StartKind kind, bool counts, CancellationToken ct,
        bool own = false, string? model = null, bool asks = true) =>
        // No gate here (PARALLEL1c): the walk takes its own only to read again and count, never across a question.
        roster.WalkAsync(adapter, config, workspace, chosen, kind, counts, own, ct, new OfferAsk(model, null, 0), asks);

    /// <summary>
    /// The scope a start walks (OWNSPACE1a, D175 points 1-2 and its OWNSPACE1a note): <paramref name="scope"/>, unless it names no
    /// account and the agent's own home is the person's. Then the agent's Daoris accounts on this machine are the start's: one is
    /// the scope's one account, more its list, walked by the rotation's own order and allowance rules; none leaves it naming
    /// nothing, which a start holds on (<see cref="RotationWords.NoAccountOf"/>), never running in the person's own tool home.
    /// </summary>
    internal RotationScope Walked(RotationScope scope, string owner, HarnessToolchain toolchain)
    {
        if (scope.List.Count > 0 || scope.Begins is not null || toolchain.StartsInOwnHome) return scope;

        IReadOnlyList<string> mine;
        try
        {
            mine = HarnessSettings.Profiles(roster.Home, owner);
        }
        catch (DriverException)
        {
            mine = [];
        }

        return mine.Count switch
        {
            0 => scope,
            1 => scope with { Begins = mine[0] },
            _ => roster.Settings.Over(owner, mine),
        };
    }

    /// <summary>
    /// The hold of a start with no Daoris account of the agent's signed in (OWNSPACE1a, D175 point 2): one sentence, and a wait
    /// for a sign-in naming the accounts it passed signed out, none where the agent has none, so a look reports it as a wait and
    /// says it once (D125 point 6).
    /// </summary>
    internal static HarnessSelection NoAccount(string owner, IReadOnlyList<string> signedOut, IReadOnlyDictionary<string, string> names)
    {
        var note = RotationWords.NoAccountOf(owner);
        return new HarnessSelection(note.Note) { SignedOut = SignedOutAccounts.Of(owner, signedOut, names) with { NoAccount = true }, RefusalNote = note };
    }
}
