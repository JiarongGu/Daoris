using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A conversation over its account's usage curve moves on its own as its turn ends (SWITCH1c-ii; D165 point 3; the switch design
/// §1.2, §2 and §4): to another account of the same agent in its scope that has room, as a hand-over with its agent unchanged, why
/// <c>curve</c>, asked by <c>daoris</c>. <see cref="SwitchDecision"/> reads the account and <see cref="CourseDecision"/> decides whether and where the line moves (FOUNDATION1h
/// branch 1; the scope's switch is its consent); this class asks the first, reads the others where it says so, asks
/// again, asks the course, decides the move in the store (<see cref="Moves"/>, FOUNDATION1f-iii part a; no hand-over box is written beside it), and takes it
/// (<see cref="ChatHandOver"/>'s take of a move), which ends the old conversation and starts the new one, its record continuing the old.
/// Over the runner's context (PART10g, D166).
/// </summary>
/// <remarks>
/// <para><b>Never mid-turn, never during a take.</b> A conversation is judged once per turn's end, as its turns tell it with
/// nothing running, opening or queued (<see cref="TurnJustEnded"/>); never while a hand-over waits or is taken, whose box is kept
/// until its take is done (a box an older build left is still the hand-over's own); and the take holds the turns
/// (<see cref="ChatTurns.Hold"/>), so a word said as it begins either runs its turn first, the move then closed and the next turn's end
/// judging again, or takes nothing.</para>
/// <para><b>Never across agents, never outside the scope's list.</b> The candidates are the other accounts of the scope's list of
/// the conversation's own agent; another agent is the <c>handOver</c> rule's (MIXAGENT1c). Where none has room it stays and says
/// nothing: the person's offer (MIXAGENT1b) is the fallback. <c>switch: off</c> moves nothing.</para>
/// </remarks>
/// <param name="handOver">The hand-over a move takes: a move by the curve is a hand-over with its agent unchanged.</param>
internal sealed class CurveMove(ChatContext context, ChatHandOver handOver)
{
    private readonly ChatContext _context = context;

    private readonly ChatHandOver _handOver = handOver;

    /// <summary>
    /// The conversations' move record (FOUNDATION1f-iii part a): the curve's move is decided there, as the live limit move's is since f-ii, over
    /// the home's one store file, so the shell's come-up takes a dead shell's curve move up through either holder. A test hands one over a store
    /// of its own run.
    /// </summary>
    internal ConversationMoves Moves { get; set; } = new(context.Home, context.Service, context.Events, context.Output);

    /// <summary>What each judged turn's end came to, by session: a test hears it; no person reads it.</summary>
    public event Action<string, SwitchChoice>? Judged;

    /// <summary>What the course answered each judged turn's end, by session (FOUNDATION1h branch 1): a test hears it; no person reads it.</summary>
    internal event Action<string, LineCourse>? Decided;

    /// <summary>
    /// Whether the turns, as just told, are a turn's end not yet judged (SWITCH1c-ii): nothing running, the door not opening, none
    /// of the person's words waiting, and a turn ended since <paramref name="judged"/>. A change for any other reason judges nothing.
    /// </summary>
    /// <param name="judged">The turn's end the conversation was last judged at, or null before any.</param>
    public static bool TurnJustEnded(ChatQueue queue, DateTimeOffset? judged) =>
        !queue.Taking && !queue.Opening && queue.Queued.Count == 0 && queue.LastTurnEnded is { } ended && ended != judged;

