using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The supervisor's decisions' shared state, one per <see cref="QuestionSupervisorDecisions"/>: the live decisions, one lock per session,
/// and the questions a decision closed as the owner's. The book owns that memory and the one move (<see cref="Move"/>), which writes the
/// state line through <see cref="Files"/>; the keeping, releasing, sweeping, ticket and read parts reach it and keep no state of their own.
/// </summary>
internal sealed class SupervisorBook(string home, MachineLog? log)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    /// <summary>The files a decision is written to and read back from as history.</summary>
    public SupervisorDecisionFiles Files { get; } = new(home);

    /// <summary>Every live decision by id.</summary>
    public ConcurrentDictionary<string, SupervisorLiveDecision> Live { get; } = new(StringComparer.Ordinal);

    /// <summary>Every course decision on a referred point by id (<see cref="SupervisorCourses"/>).</summary>
    public ConcurrentDictionary<string, SupervisorCourseDecision> Courses { get; } = new(StringComparer.Ordinal);

    /// <summary>Subjects a decision closed as the owner's, by kind and subject, with the state that closed them, across a restart too.</summary>
    public ConcurrentDictionary<SupervisorSubject, SupervisorState> Closed { get; } = new();

    /// <summary>Run <paramref name="body"/> under <paramref name="session"/>'s decision lock.</summary>
    public async Task<T> UnderAsync<T>(string session, Func<Task<T>> body, CancellationToken ct = default)
    {
        var gate = _locks.GetOrAdd(session, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await body().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Why a subject a decision closed refuses another act on it: the owner's again (<c>overruled</c>), or left (<c>answered</c>).</summary>
    public string? ClosedWhy(SupervisorSubject subject) =>
        Closed.TryGetValue(subject, out var state)
            ? state == SupervisorState.Left ? SupervisorWhy.Answered : SupervisorWhy.Overruled
            : null;

    /// <summary>A stale decision whose word is never taken back: the word of a release a revoke's sweep did not reach before it staled.</summary>
    public static bool WordLeft(SupervisorLiveDecision decision) =>
        decision.State == SupervisorState.Stale && decision.Word is not null && !decision.TakenBack;

    /// <summary>
    /// Take back <paramref name="decision"/>'s word, under its session's lock: once the service has taken it back the decision remembers
    /// it, so a second sweep finds nothing to take back. A refusal is not remembered, so the next sweep or press asks again. True where
    /// there is no word to take back or the service took it back.
    /// </summary>
    public static async Task<bool> TakeBackWordAsync(string session, SupervisorLiveDecision decision, SupervisorTakeBack takeBack, CancellationToken ct)
    {
        if (decision.Word is not { } word || decision.TakenBack) return true;
        var taken = await takeBack(session, word, ct).ConfigureAwait(false);
        if (taken) decision.TakenBack = true;
        return taken;
    }

    public IReadOnlyList<string> LiveIds(string session) =>
        [.. Live.Values.Where(each => each.Session == session).OrderBy(each => each.KeptAt).Select(each => each.Id)];

    /// <summary>Move <paramref name="decision"/> under its lock, by the move table alone; a move not in it is refused and changes nothing.</summary>
    public SupervisorState Move(SupervisorLiveDecision decision, SupervisorState to, DateTimeOffset at, string? why)
    {
        if (!SupervisorStates.Moves(decision.State, to)) return decision.State;
        Append(decision, to, at, why);
        if (SupervisorStates.OwnersAgain(to)) Closed[SupervisorSubject.Question(decision.Session, decision.Question.Id)] = to;
        if (to != SupervisorState.Admitted) decision.Ticket = null;
        var code = $"supervisor.{SupervisorStates.Spell(to)}";
        if (why is null) log?.Info(code, ("session", decision.Session));
        else log?.Info(code, ("session", decision.Session), ("why", why));
        return to;
    }

    /// <summary>Set <paramref name="decision"/>'s state and append its line to the decision's file.</summary>
    public void Append(SupervisorLiveDecision decision, SupervisorState to, DateTimeOffset at, string? why)
    {
        var line = SupervisorDecisionFiles.StateLine(to, at, why, to == SupervisorState.Released ? decision.Word : null);
        decision.State = to;
        decision.StateAt = at;
        decision.Why = why;
        decision.Lines.Add(line);
        SupervisorDecisionFiles.TryAppend(Files.StatesOf(decision.Session, decision.Id), line);
    }

    /// <summary>Try any move on a live decision, for the tests of the move table; true where it moved.</summary>
    public Task<bool> TryMoveAsync(string session, string decision, SupervisorState to, DateTimeOffset at) =>
        UnderAsync(session, () => Task.FromResult(
            Live.TryGetValue(decision, out var live) && SupervisorStates.Moves(live.State, to) && Move(live, to, at, null) == to));
}
