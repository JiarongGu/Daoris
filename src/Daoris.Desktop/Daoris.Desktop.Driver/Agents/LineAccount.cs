namespace Daoris.Driver;

/// <summary>
/// The kind of line a resume is for (FOUNDATION1a-i, the foundation design §3.2): what its caller can do with the account rule's answer,
/// which decides whether a cooling own account is moved off, whether the walk is asked where the own account cannot carry the words, and
/// whether that walk counts the start it picks. The values are the three flags LIMITSWITCH1 and LIMITSWITCH2 gave the roster's resume,
/// named by the lines that pass them. The default is a driven record that cannot switch.
/// </summary>
/// <param name="Sort">Which line.</param>
/// <param name="Carries">
/// The line's runner can carry the words to a new part by itself, and they are the person's: a closed quest's session where the shell has a
/// runner, a conversation whose words a person said. A driven record of an open or taken quest always can; Ask Daoris's room never does,
/// since it goes elsewhere only at a limit (D205 §3.5(e)).
/// </param>
public readonly record struct LineKind(LineSort Sort = LineSort.DrivenClosed, bool Carries = false)
{
    /// <summary>A driven record of an open or taken quest: the driver carries it on, the walk's pick opening its part and counted by it.</summary>
    public static LineKind DrivenOpen => new(LineSort.DrivenOpen, true);

    /// <summary>A closed quest's session, which carries on only where the shell's chat runner opens a new part of it (<paramref name="carries"/>).</summary>
    public static LineKind DrivenClosed(bool carries) => new(LineSort.DrivenClosed, carries);

    /// <summary>The limited record's own account asked before the walk (LIMITRESUME1b step 1): a probe that walks nothing and moves nothing.</summary>
    public static LineKind DrivenLimit => new(LineSort.DrivenLimit);

    /// <summary>An ended conversation, whose runner's ended move opens the new part and counts its start once (LIMITSWITCH2).</summary>
    public static LineKind Conversation(bool carries) => new(LineSort.Conversation, carries);

    /// <summary>Ask Daoris's own conversation: it waits for its own account (D137 §2.2, D205 §3.5(e)).</summary>
    public static LineKind AskDaoris => new(LineSort.Conversation);

    /// <summary>An account that cannot carry the words is replaced by the walk's pick, where one is asked.</summary>
    public bool Walks => Sort is LineSort.DrivenOpen or LineSort.DrivenClosed;

    /// <summary>The own account cooling, its words go on now in a new part where another account is ready (LIMITSWITCH1, LIMITSWITCH2).</summary>
    public bool Switches => Carries && Sort != LineSort.DrivenLimit;

    /// <summary>The switch's walk counts the start it picks: only the driver's carry-on, which opens on that pick.</summary>
    public bool WalkCounts => Sort == LineSort.DrivenOpen;
}

/// <summary>The line kinds <see cref="LineKind"/> names.</summary>
public enum LineSort
{
    /// <summary>A closed quest's session; also what a resume that names no kind is.</summary>
    DrivenClosed,

    /// <summary>A driven record of an open or taken quest.</summary>
    DrivenOpen,

    /// <summary>A driven record a limit ended, its own account asked first.</summary>
    DrivenLimit,

    /// <summary>A repository conversation, or Ask Daoris's.</summary>
    Conversation,
}

/// <summary>What moved the line, apart from its account: the person's choice and a limit's end.</summary>
/// <param name="NewSession">The person chose to go on in a new session rather than wait for a cooling account.</param>
/// <param name="Limited">An account's limit ended the record (LIMITRESUME1b): its words go where the line goes, never waiting.</param>
public readonly record struct LineTrigger(bool NewSession = false, bool Limited = false);

