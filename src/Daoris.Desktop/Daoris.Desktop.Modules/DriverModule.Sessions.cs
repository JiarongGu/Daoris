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
        // 🔴 And when another Daoris process on this machine runs it — a terminal's chat — this
        // host can neither stop it nor call it ended: the record still says working (REV3).
        // The one implementation the terminal's request is honoured by too (SESSUX1g).
        var answer = await SessionMoves.StopAsync(_loop.Processes, _loop.Service, id, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();
        return new { answer.Stopped, answer.Orphan, answer.Elsewhere };
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

        // The process first, then the record, with the person's words or the sentence that says they moved it: the one
        // implementation the terminal's request is honoured by too (SESSUX1g). False from the stop is the common case.
        var message = await SessionMoves.ResolveAsync(
            session => _loop.Processes.Stop(session), service, id, state, note, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();

        return new { Session = id, State = state, Message = message };
    }

    // *Go on in a new session* (MSG1g, D137 §2.2): words a resume holds while the account their session ran on cools go on now
    // in a new session, handed them, without that conversation. The driver's door judges and keeps the person's choice
    // (D143 point 4), the same door `daoris-driver sessions go-on-new` calls (D50); the loop is nudged so its next look carries
    // them on. What cannot is `sent: false` with its code, for the page to word, and the driver's sentence beside it. The
    // page's press is a later row.
    [DriverRoute("SESSION_GO_ON_NEW")]
    private async Task<object?> SessionGoOnNewAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();
        var answer = await GoOnNew.AskAsync(
                service, _loop.Home, (adapter, profile) => _loop.Harnesses.CoolingOf(adapter ?? "", profile), id,
                DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        if (answer.Sent) _loop.Nudge();
        return new { answer.Sent, answer.Why, answer.Message };
    }

    private const string PersonMoves =
        "A person may finish, decline or stop a parked session — not move it anywhere else. "
        + "Answering it so it carries on is a message, not a move.";

    private const string DeclineNeedsReason =
        "Declining needs a reason: it is the part whoever reads this record can act on.";

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

    /// <summary>
    /// Where each session is listed by state (SESSUX1a, D126 §2.4): its group, the word its row shows, and what its second
    /// line says, in the order a list shows them, read by the driver library's one reader, which the terminal's
    /// <c>sessions</c> prints too. With <c>ids</c>, those sessions' alone.
    /// </summary>
    /// <remarks>
    /// <para><b>From the records, the quests, the planner's verdicts, the trees and the marks</b>: the loop's last look where
    /// it has looked, a plan over a fresh snapshot where it has not; git asked only of an ended session's own tree that
    /// could be to review. Never from what a session printed.</para>
    ///
    /// <para><b>Nothing machine-local comes back</b>: ids, groups, words and counts. A tree's path stays here.</para>
    /// </remarks>
    [DriverRoute("SESSION_GROUPS")]
    private async Task<object?> SessionGroupsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (_, grouped) = await GroupsAsync(Ids(request), cancellationToken).ConfigureAwait(false);
        return new { Sessions = grouped.Select(Grouped).ToArray() };
    }

    /// <summary>
    /// Archive these sessions on this machine, or bring them back (SESSUX1a, D126 §5.2): the marks as they now stand.
    /// </summary>
    /// <remarks>
    /// <para><b>Each is judged by where the reader places it now</b>: a live one, one waiting on you and one with work to
    /// review are refused, so archive never hides what needs the person, and an id no record has is refused too. Asked of
    /// one session, a refusal is the answer, in the catalogue's words; asked of several, as <i>Archive what ended</i>'s
    /// second press asks, what may go is archived and the rest are kept, each with its code, since a list is a fact about
    /// a moment.</para>
    ///
    /// <para><b>Unarchive needs no service</b>: it takes marks away, and one that was not archived is said, never refused
    /// (D48 §6).</para>
    /// </remarks>
    [DriverRoute("SESSION_ARCHIVE")]
    private async Task<object?> SessionArchiveAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var archive = PayloadHelper.GetRequiredValue<bool>(request.Payload, "archived");
        var ids = Ids(request) ?? [];
        var marks = new SessionArchive(_loop.Home);

        if (!archive)
        {
            var back = marks.Unarchive(ids);
            return new { Archived = back.Marks.Select(Mark).ToArray(), back.NotArchived };
        }

        var (look, grouped) = await GroupsAsync(ids, cancellationToken).ConfigureAwait(false);
        var answer = marks.Archive(ids, grouped, [.. look.Records.Select(record => record.Id)], DateTimeOffset.UtcNow);
        var kept = answer.Outcomes.Where(outcome => outcome.Verdict != ArchiveVerdict.Archived).ToList();
        // SESSUX1g (D126 §7.4): counted in the machine log as the screen's, as the terminal's archive is counted as its own.
        SessionArchive.Said(_loop.Log, answer.Outcomes.Count - kept.Count, PluginEvents.Screen);
        if (ids.Count == 1 && kept.Count == 1) throw Kept(kept[0]);

        return new
        {
            Archived = answer.Marks.Select(Mark).ToArray(),
            Kept = kept.Select(outcome => new { outcome.Session, Code = CodeOf(outcome.Verdict), outcome.Group }).ToArray(),
        };
    }

    /// <summary>One look at the sessions, gathered by the driver library's one gatherer, which the terminal's <c>sessions</c> reads too.</summary>
    private async Task<(SessionLook Look, IReadOnlyList<SessionGrouping> Grouped)> GroupsAsync(
        IReadOnlyCollection<string>? only, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);
        var look = await SessionGroups.LookAsync(service, config, Door(config), _loop.Look.Latest, _loop.Home, only, cancellationToken)
            .ConfigureAwait(false);
        // The cool-offs the loop's last look held starts on (MSG1f2): what a record whose words one holds says, its reset.
        look = look with { Waits = _loop.Look.Waits };
        return (look, SessionGroups.Read(look, only));
    }

    /// <summary>
    /// Delete a conversation that served no quest (SESSUX1f, D126 §5.4): its record through the ledger, then what this
    /// machine kept of it, by the driver library's one owner, which the terminal's <c>sessions delete</c> calls too.
    /// </summary>
    /// <remarks>
    /// <para><b>The ledger's half is said first</b>: whose the record is, whether it runs, whether it served a quest, what
    /// names it and whether a remote holds it; then this machine's, its tree and a landing. Each refusal is a code in
    /// <see cref="Refusals"/> said in the catalogue's words, read by the ledger's word, never its sentence. The page offers
    /// the act only where <c>SESSION_GROUPS</c> says <c>deletable</c>, so a refusal answers a race.</para>
    ///
    /// <para><b>Nothing machine-local comes back</b>: the id and the names of what went. <c>session.deleted</c> is written
    /// to the machine log with no word of it.</para>
    /// </remarks>
    [DriverRoute("SESSION_DELETE")]
    private async Task<object?> SessionDeleteAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();
        var outcome = await new SessionDeletion(_loop.Home)
            .DeleteAsync(service, id, PluginEvents.Screen, _loop.Log, _loop.Events, cancellationToken)
            .ConfigureAwait(false);
        if (outcome.Verdict != DeleteVerdict.Deleted) throw NotDeleted(outcome);

        _loop.Nudge();
        return new { Deleted = id, outcome.Removed };
    }

    /// <summary>
    /// A delete refused, as the catalogue says it: the session, and the fact its sentence names. Which named it, and the
    /// archive's two codes asked of a delete, travel as the catalogue's <c>context</c>.
    /// </summary>
    private static Exception NotDeleted(DeleteOutcome outcome)
    {
        var session = ("session", outcome.Session);
        return outcome.Verdict switch
        {
            DeleteVerdict.Unknown => Refusals.Because(Refusals.SessionUnknown, outcome.Message, session, ("context", "delete")),
            DeleteVerdict.Live => Refusals.Because(Refusals.SessionLive, outcome.Message, session, ("context", "delete")),
            DeleteVerdict.NotOurs => Refusals.Because(Refusals.SessionNotOurs, outcome.Message, session, ("machine", outcome.Machine ?? "")),
            DeleteVerdict.ServedQuest => Refusals.Because(Refusals.SessionServedQuest, outcome.Message, session, ("quest", outcome.Quest ?? "")),
            DeleteVerdict.Named => Refusals.Because(
                Refusals.SessionNamed, outcome.Message, session, ("context", outcome.NamedBy ?? SessionDeletion.ByAsk),
                ("ask", outcome.Ask ?? ""), ("quest", outcome.Quest ?? "")),
            DeleteVerdict.TreeHere => Refusals.Because(Refusals.SessionTreeHere, outcome.Message, session),
            _ => Refusals.Because(Refusals.SessionOnRemote, outcome.Message, session, ("workspace", outcome.Workspace ?? "")),
        };
    }

    /// <summary>The configured adapter's wire, as the loop plans by it (D70); the pipe, the stricter door, when it names none this build has.</summary>
    private SessionWire Door(DriverConfig config)
    {
        try
        {
            return _loop.Harnesses.Adapters.Resolve(config.Adapter).Wire;
        }
        catch (DriverException)
        {
            return SessionWire.Pipe;
        }
    }

    /// <summary>The session ids the page sent, or null where it sent none.</summary>
    private static IReadOnlyCollection<string>? Ids(IpcRequest request) =>
        request.Payload is { } payload && payload.TryGetProperty("ids", out var named) && named.ValueKind == JsonValueKind.Array
            ? [.. named.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!).Distinct(StringComparer.Ordinal)]
            : null;

    private static object Grouped(SessionGrouping row) => new
    {
        row.Session,
        row.Group,
        row.Shown,
        row.Archived,
        row.Teammate,
        row.Strikes,
        row.Awaits,
        row.AwaitsOf,
        Work = row.Work is { } work ? new { work.Commits, work.Uncommitted } : null,
        // SESSUX1b (D126 §2.2): its stop holds its quest here, which its line says.
        row.HoldsQuest,
        // PAUSE1b (D132 §6.1): whose pause holds its quest, which its line says and whose *Resume* stands in *Try again*'s place.
        PausedBy = row.PausedBy is { } pause ? new { Scope = pause.Word, pause.Id } : null,
        // MSG1f2 (D137 §3.2): what holds the person's words on a record that resumes later, which its line says.
        Holds = row.Holds is { } holds ? new { holds.Why, holds.Reason, holds.Repository, holds.Until } : null,
        // SESSUX1f (D126 §5.4): *Delete…* is offered only where it would be taken.
        row.Deletable,
    };

    private static object Mark(ArchiveMark mark) => new { mark.Session, mark.At };

    private static string CodeOf(ArchiveVerdict verdict) => verdict switch
    {
        ArchiveVerdict.Live => Refusals.SessionLive,
        ArchiveVerdict.NeedsYou => Refusals.SessionNeedsYou,
        _ => Refusals.SessionUnknown,
    };

    /// <summary>
    /// The refusal for one session archive kept. A group travels as the catalogue's <c>context</c> too, so the page's
    /// sentence names which: waiting on you, or to review.
    /// </summary>
    private static Exception Kept(ArchiveOutcome outcome) => outcome.Verdict switch
    {
        ArchiveVerdict.Live => Refusals.Because(
            Refusals.SessionLive, $"{outcome.Session} is still running, so it was not archived. Stop it first.",
            ("session", outcome.Session)),
        ArchiveVerdict.NeedsYou => Refusals.Because(
            Refusals.SessionNeedsYou,
            $"{outcome.Session} {(outcome.Group == SessionGroup.Review ? "has work to review" : "is waiting on you")}, so it was not "
            + "archived: archive never hides what needs you.",
            ("session", outcome.Session), ("group", outcome.Group ?? SessionGroup.You), ("context", outcome.Group ?? SessionGroup.You)),
        _ => Refusals.Because(
            Refusals.SessionUnknown, $"No session here is {outcome.Session}.", ("session", outcome.Session)),
    };

    /// <summary>
    /// *Open folder* (SESSUX1d, D126 §3.5): the folder a session worked in, its own tree or its repository's checkout,
    /// opened in the system's file manager through the window kit's launcher, as the log's folder and a plugin's open.
    /// </summary>
    /// <remarks>
    /// <para><b>The module names the folder, never the page</b>: it is read from the session's record, and only a tree this
    /// home opened or the checkout the registry names for its repository is opened. A record naming any other folder, a
    /// teammate's record and a tree a tidy took are one refusal, <c>SESSION_FOLDER_GONE</c>, since each is a folder this
    /// machine does not hold for that session. The page offers the press only where it knows the folder is here, so the
    /// refusal answers a race.</para>
    ///
    /// <para><b>Nothing machine-local comes back</b>: whether it opened, and nothing else.</para>
    /// </remarks>
    [DriverRoute("SESSION_OPEN_FOLDER")]
    private async Task<object?> SessionOpenFolderAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var service = _loop.Service ?? throw NotReady();
        var records = SessionRecords.Parse(await SessionRecords.ReadAsync(
            service.BaseUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable), ct: cancellationToken).ConfigureAwait(false));
        var record = records.FirstOrDefault(row => string.Equals(row.Id, id, StringComparison.Ordinal))
            ?? throw Refusals.Because(
                Refusals.SessionUnknown, $"No session here is {id}, so there is no folder to open.",
                ("session", id), ("context", "folder"));

        var folder = await FolderOfAsync(service, record, cancellationToken).ConfigureAwait(false)
            ?? throw Refusals.Because(
                Refusals.SessionFolderGone,
                $"The folder {id} worked in is not on this machine: the clean-up took its tree, or it ran somewhere else.",
                ("session", id));

        if (_openFolder is null) return new { Opened = false };
        try
        {
            _openFolder(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw Refusals.Because(
                Refusals.SessionFolderNotOpened,
                $"the folder would not open: {error.Message}",
                ("session", id), ("problem", error.Message));
        }

        return new { Opened = true };
    }

    /// <summary>
    /// The folder a session worked in, where this machine holds it: its own tree under this home's trees, else its
    /// repository's registered checkout when the record names that or no folder at all. Null for anything else.
    /// </summary>
    private async Task<string?> FolderOfAsync(ServiceClient service, SessionRecord record, CancellationToken cancellationToken)
    {
        // A teammate's record ran on their machine (SYNC4): a path it names is theirs, whatever this disk holds there.
        if (record.Teammate) return null;

        var trees = new SessionTrees(_loop.Home);
        if (record.Tree is { } tree && HeldTree(trees, tree)) return Directory.Exists(tree) ? tree : null;

        var root = (await service.RegistryAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(row => string.Equals(row.Repository, record.Repository, StringComparison.Ordinal))?.Root;
        if (root is null || (record.Tree is { } named && !SamePlace(named, root))) return null;
        return Directory.Exists(root) ? root : null;
    }

    /// <summary>Two paths as one place: separators and a trailing one ignored, and case where the system ignores it.</summary>
    private static bool SamePlace(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)).Replace('\\', '/'),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)).Replace('\\', '/'),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
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
