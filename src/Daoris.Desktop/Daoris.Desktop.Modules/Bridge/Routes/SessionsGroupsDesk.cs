using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// Where sessions are listed and what becomes of them, behind the sessions doors: where each one's work is, the groups the reader
/// places them in, archiving and unarchiving, and deleting a conversation that served no quest. Each is the driver library's one owner,
/// which the terminal's <c>sessions</c> verbs call too.
/// </summary>
internal sealed class SessionsGroupsDesk(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    public async Task<object?> WhereAsync(SessionWhereRead read, CancellationToken cancellationToken)
    {
        var trees = new SessionTrees(_loop.Home);
        var landings = trees.Recorded.Entries();
        var rows = new List<object>();
        if (read.Sessions is { } sessions)
        {
            foreach (var session in sessions)
            {
                if (session is not { Id: { } id, Tree: { } tree } || !HeldTree(trees, tree)) continue;

                cancellationToken.ThrowIfCancellationRequested();
                // As LandedBranches.Landing reads it: the newest entry naming the session.
                var landing = landings.LastOrDefault(entry => entry.Names(id));
                rows.Add(new
                {
                    Session = id,
                    TreeGone = TreeFolders.TreeGone(tree),
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
    internal static bool HeldTree(SessionTrees trees, string path)
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

    public async Task<object?> GroupsReadAsync(SessionGroupsRead press, CancellationToken cancellationToken)
    {
        var ids = Ids(press.Ids);
        var (look, grouped) = await GroupsAsync(ids, cancellationToken).ConfigureAwait(false);
        // The entries beside the rows.
        var sessions = grouped.Select(SessionsGroupsWires.Grouped).ToArray();
        var entries = SessionGroups.ReadEntries(look, ids).Select(SessionsGroupsWires.Entry).ToArray();
        if (!press.Said) return new { Sessions = sessions, Entries = entries };

        // `daoris session list` asks for its own lines from this look (the group, the repository and `--json` it is given),
        // which the alone path prints from a look of its own through the same function; the page asks for none.
        var ask = new SessionsAsk("list") { Group = press.Group, Repository = press.Repository, Json = press.Json };
        return new
        {
            Sessions = sessions, Entries = entries,
            Said = new TreesSaid(SessionsCommand.ListLines(look, ask, _loop.Home, DateTimeOffset.UtcNow), [], 0).ToWire(),
        };
    }

    public async Task<object?> ArchiveAsync(SessionArchiveMark press, CancellationToken cancellationToken)
    {
        var archive = press.Archived;
        var ids = Ids(press.Ids) ?? [];
        var marks = new ConversationArchive(_loop.Home);
        // `sessions archive` and `unarchive` sent over the pipe ask for their words (`said: true`), the alone path's own, and a
        // terminal's lines; the page asks for none, and its answers keep their shape.
        var said = press.Said;
        var records = await (_loop.Service ?? throw NotReady()).SessionRecordsAsync(cancellationToken).ConfigureAwait(false);

        if (!archive)
        {
            var back = marks.Unarchive(ids, records);
            var marked = back.Marks.Select(Mark).ToArray();
            return said
                ? new { Archived = marked, back.NotArchived, Said = ConversationArchive.UnarchiveSaid(ids, back.NotArchived).ToWire() }
                : new { Archived = marked, back.NotArchived };
        }

        var (look, grouped) = await GroupsAsync(ConversationArchive.Parts(ids, records), cancellationToken, walked: true).ConfigureAwait(false);
        var answer = marks.Archive(ids, grouped, look.Records, DateTimeOffset.UtcNow);
        var kept = answer.Outcomes.Where(outcome => outcome.Verdict != ArchiveVerdict.Archived).ToList();
        // counted in the machine log by the door it came in by: the screen's, or the terminal's over the pipe.
        ConversationArchive.Said(_loop.Log, answer.Outcomes.Count - kept.Count, PipeDoor.Now == PipeDoor.Terminal ? PluginEvents.Terminal : PluginEvents.Screen);
        var archived = answer.Marks.Select(Mark).ToArray();
        var keptRows = kept.Select(outcome => new { outcome.Session, Code = CodeOf(outcome.Verdict), outcome.Group }).ToArray();
        // A terminal is told one kept in its own words and exit, as it says it alone, never thrown.
        if (said) return new { Archived = archived, Kept = keptRows, Said = ConversationArchive.ArchiveSaid(answer.Outcomes).ToWire() };
        if (ids.Count == 1 && kept.Count == 1) throw Kept(kept[0]);

        return new { Archived = archived, Kept = keptRows };
    }

    /// <summary>One look at the sessions, gathered by the driver library's one gatherer, which the terminal's <c>sessions</c> reads too.</summary>
    /// <param name="walked">
    /// Whether the asked sessions' trees are walked with git now, as an archive's judgement walks them: a press is judged as the trees
    /// stand when it is pressed, never as the facts last judged them, so archive never hides work a tree gained since. The list's
    /// trees are the facts'.
    /// </param>
    private async Task<(SessionLook Look, IReadOnlyList<SessionGrouping> Grouped)> GroupsAsync(
        IReadOnlyCollection<string>? only, CancellationToken cancellationToken, bool walked = false)
    {
        var service = _loop.Service ?? throw NotReady();
        // the one look, in the groups slice's builder (no tree judged here unless walked; the waits, the calls and
        // the kept look are laid there), so the route and the pushed slice cannot drift.
        var look = await GroupsStateSlice.LookAsync(_loop, service, cancellationToken, only, walked).ConfigureAwait(false);

        return (look, SessionGroups.Read(look, only));
    }

    public async Task<object?> DeleteAsync(SessionDelete press, CancellationToken cancellationToken)
    {
        var (id, said, help) = press;
        var service = _loop.Service ?? throw NotReady();
        // `help delete <id> --yes` asks for its own words, its checks that the conversation is Ask Daoris's before the delete's.
        if (said && help)
        {
            return await SessionsVerbs.HelpSaidAsync(context, new HelpVerbAsk("delete") { Id = id, Yes = true }, cancellationToken).ConfigureAwait(false);
        }

        // `sessions delete` sent over the pipe asks for the verb's own words, a refusal said with its exit 1 as the alone path says it.
        if (said) return await SessionsVerbs.SaidAsync(context, new SessionsAsk("delete") { Ids = [id] }, cancellationToken).ConfigureAwait(false);

        var outcome = await new SessionDeletion(_loop.Home)
            .DeleteAsync(service, id, PluginEvents.Screen, _loop.Log, _loop.Events, cancellationToken)
            .ConfigureAwait(false);
        if (outcome.Verdict != DeleteVerdict.Deleted) throw NotDeleted(outcome);

        _loop.Nudge();
        return new { Deleted = id, outcome.Removed, outcome.Stayed };
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

    /// <summary>The session ids the page sent, or null where it sent none.</summary>
    internal static IReadOnlyCollection<string>? Ids(IReadOnlyList<string>? listed) =>
        listed is null ? null : [.. listed.Distinct(StringComparer.Ordinal)];

    private static object Mark(ArchiveMark mark) => new { mark.Session, mark.At };

    private static string CodeOf(ArchiveVerdict verdict) => verdict switch
    {
        ArchiveVerdict.Live => Refusals.SessionLive,
        ArchiveVerdict.NeedsYou => Refusals.SessionNeedsYou,
        _ => Refusals.SessionUnknown,
    };

    /// <summary>The refusal for one session archive kept; its group travels as the catalogue's <c>context</c> too.</summary>
    private static Exception Kept(ArchiveOutcome outcome) => outcome.Verdict switch
    {
        ArchiveVerdict.Live => Refusals.Because(
            Refusals.SessionLive, $"{outcome.Session} is still running, so it was not archived. Stop it first.",
            ("session", outcome.Session)),
        ArchiveVerdict.NeedsYou => Refusals.Because(
            Refusals.SessionNeedsYou,
            $"{outcome.Session} needs you, so it was not archived: archive never hides what needs you.",
            ("session", outcome.Session), ("group", outcome.Group ?? SessionGroup.NeedsYou), ("context", outcome.Group ?? SessionGroup.NeedsYou)),
        _ => Refusals.Because(
            Refusals.SessionUnknown, $"No session here is {outcome.Session}.", ("session", outcome.Session)),
    };
}