/// <summary>The account a line is held to: none, a handed quest's named account, or a hand-over press's account opened as a pick.</summary>
/// <remarks>No record keeps an account pick yet (FOUNDATION1a-ii's), so a resume's constraint is always none.</remarks>
public readonly record struct LineConstraint(LineBinding Binding = LineBinding.None, string? Account = null)
{
    /// <summary>The line's own account, or a start's walk, as before.</summary>
    public static LineConstraint None => default;

    /// <summary>A handed quest's named account: asked for first, then the walk of the same agent's accounts (<c>SelectHandedAsync</c>).</summary>
    public static LineConstraint Handed(string account) => new(LineBinding.Handed, account);

    /// <summary>A hand-over press's account, opened as a pick: the one account, never moved (<c>SelectAsync</c> with a choice).</summary>
    public static LineConstraint Pick(string account) => new(LineBinding.Pick, account);
}

/// <summary>The ways <see cref="LineConstraint"/> holds a line.</summary>
public enum LineBinding { None, Handed, Pick }

/// <summary>
/// What the roster knows of the line's own account before the rule reads it: <see cref="ResumeAccount.Judge"/>'s answer, that the
/// asked model is not offered there, and its cool-off whatever else holds it.
/// </summary>
/// <param name="Hold">What holds the account from carrying the words; <see cref="NextHold.Ready"/> where nothing does.</param>
/// <param name="NotOffered">The account's reading does not offer the record's asked model (D205 §3.5(a)); only read of a ready account.</param>
/// <param name="Cooling">The account's cool-off, which a cooling account always has and an account that cannot run there may have as well.</param>
/// <param name="NoAccounts">The adapter has no toolchain and so no accounts: it spawns as it always did and walks as a start does.</param>
public readonly record struct LineOwn(NextHold Hold, bool NotOffered = false, CoolingEntry? Cooling = null, bool NoAccounts = false)
{
    /// <summary>An account cooling or not, as a reader of the cooling store alone sees it.</summary>
    public static LineOwn Of(CoolingEntry? cooling) => new(cooling is null ? NextHold.Ready : NextHold.Cooling, Cooling: cooling);

    /// <summary>An adapter that declares no toolchain.</summary>
    public static LineOwn Without => new(NextHold.Ready, NoAccounts: true);
}

/// <summary>Where the rule asks the walk (the foundation design's row a-i): the three places today's resume walks, and a handed quest's start.</summary>
public enum LineWalkPlace
{
    /// <summary>An adapter with no accounts walks as a start does, counted, whatever the line's flags say.</summary>
    NoToolchain,

    /// <summary>The switch's walk: the own account cooling and the line able to carry its words on by itself.</summary>
    Switch,

    /// <summary>The roster's own <c>ElsewhereAsync</c>: the own account cannot carry the words, with no account chosen.</summary>
    Elsewhere,

    /// <summary>A handed quest's named account first, then the same agent's walk.</summary>
    Handed,
}

/// <summary>
/// What the rule asks of the walk: where, whether it counts the start it picks, and for an account the asked model is not offered on,
/// that this account is passed. The caller answers it with the walk's selection, or a <c>Decide</c> run twice does (<see cref="LineAccount.DecideAsync"/>).
/// </summary>
public sealed record LineAsk(LineWalkPlace Place, bool Counts, bool NotOffered = false, string? Named = null);

/// <summary>Which accounts a reset is the earliest of (the foundation design §3.2 step 5, row a-i): each caller's own set as today.</summary>
public enum ResetSet
{
    /// <summary>The walk's states: an order with none ready, its first reset (<c>HarnessRoster.Unready</c>).</summary>
    Walk,

    /// <summary>The switch's walk against the own reset, the own account's included where it may resume there (LIMITSWITCH1, O2-4).</summary>
    Switch,

    /// <summary>A limited conversation's wake: every cooling account of the agent, the walk's or not (<c>HarnessRoster.CoolingsOf</c>).</summary>
    Wake,
}

/// <summary>What would put a hold right (the foundation design §3.2 step 5): named, since the rule says it where nothing cools.</summary>
public enum LineRepair
{
    /// <summary>Nothing to repair: the hold is a refusal that passes by itself or is for the person's press.</summary>
    None,

