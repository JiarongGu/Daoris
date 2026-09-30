using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A session as the rail and its drawer act on it, the page's `bridge/sessions.ts` (MOD5): a stop, the
/// person's answer to a parked session (D52 §4), and what sessions opened with and said, read off this
/// machine's own record (RAIL1).
/// </summary>
public sealed partial class DriverModule
{
    [DriverRoute("STOP_SESSION")]
    private async Task<object?> StopSessionAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        // False is an answer, not an error: the session already finished, and its record says how.
        // 🔴 Unless the record says it still runs and nothing on this machine runs it — a chat
        // left behind by an application that closed, or a crash. Then the person's stop is how
        // it ends: pressing it used to change nothing and say nothing (2026-09-25).
        // Which of the three it was is part of the answer: the page says it, and an orphan's
        // ending is not the person's — its record says nothing ran it.
        var stopped = _loop.Processes.Stop(id);
        var orphan = !stopped
            && _loop.Service is { } service
            && (await Orphans.EndAsync(service, _loop.Processes, only: id, ct: cancellationToken)
                .ConfigureAwait(false)).Count > 0;
        // 🔴 And when another Daoris process on this machine runs it — a terminal's chat — this
        // host can neither stop it nor call it ended: the record still says working (REV3).
        var elsewhere = !stopped && !orphan && _loop.Processes.AliveOnThisMachine(id);
        _loop.Nudge();
        return new { Stopped = stopped || orphan, Orphan = orphan, Elsewhere = elsewhere };
    }

    // The person's answer to a session parked at a checkpoint (D52 §4). It goes through the
    // DRIVER rather than straight to the service because the two halves must move together:
    // this machine lets the process go, and only then does the record say it ended.
    /// <summary>
    /// The three moves a person may make on a parked session — `completed`, `declined`, `stopped`,
    /// each carrying what they want the record to say (design §4).
    /// </summary>
    /// <remarks>
    /// <para><b>Narrowed here, not re-judged.</b> The ledger allows a fourth from `awaiting-person`
    /// — back to `working` — and that one is the DRIVER's observation, not a button: a person
    /// resumes a conversation by answering it, which is the composer's job. Narrowing the person's
    /// verbs is a surface rule and belongs on the surface; everything about whether the move is
    /// legal at all stays the ledger's (D36), and its refusal reaches the person verbatim.</para>
    ///
    /// <para><b>The process goes first.</b> A record that says `completed` while this machine still
    /// holds the process is exactly the lie the observed lifecycle exists to prevent — so the
    /// process is let go, and the record moves after. `Stop` answering false is not an error: the
    /// common case is a session parked with nothing of ours still running.</para>
    ///
    /// <para><b>Declining needs a reason</b>, the same rule the quest door already holds and for the
    /// same reason: the note is the part whoever reads the record can act on.</para>
    ///
    /// <para><b>A move with no note still writes one.</b> The store keeps the previous note when a
    /// move carries none (deliberately — a later move must not erase what an earlier one recorded),
    /// so a session finished at a checkpoint would otherwise read <i>reached completed</i> beside
    /// the analysis it was parked with, which says the opposite of what happened. The stamped
    /// sentence also carries the one fact the state cannot: `completed` normally means the session
    /// closed its own quest, and this one means a person decided it was done.</para>
    /// </remarks>
    [DriverRoute("RESOLVE_SESSION")]
    private async Task<object?> ResolveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var state = PayloadHelper.GetRequiredValue<string>(request.Payload, "state");
        var note = Optional(request, "note");

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

        // False is the common case, not a failure: a parked session usually has no process here.
        _loop.Processes.Stop(id);
        var message = await service.AdvanceAsync(
            id, state, note ?? ByThePerson(state), ct: cancellationToken);
        _loop.Nudge();

        return new { Session = id, State = state, Message = message };
    }

    private const string PersonMoves =
        "A person may finish, decline or stop a parked session — not move it anywhere else. "
        + "Answering it so it carries on is a message, not a move.";

    private const string DeclineNeedsReason =
        "Declining needs a reason: it is the part whoever reads this record can act on.";

    /// <summary>What the record says when the person wrote nothing — never translated: it is data.</summary>
    private static string ByThePerson(string state) => state switch
    {
        "completed" => "The person finished this at a checkpoint.",
        "stopped" => "The person stopped this at a checkpoint.",
        _ => "The person moved this at a checkpoint.",
    };

    // What a person first said in each of these sessions (RAIL1): a conversation's identity, from
    // this machine's own record — never the session record, which travels (D47 §4).
    [DriverRoute("SESSION_OPENINGS")]
    private async Task<object?> SessionOpeningsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        if (request.Payload is { } payload && payload.TryGetProperty("ids", out var named)
            && named.ValueKind == JsonValueKind.Array)
        {
            ids.AddRange(named.EnumerateArray()
                .Where(id => id.ValueKind == JsonValueKind.String)
                .Select(id => id.GetString()!));
        }

        await Task.CompletedTask;
        return new { Openings = _loop.Events.Openings(ids) };
    }

    /// <summary>
    /// Where each of these sessions' work is now (LOOK2b), for the rail in one ask: whether the tree it opened is still
    /// here, and its landing, standing or a trace (D113), so a row says where the work landed rather than naming a tree a
    /// landing tidied away.
    /// </summary>
    /// <remarks>
    /// <para><b>From this machine's own files, and cheap</b>: the landing record and whether a folder is there. No service
    /// is asked and no git is run, so it answers on a cold start, and a landing's branch is read as the record holds it:
    /// the review is where git is asked where that branch stands (D113 §1).</para>
    ///
    /// <para><b>Only this home's trees are looked at.</b> The page sends the tree each record names; one outside the trees
    /// this home opened is a session in a repository's own checkout, which no landing tidies, and nothing is said of
    /// it. Nothing machine-local comes back.</para>
    /// </remarks>
    [DriverRoute("SESSION_WHERE")]
    private async Task<object?> SessionWhereAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var trees = new SessionTrees(_loop.Home);
        var rows = new List<object>();
        if (request.Payload is { } payload && payload.TryGetProperty("sessions", out var sessions)
            && sessions.ValueKind == JsonValueKind.Array)
        {
            foreach (var session in sessions.EnumerateArray())
            {
                if (session.ValueKind != JsonValueKind.Object
                    || !session.TryGetProperty("id", out var named) || named.ValueKind != JsonValueKind.String
                    || !session.TryGetProperty("tree", out var at) || at.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var id = named.GetString()!;
                var tree = at.GetString()!;
                if (!HeldTree(trees, tree)) continue;

                var landing = trees.Recorded.Landing(id);
                rows.Add(new
                {
                    Session = id,
                    TreeGone = SessionTrees.TreeGone(tree),
                    Landed = landing is null
                        ? null
                        : new
                        {
                            landing.Repository,
                            landing.Branch,
                            State = landing.GoneAt is null ? LandedState.Standing : LandedState.Gone,
                        },
                });
            }
        }

        await Task.CompletedTask;
        return new { Sessions = rows };
    }

    /// <summary>Whether a path names a tree this home opened; a path no folder could have is none.</summary>
    private static bool HeldTree(SessionTrees trees, string path)
    {
        try
        {
            return path.Length > 0 && trees.Holds(path);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    // What sessions said, searched (RAIL1): the person's words and the agent's, from this machine's
    // own record, bounded and saying so.
    [DriverRoute("SESSION_SEARCH")]
    private async Task<object?> SessionSearchAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var query = PayloadHelper.GetRequiredValue<string>(request.Payload, "q");
        await Task.CompletedTask;
        // Within one session, where the page names it (SESS1 S9): its words and its calls' titles.
        var found = Optional(request, "session") is { } one ? _loop.Events.Within(one, query) : _loop.Events.Search(query);
        return new
        {
            Query = query,
            Hits = found.Hits.Select(hit => new { hit.Session, hit.Seq, hit.Kind, hit.Snippet }).ToArray(),
            found.Cut,
        };
    }
}