    /// <summary>A turn's end, told as the turns move: judged on a task of its own, as <see cref="ChatHandOver.Due"/>'s take is.</summary>
    public void Due(string sessionId, DriverConfig config, Func<string, string, Task>? onEnded)
    {
        if (_context.Closing) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await MoveAsync(sessionId, config, onEnded).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A judgement that failed moves nothing, and the conversation runs on where it is: the next turn's end judges again.
            }
        });
    }

    /// <summary>
    /// Judge a conversation by its account's usage curve and, where the decision says so, move it (SWITCH1c-ii). Its account, agent
    /// and workspace are its record's; the scope's switch and curve, the scope's list and what each account said are this machine's.
    /// </summary>
    /// <returns>What the decision said; null where nothing was judged (a hand-over waits or is taken, or it is no conversation of
    /// an account of an agent whose accounts the roster holds).</returns>
    public async Task<SwitchChoice?> MoveAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default)
    {
        // A hand-over's box an older build left, or its move the store carries and has not begun (FOUNDATION1f-v part a), holds the record from a move.
        if (_context.Closing || _context.GoingOn.ContainsKey(sessionId) || _handOver.Holds(sessionId))
        {
            return null;
        }

        // Ask Daoris's own conversations and intakes never go on elsewhere (D137 §2.2), and one on the tool's own home has no account.
        // The one exception is a limit with no account of its agent left (LIMITGOON2, D205 §3.5(e), LimitMove), never the curve.
        var prior = await _context.Service.RecordAsync(sessionId, ct).ConfigureAwait(false);
        if (prior is not { Kind: "chat", Teammate: false, Ask: null, Profile: { Length: > 0 } account, Adapter: { Length: > 0 } adapter }
            || string.Equals(prior.Repository, KnowledgeRoomIdentity.Repository, StringComparison.Ordinal))
        {
            return null;
        }

        string owner;
        string agent;
        RotationScope scope;
        try
        {
            var resolved = _context.Harnesses.Adapters.Resolve(adapter);
            if (resolved.Toolchain is not { } toolchain) return null;
            (agent, owner) = (resolved.Name, toolchain.Owner(resolved.Name));
            scope = _context.Harnesses.Settings.Set(owner);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }

        // Its last move by the curve is when its record was made, where it continues another by the curve (§2's hold).
        var lastMove = prior.FromWhy == HandOverWhy.Curve ? prior.Created : null;

        // MODELROLE1d2 (D205 §3.5(a)): each candidate judged by its offer reading for the model the record asks, the composer's kept
        // change's where one is, so the move never takes an account that does not offer the conversation's model.
        var model = (prior.Asked ?? ActorAsked.Before(agent)).Overlaid(ActorOverride.Read(prior.Override)).NamedModel;
        (string, string?, string)? offer = model is null
            ? null
            : (agent, (await _context.Harnesses.ReportAsync(agent, config, refresh: false, ct).ConfigureAwait(false))?.Version, model);
        var choice = Decide(othersRead: false);
        if (choice is SwitchChoice.ReadOthers { Accounts: var stale })
        {
            await _context.Harnesses.ReadUsageAsync(agent, owner, stale, config, ct).ConfigureAwait(false);
            choice = Decide(othersRead: true);
        }

        Judged?.Invoke(sessionId, choice);

        // FOUNDATION1h branch 1 (the foundation design §3.1): whether this line may move, and where, is the course's one decision. The account
        // reading above is the adapter's to read (the others' windows are read only where it says so); the scope's switch is the course's
        // consent (`SwitchAdmits`), so a hold, the switch's or a stay, is no move, and the account moved to is the course's answer.
        var course = CourseDecision.Decide(
            new CourseFacts(
                new CourseHead(
                    prior.State, Role: prior.Actor ?? "chat", Choice: prior.Choice, Adapter: adapter, Profile: account, Conversation: true,
                    AskDaoris: prior.Ask is not null, Session: sessionId),
                CourseBoundary.BetweenTurns,
                Rules: new CourseRules(SwitchAdmits: Admits(scope.Use), CurveAccount: (choice as SwitchChoice.MoveTo)?.Account)),
            CourseTrigger.TurnEnd);
        Decided?.Invoke(sessionId, course);
        if (course is not LineCourse.Move { Where.Account: { } to } || choice is not SwitchChoice.MoveTo move) return choice;

        // What the hand-over's own ask judged before it kept a box (no such session, a conversation that went on, an account not the agent's,
        // a tree gone): refused here the same way, quietly, before anything is decided.
        var (refused, _) = await HandOver.JudgeAsync(_context.Service, _handOver.AccountsOf, sessionId, agent, to, ct).ConfigureAwait(false);
        if (refused is not null) return choice;

        // FOUNDATION1f-iii part a (the foundation design §3.3, §13.4 step 1): the move is decided in the store, and no box is written beside it.
        // A record a move still stands for, the one it left or the one whose open made it, takes no move now; a store that will not take the
        // decision moves nothing, and the next turn's end judges again.
        var begun = await Moves.BeginAsync(Decision(sessionId, to), ct).ConfigureAwait(false);
        if (begun.Move is not { } record) return choice;

        // Taken now, the turns held while it is; where a word started a turn meanwhile, the move closes and the next turn's end judges again.
        var asked = new HandOverRequest(agent, to, HandOverWhy.Curve, HandOverBy.Daoris, DateTimeOffset.UtcNow);
        await _handOver.TakeAsync(sessionId, asked, move, record, config, onEnded, ct).ConfigureAwait(false);
        return choice;

        SwitchChoice Decide(bool othersRead)
        {
            var now = _context.Harnesses.Clock();
            return SwitchDecision.Decide(
                scope.Use, account, SaidOf(owner, account, now), _context.Harnesses.SwitchCandidates(owner, scope.List, offer), now, lastMove,
                othersRead);
        }
    }

    /// <summary>
    /// The course's consent for Daoris's own move (<see cref="CourseRules.SwitchAdmits"/>): the scope's switch is not <c>off</c>. Daoris's own
    /// offer is not the hand-over rule's (<see cref="HandOverRules"/>), so the person's hand-over rule never gates the curve.
    /// </summary>
    private static bool Admits(RotationUse use) => use.Switch != "off";

    /// <summary>
    /// The move as the course decided it (FOUNDATION1f-iii part a, §3.3): a conversation's, by the brief, for the curve, asked by Daoris, on the
    /// account the decision named; the agent is the conversation's own, so none is named.
    /// </summary>
    private static MoveDecision Decision(string sessionId, string account) =>
        new(sessionId, MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Curve, HandOverBy.Daoris) { Account = account, RecipeWhy = HandOverWhy.Curve };

    /// <summary>What the conversation's own account last said of its windows: its frames, or a read; null for nothing.</summary>
    private AccountSaid? SaidOf(string owner, string account, DateTimeOffset now)
    {
        try
        {
            return AccountWindows.SaidOf(_context.Harnesses.Home, owner, account, now);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