    /// <summary>An account to sign in: the walk found every one signed out.</summary>
    SignIn,

    /// <summary>An account to add: the account is gone from this machine, or the agent has none of Daoris's.</summary>
    AddAccount,

    /// <summary>The model to offer there: the account's reading does not offer what the record asked.</summary>
    OfferModel,

    /// <summary>The provider's refusal to clear: it refused the account's credential.</summary>
    ClearRefusal,

    /// <summary>The set to change: the work no longer uses the account, or it is kept for conversations.</summary>
    ChangeSet,

    /// <summary>The agent to install: nothing of it is here, or a pin has nothing installed at it.</summary>
    InstallAgent,
}

/// <summary>What the account rule answers: <see cref="Resume"/>, <see cref="Elsewhere"/>, <see cref="Wait"/> or <see cref="Hold"/>.</summary>
public abstract record LineAnswer
{
    private LineAnswer()
    {
    }

    /// <summary>The line's own account carries its words. Where <paramref name="Walked"/> is set the adapter has no accounts and its start's walk is the answer.</summary>
    public sealed record Resume(HarnessSelection? Walked = null) : LineAnswer;

    /// <summary>The words go to another account: the walk's pick. <paramref name="Switched"/> is LIMITSWITCH's move off a cooling own account.</summary>
    public sealed record Elsewhere(
        HarnessSelection Selection, NextHold Why, bool NotOffered = false, bool Switched = false, string? Passed = null) : LineAnswer;

    /// <summary>
    /// The words wait for <paramref name="Earliest"/>, the earliest reset of <paramref name="Accounts"/> as <paramref name="Set"/> reads it.
    /// <paramref name="OwnFirst"/> is that the earliest is the own account's, whose conversation is there; <paramref name="Walked"/> is the
    /// walk's refusal where the walk is what said the wait.
    /// </summary>
    public sealed record Wait(
        CoolingEntry Earliest, IReadOnlyList<CoolingEntry> Accounts, ResetSet Set, NextHold Why, HarnessSelection? Walked = null,
        bool OwnFirst = false, bool NotOffered = false) : LineAnswer;

    /// <summary>
    /// Nothing carries the words and nothing resets: <paramref name="Why"/> says what holds the own account, and <paramref name="Repair"/> what
    /// would put it right. <paramref name="Walked"/> is the walk's refusal where the walk said it (<c>HarnessRoster.Unready</c>'s).
    /// </summary>
    public sealed record Hold(
        NextHold Why, LineRepair Repair, HarnessSelection? Walked = null, CoolingEntry? Cooling = null, bool NotOffered = false) : LineAnswer;
}

/// <summary>
/// The account rule as one call (FOUNDATION1a-i, the foundation design §3.2): what a line whose conversation lives on one account does
/// when that account cannot carry its words, as LIMITSWITCH1 and LIMITSWITCH2 built it and <see cref="ResumeChoice"/>, the roster's resume,
/// the chat's go-on, Ask Daoris's, the limits wake and the closed quest's hold read it. Pure: it asks nothing and writes nothing.
/// </summary>
/// <remarks>
/// <para><b>The walk is a lazy input</b>, asked only where today's code walks and never handed up front, since a walk counts the start it
/// chooses: where the own account cools under a line that switches, in the roster's own <c>ElsewhereAsync</c> for an account that cannot carry,
/// and for an adapter with no toolchain. A caller that cannot hand a walk it has not yet made runs the rule twice
/// (<see cref="DecideAsync"/>), the second time with the walk's answer where the first asked for it.</para>
/// <para><b>Nothing here changes an answer.</b> Each branch is one the roster's resume took, in its order; the earliest reset is each caller's own
/// set (<see cref="Earliest"/>); and a reset over exactly the accounts a line may take is FOUNDATION1a-iii's.</para>
/// </remarks>
public static class LineAccount
{
    /// <summary>
    /// The own account cools, nobody chose a new session and no limit ended the record: its words wait or move on by the line's switch, never
    /// asking another account to carry them on its own.
    /// </summary>
    public static bool CoolingHolds(NextHold own, LineTrigger trigger) => own == NextHold.Cooling && !trigger.NewSession && !trigger.Limited;

