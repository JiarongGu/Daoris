using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// What a session's own presses change, behind the sessions doors: a stop, the person's move of a parked record and the choice to go on
/// in a new session. Each goes through the driver library's one implementation, which the terminal's request is honoured by too.
/// </summary>
internal sealed class SessionsMoveDesk(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    public async Task<object?> StopAsync(StopSession press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        // `sessions stop` sent over the pipe asks for the verb's own words; the page asks for none and its answer keeps its shape.
        if (press.Said) return await SessionsVerbs.SaidAsync(context, new SessionsAsk("stop") { Ids = [id] }, cancellationToken).ConfigureAwait(false);

        // False is an answer, not an error: the session already finished, and its record says how.
        // 🔴 Unless the record says it still runs and nothing on this machine runs it — a chat
        // left behind by an application that closed, or a crash. Then the person's stop is how
        // it ends.
        // Which of the three it was is part of the answer: the page says it, and an orphan's
        // ending is not the person's — its record says nothing ran it.
        // 🔴 And when another Daoris process on this machine runs it — a terminal's chat — this
        // host can neither stop it nor call it ended: the record still says working.
        var answer = await SessionMoves.StopAsync(_loop.Processes, _loop.Service, id, cancellationToken).ConfigureAwait(false);
        // the process that runs it takes the stop, as its own; none that does not is claimed to have stopped.
        if (answer.Elsewhere
            && await SessionMoves.StopThroughOwnerAsync(
                _loop.Home, id, SessionMoves.OwnerWait, TimeSpan.FromMilliseconds(100), () => DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false))
        {
            answer = answer with { Stopped = true, Elsewhere = false };
        }

        _loop.Nudge();
        return new { answer.Stopped, answer.Orphan, answer.Elsewhere };
    }

    public async Task<object?> ResolveAsync(ResolveSession press, CancellationToken cancellationToken)
    {
        var (id, state, note, said) = press;

        if (state is not ("completed" or "declined" or "stopped"))
        {
            throw Refusals.Because(
                Refusals.SessionMoveNotYours, PersonMoves,
                ("state", state), ("moves", "completed, declined, stopped"));
        }

        if (state == "declined" && note is null)
        {
            throw Refusals.Because(Refusals.SessionDeclineNeedsReason, DeclineNeedsReason);
        }

        var service = _loop.Service ?? throw NotReady();

        // `sessions finish` and `decline` sent over the pipe ask for the verb's own words (`stopped` is the page's alone).
        if (said && state != "stopped")
        {
            return await SessionsVerbs.SaidAsync(
                    context, new SessionsAsk(state == "completed" ? "finish" : "decline") { Ids = [id], Note = note }, cancellationToken)
                .ConfigureAwait(false);
        }

        // The process first, then the record, with the person's words or the sentence that says they moved it: the one
        // implementation the terminal's request is honoured by too. False from the stop is the common case.
        var message = await SessionMoves.ResolveAsync(
            session => _loop.Processes.Stop(session), service, id, state, note, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();

        return new { Session = id, State = state, Message = message };
    }

    private const string PersonMoves =
        "A person may finish, decline or stop a parked session — not move it anywhere else. "
        + "Answering it so it carries on is a message, not a move.";

    private const string DeclineNeedsReason =
        "Declining needs a reason: it is the part whoever reads this record can act on.";
}
