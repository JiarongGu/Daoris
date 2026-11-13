using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// Ask Daoris, the page's `bridge/help.ts`: its conversation in its room, its history, and what its presses published.
/// </summary>
public sealed class HelpRoutes(DriverContext context)
{
    private const StateScopes Kept = StateScopes.Sessions | StateScopes.Help;

    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;
    private readonly IEventBus _events = context.Events;

    // A new Ask Daoris conversation, answered once placed and spawned off the request; a workspace this machine lacks is refused.
    [DriverRoute("START_HELP", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("START_HELP", None = OwnerNone.OwnConversation)]
    [GroundRepository("START_HELP", "workspace")]
    [Publishes("START_HELP", StateScopes.Sessions)]
    private async Task<object?> StartHelpAsync(StartHelp press, CancellationToken cancellationToken)
    {
        // Asked first: no service answer changes it, and it says what to do.
        var config = DriverConfig.Load(_loop.ConfigPath);
        if (ActorKey.FirstAgent("ask-daoris", config) is not { Length: > 0 } helper)
        {
            throw new DriverException(
                "Ask Daoris has no agent to run on — name one under Agents → Roles, Ask Daoris, or "
                + "`daoris role set ask-daoris \"<agent>\"`. Its starters need none.");
        }

        var named = WorkspaceAsked(press.Workspace);
        var circle = RemoteTarget.Workspace(named);
        var chat = _loop.Chat ?? throw NotReady();
        var service = _loop.Service ?? throw NotReady();
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var spelled = HeldWorkspace(snapshot, circle);
        var focus = HelpFocusRoutes.Asked(press.Focus, snapshot.Repositories);

        // A conversation of its own every press, beside any running or starting; its order walked under the mode standing now.
        var place = await chat.PlaceHelpAsync(
            helper, config, named is null ? null : spelled, cancellationToken, order: ActorChoices.Effective("ask-daoris", config)).ConfigureAwait(false);
        if (place.SessionId is not { } id) return new { SessionId = (string?)null, place.Message, Workspace = circle };

        _loop.HelpStarting.Placed(id);
        var started = HelpFocusRoutes.Started(_loop.Home, id, circle, focus);
        _ = Task.Run(() => RunPlacedAsync(chat, place, config));
        _loop.Nudge();
        return new { SessionId = id, place.Message, Starting = true, Workspace = circle, Focus = started };
    }

    private static string? WorkspaceAsked(string? workspace) => string.IsNullOrWhiteSpace(workspace) ? null : workspace.Trim();

    private static string HeldWorkspace(Snapshot snapshot, string circle)
    {
        var held = snapshot.Repositories.Select(known => known.Workspace).Append(RemoteTarget.DefaultWorkspace)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return held.FirstOrDefault(name => string.Equals(name, circle, StringComparison.OrdinalIgnoreCase))
            ?? throw new DriverException(
                $"there is no workspace `{circle}` on this machine — one of {string.Join(", ", held.Select(name => $"`{name}`"))}.");
    }

    /// <summary>
    /// The conversation placed, started off the request: the room read through the loop's <c>DescribeHelp</c>, then the agent
    /// spawned. Words said to it meanwhile are on its record, and go to its door in order once it opens; where it could not start,
    /// they wait there for its go-on.
    /// </summary>
    private async Task RunPlacedAsync(ChatRunner chat, HelpPlaced place, DriverConfig config)
    {
        var id = place.SessionId!;
        ChatStart? start = null;
        try
        {
            start = await chat.RunHelpAsync(
                place, config,
                ct => _loop.DescribeHelp is { } describe ? describe(ct) : Task.FromResult<HelpMachine?>(null),
                onEnded: (session, state) => DriverLoop.Ended(_events, session, state)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The runner ended the record saying why; nothing here could say more, and the words below are kept either way.
        }
        finally
        {
            // Over first, so a word said from here on goes the way any conversation's goes; what was kept on its record meanwhile goes to
            // its door, and where the start failed waits on the record for its go-on.
            await _loop.HelpStarting.OverAsync(id).ConfigureAwait(false);
            if (start?.SessionId is not null) await chat.TakeKeptAsync(id).ConfigureAwait(false);

            // What a word said to it now would do, told again: its own queue now, or its record.
            _loop.Words.Tell(id);
            _loop.Nudge();
        }
    }

    // what one conversation's presses published, the newest first, each by the ids the
    // service answered, as `HelpPublished` keeps them beside its other files. The page reads each one's state from the pushed quests,
    // asks and sessions. A read of this machine's home, off the window's thread: it declares nothing, and its key is under
    // `help`. Desktop-only, as the conversation is.
    [DriverRoute("HELP_PUBLISHED", PipeClass.Open)]
    private Task<object?> HelpPublishedAsync(HelpPublishedRead press, CancellationToken cancellationToken)
    {
        var session = press.Session;
        var home = _loop.Home;
        return Task.Run<object?>(() => PublishedAnswer(session, new HelpPublished(home).Read(session)), cancellationToken);
    }

    public static object PublishedAnswer(string session, IReadOnlyList<HelpPublishedEntry> published) => new
    {
        Session = session,
        Published = published.Select(entry => new { entry.At, entry.Proposal, entry.Kind, entry.Workspace, entry.Ask, entry.Quest }).ToArray(),
    };

    // ——— Ask Daoris's history: its conversations listed and searched, named, pinned and started from. Going on in
    // one is the person's words to it (`SESSION_INPUT`), and deleting one is `SESSION_DELETE`'s, so each has the one owner the
    // rest of the window uses; `daoris help` is the terminal's door to the same. This machine's and the person's:
    // the words are this machine's record, what the person keeps of each a file beside them (`HelpConversations`), and
    // nothing here reaches a remote.

    // A page of Ask Daoris's conversations, pinned first, then the newest; with `q`, those whose name or words hold it, each with
    // where. `offset` and `limit` are the terminal's `--offset` and `--limit`: every record is ordered and searched
    // before the page is taken, and the answer says how many there are and where the next page starts. `workspace` is the
    // terminal's `--workspace`: that workspace's conversations, searched and counted
    // before the page; none named is every workspace's, as `--all` says, each row naming its own.
    [DriverRoute("HELP_CONVERSATIONS", PipeClass.Open)]
    private async Task<object?> HelpConversationsAsync(HelpConversationsRead press, CancellationToken cancellationToken)
    {
        // `about`, a place's conversations, is never asked beside a workspace.
        var about = HelpFocusRoutes.Place(press.About);
        if (about is not null && !string.IsNullOrWhiteSpace(press.Workspace)) throw new DriverException("both: a list is one workspace's or one place's, not both.");

        // `help list` in the verb's words, `--json` its own answer.
        if (press.Said)
        {
            var listed = new HelpVerbAsk("list")
            {
                Search = press.Q?.Trim(),
                Offset = (int)Math.Clamp(press.Offset ?? 0, 0, int.MaxValue),
                Limit = press.Limit is { } limit ? (int)Math.Clamp(limit, 1, HelpConversations.PageLimit) : null,
                Workspace = press.Workspace,
                All = press.All,
                About = about?.Said,
                Json = press.Json,
            };
            return await SessionsVerbs.HelpSaidAsync(_context, listed, cancellationToken).ConfigureAwait(false);
        }

        var service = _loop.Service ?? throw NotReady();
        var records = SessionRecords.Parse(await service.SessionRecordsJsonAsync(cancellationToken).ConfigureAwait(false));
        var listing = new HelpConversations(_loop.Home).List(
            records, _loop.Events, Resumes, press.Q,
            (int)Math.Clamp(press.Offset ?? 0, 0, int.MaxValue),
            (int)Math.Clamp(press.Limit ?? HelpConversations.PageLimit, 0, HelpConversations.PageLimit),
            press.Workspace, about);
        return HistoryAnswer(listing);
    }

    /// <summary>
    /// A listing as the page reads it, its <c>HelpConversation</c>s: field for field what <c>daoris ask-daoris list --json</c>
    /// prints (<see cref="HelpCommand.JsonFields"/>). Public, as <see cref="Grouped"/> is, so its shape is tested without a service.
    /// </summary>
    public static object HistoryAnswer(HelpListing listing) => new
    {
        Conversations = listing.Conversations.Select(row => new
        {
            // the mode it ran under, its record's, after whether it runs, then the workspace it started in, then its focus.
            row.Session, row.Title, row.Name, row.Opening, row.About, row.Created, row.Last, row.Pinned, row.Live, row.Mode, row.Workspace,
            Focus = row.Focus.Wire(), FocusNext = row.FocusNext?.Wire(),
            Marks = row.Marks.Select(mark => mark.Wire()).ToArray(), row.Resumable, row.From, row.Handed, row.Found, row.FoundLine, row.Parts,
        }).ToArray(),
        listing.Cut,
        listing.Total,
        listing.Next,
    };

    // what each start of one Ask Daoris conversation handed its agent,
    // newest first, as the start kept it (`HelpWired`): `{ session, recorded, receipts }`, a conversation from before answering
    // `recorded: false`. Read from the home alone, off the window's thread, answered as the file's own JSON so a null is
    // kept. A read: it declares nothing. Desktop-only: its folders are this machine's, and no shared host or feed carries it.
    [DriverRoute("HELP_WIRED", PipeClass.Open)]
    private Task<object?> HelpWiredAsync(HelpWiredRead press, CancellationToken cancellationToken)
    {
        var session = press.Session;
        var home = _loop.Home;
        return Task.Run<object?>(() => HelpWired.Answer(session, new HelpWired(home).Read(session)), cancellationToken);
    }

    // A new Ask Daoris conversation from an earlier one: opened as START_HELP opens one, on the helper's agent, its
    // workspace's running conversation set aside first, and Daoris's brief of the earlier line handed with the person's first
    // words. Its end reaches the page as a start's does.
    [DriverRoute("HELP_START_FROM", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("HELP_START_FROM", Session = "id")]
    [GroundSession("HELP_START_FROM", "id")]
    [Publishes("HELP_START_FROM", Kept, Session = "id")]
    private async Task<object?> HelpStartFromAsync(HelpStartFrom press, CancellationToken cancellationToken)
    {
        var config = DriverConfig.Load(_loop.ConfigPath);
        if (ActorKey.FirstAgent("ask-daoris", config) is not { Length: > 0 } helper)
        {
            throw new DriverException(
                "Ask Daoris has no agent to run on — name one under Agents → Roles, Ask Daoris, or "
                + "`daoris role set ask-daoris \"<agent>\"`. Its starters need none.");
        }

        var id = await HelpConversationOfAsync(press.Id, cancellationToken).ConfigureAwait(false);
        var chat = _loop.Chat ?? throw NotReady();
        var service = _loop.Service ?? throw NotReady();

        // in the workspace its press names, as START_HELP's, whatever the earlier one's is, beside any running.
        var named = WorkspaceAsked(press.Workspace);
        var circle = RemoteTarget.Workspace(named);
        var spelled = HeldWorkspace(await service.SnapshotAsync(cancellationToken).ConfigureAwait(false), circle);

        // Placed as START_HELP places one: answered once the record is open and the earlier words kept; the room is
        // read through the loop's DescribeHelp and the agent spawned after, off the request, so a slow room cannot pass the page's bound.
        var place = await chat.PlaceHelpFromAsync(
            id, helper, config, named is null ? null : spelled, cancellationToken, ActorChoices.Effective("ask-daoris", config)).ConfigureAwait(false);
        if (place.SessionId is not { } placedId) return new { SessionId = (string?)null, place.Message, From = id, Workspace = circle };

        _loop.HelpStarting.Placed(placedId);
        var focus = await HelpFocusRoutes.CarriedAsync(_loop, id, placedId, circle, cancellationToken).ConfigureAwait(false);
        _ = Task.Run(() => RunPlacedAsync(chat, place, config));
        _loop.Nudge();
        return new { SessionId = placedId, place.Message, From = id, Workspace = circle, Focus = focus };
    }

    /// <summary>
    /// The id when it names an Ask Daoris conversation of this machine's, judged from the records as the service answers them;
    /// otherwise the sentence why not, which the page says where it is pressed.
    /// </summary>
    private async Task<string> HelpConversationOfAsync(string id, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var record = SessionRecords.Parse(await service.SessionRecordsJsonAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) throw new DriverException($"no conversation here is `{id}` any more.");
        if (record.Teammate || !WordsNever.IsHelp(record))
        {
            throw new DriverException($"`{id}` is no Ask Daoris conversation of this machine's.");
        }

        return id;
    }

    /// <summary>Whether an adapter's door resumes a conversation by its id: one this machine no longer has cannot.</summary>
    private bool Resumes(string adapter)
    {
        try
        {
            return _loop.Harnesses.Adapters.Door(adapter).Can.Resumes;
        }
        catch (DriverException)
        {
            return false;
        }
    }
}