    /// <summary>What holds an account the cooling store alone reads: cooling, or ready.</summary>
    public static NextHold OwnHold(CoolingEntry? cooling) => cooling is null ? NextHold.Ready : NextHold.Cooling;

    /// <summary>
    /// The earliest reset of the set <paramref name="set"/> names, where each caller reads its own as today. <see cref="ResetSet.Walk"/> reads
    /// <paramref name="states"/>; <see cref="ResetSet.Switch"/> the own reset and the walk's against it, the walk's only where strictly earlier;
    /// <see cref="ResetSet.Wake"/> <paramref name="coolings"/>. Null where the set has none.
    /// </summary>
    public static CoolingEntry? Earliest(
        ResetSet set, IEnumerable<AccountState>? states = null, CoolingEntry? own = null, CoolingEntry? walked = null,
        IEnumerable<CoolingEntry>? coolings = null) => set switch
    {
        ResetSet.Walk => (states ?? []).Where(state => state.Cooling is not null).Select(state => state.Cooling!).MinBy(cooling => cooling.Until),
        ResetSet.Switch => walked is not null && (own is null || walked.Until < own.Until) ? walked : own,
        _ => (coolings ?? []).MinBy(cooling => cooling.Until),
    };

    /// <summary>What would put a refusal right, read from what the walk said of it.</summary>
    public static LineRepair RepairOf(HarnessSelection refusal) => refusal switch
    {
        { SignedOut.NoAccount: true } => LineRepair.AddAccount,
        { SignedOut: not null } => LineRepair.SignIn,
        { NotReady: AccountReadiness.SignedOut } => LineRepair.SignIn,
        { NotReady: AccountReadiness.Refused } => LineRepair.ClearRefusal,
        { Absent: not null } => LineRepair.InstallAgent,
        { NotOffered.Count: > 0 } => LineRepair.OfferModel,
        _ => LineRepair.None,
    };

    /// <summary>What would put an own account that cannot carry the words right.</summary>
    public static LineRepair RepairOf(NextHold held, bool notOffered) => notOffered
        ? LineRepair.OfferModel
        : held switch
        {
            NextHold.SignedOut => LineRepair.SignIn,
            NextHold.Refused => LineRepair.ClearRefusal,
            NextHold.Missing => LineRepair.AddAccount,
            NextHold.Outside or NextHold.Kept => LineRepair.ChangeSet,
            _ => LineRepair.None,
        };

