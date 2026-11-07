namespace Daoris.Driver;

/// <summary>
/// A conversation whose provider kept failing past its episode goes on as its line's rules say: its tries rule's <c>next</c> or <c>weighted</c> moves it to another choice of its role, a new part
/// handed Daoris's brief (<c>transient</c>, asked by <c>daoris</c>) and counted in <c>moves</c>; an <c>ask</c>, or the moves rule's bound, holds it
/// for the person, whose next word goes on in it; a <c>hold</c> is the turn's end. Over the runner's context, as
/// <see cref="CurveMove"/> is.
/// </summary>
/// <remarks>
/// <para><b>Never mid-turn.</b> Judged once at the end of the turn the episode ended, with nothing running, opening or queued; the take holds the
/// turns, so a word said as it begins either runs its turn first, the move then closed, or waits for the new part.</para>
/// <para><b>A teammate's record, an intake and Ask Daoris's own room never move</b>, as the curve's move never takes them.</para>
/// </remarks>
[Executor("transient-move", ExecutorPhase.Move)]
internal sealed class TransientMove(IExecutorWiring wiring) : IExecutor
{
    private ChatContext context => wiring.Context;

    private ChatHandOver handOver => wiring.Of<ChatHandOver>();

    /// <summary>The conversations' move record, over the home's one store file, as the curve's and the limit's moves are.</summary>
    internal ConversationMoves Moves { get; set; } = new(wiring.Context.Home, wiring.Context.Service, wiring.Context.Events, wiring.Context.Output);

    /// <summary>The rules it reads; a test hands its own.</summary>
    internal ConversationRules? Rules { get; set; }

    /// <summary>What the course answered each judged end, by session: a test hears it; no person reads it.</summary>
    internal event Action<string, LineCourse>? Decided;

    /// <summary>The end of a turn its episode ended past its tries, told as the turns move: judged on a task of its own.</summary>
    public void Due(string sessionId, DriverConfig config, Func<string, string, Task>? onEnded)
    {
        if (context.Closing) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await MoveAsync(sessionId, config, onEnded).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A judgement that failed moves nothing: the turn ended as it always did, and the person's next word goes on in it.
            }
        });
    }

    /// <summary>
    /// Judge the conversation by its line's rules and, where they move it, move it; null where nothing is judged (a hand-over waits or is
    /// taken, or it is no conversation of this machine's that a rule may move).
    /// </summary>
    public async Task<LineCourse?> MoveAsync(string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default)
    {
        if (context.Closing || context.GoingOn.ContainsKey(sessionId) || handOver.Holds(sessionId)) return null;
        var json = await context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        if (ServiceClient.ReadRecord(json, sessionId) is not { Kind: "chat", Teammate: false, Ask: null, Adapter: { Length: > 0 } adapter } prior
            || string.Equals(prior.Repository, KnowledgeRoomIdentity.Repository, StringComparison.Ordinal))
        {
            return null;
        }

        var line = LineKeys.Of(sessionId, ServiceClient.ReadRecords(json)).Key;
        var rules = Rules ??= ConversationRules.Of(context);
        if (rules.Judge(prior, line, StaffConditions.Transient) is not { } ruled) return null;
        Decided?.Invoke(sessionId, ruled.Course);
        var tries = TransientRetry.Bound;
        switch (ruled.Course)
        {
            case LineCourse.Hold { Why: CourseHoldWhy.MovesBound }:
                rules.Said(ruled, sessionId, SupervisorRules.Moves, SupervisorRules.Ask, ruled.Moved.Moves, "episode");
                return ruled.Course;
            case LineCourse.Hold:
                rules.Said(ruled, sessionId, SupervisorRules.Tries, ruled.Action.Do, ruled.Action.After ?? tries, "episode");
                return ruled.Course;
            case not LineCourse.Move:
                return ruled.Course;
        }

        // Where the role holds no other choice the rule has nowhere to move it: the turn's end stands, and the person's word goes on in it.
        if (rules.Others(prior, ruled.Role, config).FirstOrDefault() is not { } next) return ruled.Course;
        var from = StaffCourse.ChoiceOf(prior, ruled.Policy)?.Text;
        if (!rules.Counted(ruled, sessionId, StaffConditions.Transient, from)) return ruled.Course;

        var begun = await Moves.BeginAsync(Decision(sessionId, next), ct).ConfigureAwait(false);
        if (begun.Move is not { } record) return ruled.Course;
        rules.Said(ruled, sessionId, SupervisorRules.Tries, ruled.Action.Do, ruled.Action.After ?? tries, "episode");
        context.Harnesses.StaffFor(context.Home).MarkNotAnswering(adapter);
        var asked = new HandOverRequest(next.Agent, null, HandOverWhy.Transient, HandOverBy.Daoris, DateTimeOffset.UtcNow, next.Model, next.Effort);
        await handOver.TakeRuledAsync(sessionId, asked, record, config, onEnded, ct).ConfigureAwait(false);
        return ruled.Course;
    }

    /// <summary>The move as the rules decided it: a conversation's, by the brief, for a service that did not answer, asked by Daoris, on the choice named.</summary>
    private static MoveDecision Decision(string sessionId, ActorEffective next) =>
        new(sessionId, MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Transient, HandOverBy.Daoris)
        {
            RecipeWhy = HandOverWhy.Transient, Agent = next.Agent, Model = next.Model, Effort = next.Effort,
        };
}