    /// <summary>
    /// The rule: what the line does with its own account's hold. <paramref name="walk"/> is asked at most once, and only where today's resume
    /// walks (<see cref="LineAsk"/>); where it is not asked nothing walked and nothing was counted.
    /// </summary>
    /// <param name="own">The account the line's conversation lives on, as the roster reads it before any probe.</param>
    /// <param name="trigger">The person's new session and a limit's end.</param>
    /// <param name="constraint">None for a resume; a handed quest's account or a press's pick for a start.</param>
    /// <param name="line">The line's kind.</param>
    /// <param name="walk">The walk's selection for the place asked.</param>
    public static LineAnswer Decide(
        LineOwn own, LineTrigger trigger, LineConstraint constraint, LineKind line, Func<LineAsk, HarnessSelection> walk)
    {
        if (own.NoAccounts) return new LineAnswer.Resume(walk(new LineAsk(LineWalkPlace.NoToolchain, Counts: true)));

        var held = own.Hold;
        var ready = held is NextHold.Ready or NextHold.Near;
        switch (constraint.Binding)
        {
            case LineBinding.Pick:
                // A pick is the one account, never moved: refused while it cools, naming when it resets, and otherwise as it stands.
                if (ready) return new LineAnswer.Resume();
                return own.Cooling is { } picked && held == NextHold.Cooling
                    ? new LineAnswer.Wait(picked, [picked], ResetSet.Walk, held, OwnFirst: true)
                    : new LineAnswer.Hold(held, RepairOf(held, false), Cooling: own.Cooling);
            case LineBinding.Handed:
                // The named account first; where it cannot run, the walk goes on within the same agent's accounts, never back.
                return ready
                    ? new LineAnswer.Resume()
                    : Beyond(own, held, notOffered: false, new LineAsk(LineWalkPlace.Handed, Counts: true, Named: constraint.Account), walk);
        }

        if (CoolingHolds(held, trigger))
        {
            var cooling = own.Cooling!;
            if (line.Switches)
            {
                var walked = walk(new LineAsk(LineWalkPlace.Switch, line.WalkCounts));
                if (walked.Allowed) return new LineAnswer.Elsewhere(walked, held, Switched: true);

                // The own account's reset is the earliest where the walk found none sooner: its hold says its conversation is there.
                var earliest = Earliest(ResetSet.Switch, own: cooling, walked: walked.Cooling)!;
                if (!ReferenceEquals(earliest, cooling))
                {
                    return new LineAnswer.Wait(earliest, [cooling, earliest], ResetSet.Switch, held, walked);
                }
            }

            return new LineAnswer.Wait(cooling, [cooling], ResetSet.Switch, held, OwnFirst: true);
        }

        if (!ready) return Elsewhere(own, held, notOffered: false, line, walk);

        // Ready, the asked model not offered there: the walk takes the same choice's other accounts with the own one passed (§3.5(a)).
        return own.NotOffered ? Elsewhere(own, NextHold.Refused, notOffered: true, line, walk) : new LineAnswer.Resume();
    }

    /// <summary>
    /// <see cref="Decide"/> where the walk is asked of something that waits: the rule runs once with a walk that only records where it is
    /// asked, and, where it asked, once more with the walk's answer.
    /// </summary>
    public static async Task<LineAnswer> DecideAsync(
        LineOwn own, LineTrigger trigger, LineConstraint constraint, LineKind line, Func<LineAsk, Task<HarnessSelection>> walk)
    {
        LineAsk? asked = null;
        var first = Decide(own, trigger, constraint, line, ask =>
        {
            asked = ask;
            return Unasked;
        });
        if (asked is null) return first;

        var walked = await walk(asked).ConfigureAwait(false);
        return Decide(own, trigger, constraint, line, _ => walked);
    }

    /// <summary>What the first run of a twice-run rule is handed where it asks: nothing, which no caller reads.</summary>
    private static HarnessSelection Unasked { get; } = new(Refusal: "");

    // The own account cannot carry the words: the walk's pick where the line walks, else a refusal saying what held it.
    private static LineAnswer Elsewhere(LineOwn own, NextHold held, bool notOffered, LineKind line, Func<LineAsk, HarnessSelection> walk) =>
        line.Walks
            ? Beyond(own, held, notOffered, new LineAsk(LineWalkPlace.Elsewhere, Counts: true, NotOffered: notOffered), walk)
            : new LineAnswer.Hold(held, RepairOf(held, notOffered), Cooling: own.Cooling, NotOffered: notOffered);

    private static LineAnswer Beyond(LineOwn own, NextHold held, bool notOffered, LineAsk ask, Func<LineAsk, HarnessSelection> walk)
    {
        var walked = walk(ask);
        if (walked.Allowed) return new LineAnswer.Elsewhere(walked, held, notOffered, Passed: ask.Named);

        return walked.Cooling is { } soonest
            ? new LineAnswer.Wait(soonest, [soonest], ResetSet.Walk, held, walked, NotOffered: notOffered)
            : new LineAnswer.Hold(held, RepairOf(walked), walked, own.Cooling, notOffered);
    }
}
