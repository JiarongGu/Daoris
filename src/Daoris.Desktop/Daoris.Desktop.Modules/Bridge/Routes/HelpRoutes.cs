using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// Ask Daoris, the page's `bridge/help.ts`: its conversation in its room, its proposals
/// and the person's Apply or Not now, and the doors an Apply goes through, each a screen's own route.
/// </summary>
public sealed class HelpRoutes(DriverContext context)
{
    /// <summary>What a history act changes: Ask Daoris's history, and the conversations the panel shows.</summary>
    private const StateScopes Kept = StateScopes.Sessions | StateScopes.Help;

    /// <summary>
    /// What an Apply may change, every door it holds: the page's hook asked each of these again by hand before.
    /// </summary>
    /// <remarks>LAYOUT8: a set-up's Apply adds a rule to the repository's scope, so the rules are among them.</remarks>
    private const StateScopes Applies = StateScopes.Sessions | StateScopes.Quests | StateScopes.Asks | StateScopes.Driver
        | StateScopes.Rules | StateScopes.History | StateScopes.Registry | StateScopes.Plugins | StateScopes.Harnesses
        | StateScopes.Help | StateScopes.Browser;

    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;
    private readonly IEventBus _events = context.Events;

    private readonly HelpProposalFacts _facts = new(context);

    // Ask Daoris's conversation, one per workspace: the one running in the press's workspace, carried on, or a new one in its room.
    // Answered once the conversation is placed, with `starting`, its agent spawned after, off the request, since on a loaded
    // machine the room's look alone took 24.9 s and the page stops waiting at 30. `workspace` is read at the press and carried to
    // the record, so a window that changes workspace while it starts moves nothing; a name this machine does not hold is refused.
    [DriverRoute("START_HELP", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("START_HELP", None = OwnerNone.OwnConversation)]
    [GroundRepository("START_HELP", "workspace")]
    [Publishes("START_HELP", StateScopes.Sessions)]
    private async Task<object?> StartHelpAsync(StartHelp press, CancellationToken cancellationToken)
    {
        // Asked first: no service answer changes it, and it says what to do.
        var config = DriverConfig.Load(_loop.ConfigPath);
        if (config.HelperAdapter is not { Length: > 0 } helper)
        {
            throw new DriverException(
                "Ask Daoris has no agent to run on — name one under Agents → Roles, Ask Daoris, or "
                + "`daoris role set ask-daoris \"<agent>\"`. Its starters need none.");
        }

        var named = WorkspaceAsked(press.Workspace);
        var circle = RemoteTarget.Workspace(named);
        var chat = _loop.Chat ?? throw NotReady();
        var service = _loop.Service ?? throw NotReady();
        var starting = _loop.HelpStarting;
        var placing = starting.PlacingIn(circle);
        await placing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (starting.SessionIn(circle) is { } placed)
            {
                return new { SessionId = placed, Message = $"Ask Daoris `{placed}` is starting.", Running = false, Starting = true, Workspace = circle };
            }

            var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
            var spelled = HeldWorkspace(snapshot, circle);
            var held = _loop.Processes.Running;
            var runningHere = snapshot.Active.Where(session =>
                session.Repository == HelpRoom.Repository && held.Contains(session.Id, StringComparer.OrdinalIgnoreCase)
                && string.Equals(session.Workspace, circle, StringComparison.OrdinalIgnoreCase)).ToList();
            if (runningHere.FirstOrDefault(session => !chat.Finishing(session.Id)) is { } running)
            {
                return new { SessionId = running.Id, Message = $"Ask Daoris `{running.Id}` is carried on.", Running = true, Workspace = circle };
            }

            // one the person finished is winding up, and the person's words are a new conversation's. It is waited for
            // a moment, since the ledger holds one running in a workspace; past that it is answered as still finishing, which the page
            // waits out before it asks again, never handing it the new conversation's words.
            foreach (var ending in runningHere)
            {
                if (!await chat.WoundUpAsync(ending.Id, FinishingBound, cancellationToken).ConfigureAwait(false))
                {
                    return new
                    {
                        SessionId = ending.Id, Message = $"Ask Daoris `{ending.Id}` is finishing.", Running = true, Finishing = true, Workspace = circle,
                    };
                }
            }

            // Ask Daoris's order walked, each entry judged by its access under the mode standing now.
            var place = await chat.PlaceHelpAsync(
                helper, config, named is null ? null : spelled, cancellationToken, order: ActorChoices.Effective("ask-daoris", config)).ConfigureAwait(false);
            if (place.SessionId is not { } id) return new { SessionId = (string?)null, place.Message, Running = false, Workspace = circle };

            starting.Placed(circle, id);
            _ = Task.Run(() => RunPlacedAsync(chat, place, config));
            _loop.Nudge();
            return new { SessionId = id, place.Message, Running = false, Starting = true, Workspace = circle };
        }
        finally
        {
            placing.Release();
        }
    }

    /// <summary>
    /// How long a start waits for a conversation the person finished to end before it opens the next: its agent's
    /// last turn and its close, well inside the 30 s the page waits on a press.
    /// </summary>
    internal static readonly TimeSpan FinishingBound = TimeSpan.FromSeconds(5);

    /// <summary>The workspace a start's press names, trimmed; null where it names none, which is <c>default</c>'s.</summary>
    private static string? WorkspaceAsked(string? workspace) => string.IsNullOrWhiteSpace(workspace) ? null : workspace.Trim();

    /// <summary>
    /// The workspace as this machine spells it, or refused where it does not hold it, naming those it does:
    /// <c>default</c> and each registered repository's, as Ask Daoris's proposals judge one.
    /// </summary>
    private static string HeldWorkspace(Snapshot snapshot, string circle)
    {
        var held = snapshot.Repositories.Select(known => known.Workspace).Append(RemoteTarget.DefaultWorkspace)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return held.FirstOrDefault(name => string.Equals(name, circle, StringComparison.OrdinalIgnoreCase))
            ?? throw new DriverException(
                $"there is no workspace `{circle}` on this machine — one of {string.Join(", ", held.Select(name => $"`{name}`"))}.");
    }

    /// <summary>
    /// The conversation <c>START_HELP</c> placed, started off the request: the room read through the loop's
    /// <c>DescribeHelp</c> as a conversation going on reads it, then the agent spawned. The words said to it meanwhile go to its
    /// door in order once it opens; where it could not start, its record says why and keeps them as the person's, so the page's
    /// conversation shows both rather than losing either.
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
            var left = _loop.HelpStarting.Over(id, start?.SessionId is null ? null : word => Said(chat, id, word));
            foreach (var word in left)
            {
                _loop.Events.Append(id, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = word.Text });
            }

            // What a word said to it now would do, told again: its own queue now, or its record.
            _loop.Words.Tell(id);
            _loop.Nudge();
        }
    }

    /// <summary>One held word handed to the conversation's door; false where the door would not take it, so the record keeps it.</summary>
    private static bool Said(ChatRunner chat, string id, HeldWord word)
    {
        try
        {
            return chat.Say(id, word.Text, word.Files, word.Preface);
        }
        catch (DriverException)
        {
            return false;
        }
    }

    // Ask Daoris's proposals: what one conversation proposed that waits for the
    // person, each judged with the route's own code first. One the route would refuse is never
    // shown: it is settled refused, and the agent hears why in the route's words. Told by event: its pending list and
    // the service's facts held the window up to 1.3 s, so an ask is answered from what is held and this runs on a task of its own.
    [DriverRoute("HELP_PROPOSALS", PipeClass.Open)]
    [ByEvent("HELP_PROPOSALS", Untold = "each proposal judged against the checkouts' git and the service's records, which no scope tells")]
    private async Task<object?> HelpProposalsAsync(HelpProposalsRead press, CancellationToken cancellationToken)
    {
        var session = press.Session;
        var service = _loop.Service ?? throw NotReady();
        var pending = HelpProposals.Pending(_loop.Home, session);
        var (config, facts) = await _facts.FactsAsync(service, pending, cancellationToken).ConfigureAwait(false);
        var shown = new List<object>();
        foreach (var proposal in pending)
        {
            var plan = HelpProposals.Plan(proposal, config, facts);
            if (plan.Refusal is { } refused)
            {
                HelpProposals.Settle(_loop.Home, proposal.Id, "refused", refused);
                // Daoris's own word, never the person's.
                _loop.Chat?.Tell(session, InPersonsWords($"Daoris did not show proposal `#{proposal.Id}` to the person — the route refuses it: {refused}"),
                    DaorisWordKey("help-refused", proposal.Id));
                continue;
            }

            // A clear's card holds the units it listed, once, so its Apply sends exactly those.
            HelpProposals.Drawn(_loop.Home, proposal, plan);
            shown.Add(Card(proposal, plan).Card);
        }

        return new { Session = session, Proposals = shown.ToArray() };
    }

    /// <summary>
    /// A proposal's card as the page draws it, and the revision of the plan it shows: a digest of everything the card
    /// says, so an Apply carrying it runs only what the person saw, and a machine that moved since asks again. The card's own
    /// <c>revision</c> is that digest.
    /// </summary>
    internal static (string Revision, Dictionary<string, object?> Card) Card(HelpProposal proposal, HelpPlan plan)
    {
        var body = CardBody(proposal, plan);
        var fields = System.Text.Json.JsonSerializer.SerializeToElement(
            body, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var revision = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fields.GetRawText())))[..16].ToLowerInvariant();
        var card = new Dictionary<string, object?>(StringComparer.Ordinal) { ["revision"] = revision };
        foreach (var field in fields.EnumerateObject()) card[field.Name] = field.Value;
        return (revision, card);
    }

    private static object CardBody(HelpProposal proposal, HelpPlan plan) =>
        new
        {
            proposal.Id, proposal.Kind, plan.Describe, plan.Terminal, proposal.Why,
            // What a plugin runs, as its manifest writes it, for the card to show before Apply.
            Plugin = plan.Plugin is { } plugin
                ? new
                {
                    plugin.Id, plugin.Name, plugin.Version, plugin.Command, plugin.Points,
                    Harnesses = plugin.Harnesses.Select(part => new { part.Name, part.Command }).ToArray(),
                    Servers = plugin.Servers.Select(part => new { part.Name, part.Command }).ToArray(),
                    plugin.Copied, plugin.Problem,
                    // An offer's requirement lines, and an update's changes.
                    plugin.Needs, plugin.Replaced,
                    Changes = PluginsRoutes.ChangesOf(plugin.Changes),
                }
                : null,
            // An approval of what a plugin's server offers Ask Daoris, or its withdrawal: every tool, reads and
            // writes apart, and the writes it accepts.
            Grant = plan.Grant is { } grant
                ? new { grant.Plugin, grant.Name, grant.Server, grant.Reads, grant.Writes, grant.Accepted, grant.Withdraw }
                : null,
            Sync = SyncShown(plan.Sync),
            // A workflow's version, as the card shows it, never the steps or the choice Apply carries.
            Workflow = WorkflowShown(plan.Workflow),
            // A place's quick set-up: the ask, its workspace, each change once, and the plan's digest, so a plan that moved
            // draws the card again. Never the plan whole: the page it opens plans again.
            Place = plan.Place is { } place
                ? new
                {
                    place.Asked, place.Workspace, place.Digest,
                    Changes = place.Changes.Select(each => new
                    {
                        each.Change.Key, each.Change.Part, each.Change.Said, each.Change.Before, each.Change.After, each.Repositories,
                    }).ToArray(),
                }
                : null,
        };


    /// <summary>
    /// What a bring-up-to-date card shows, the page's <c>HelpSyncShown</c>: whether the person has looked, every
    /// row the look listed in the terminal's words, and what the rows do not say — each repository not fetched,
    /// with git's reason, when it last heard from origin and how origin is reached, and the repositories left apart.
    /// Null for every other kind.
    /// </summary>
    public static object? SyncShown(HelpSyncPlan? sync) => sync is null ? null : new
    {
        sync.Looked,
        Rows = sync.Rows.Select(row => new { row.Key, row.Step, row.Moves, row.Says }).ToArray(),
        NotFetched = sync.Besides.NotFetched.Select(line => new { line.Repository, line.Fetch, line.LastFetch, line.Reach }).ToArray(),
        Apart = sync.Besides.Apart.ToArray(),
    };

    // The person's Apply: made through the door the screen's own route uses, judged again
    // first, since the machine may have moved since the card was drawn. The result goes back into
    // the conversation as a turn, so the agent knows, told as Daoris's word, never the person's (ASKQUESTS1, D188's COWORK1b
    // mark); what it published is kept with the conversation by `HelpProposals.ApplyAsync`. It acts through the doors directly, not through
    // their routes, so it declares every family a door it holds may move: the driver's file and what reads
    // it, an ask, a quest or a session it made, paused or deleted, an agent's settings or pin, a plugin, the browser's settings,
    // a repository moved, finished history cleared, and the proposal itself.
    [DriverRoute("HELP_APPLY", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("HELP_APPLY", ByPlan = true)]
    [GroundWait("HELP_APPLY", "help", "id", "revision")]
    [Publishes("HELP_APPLY", Applies)]
    private async Task<object?> HelpApplyAsync(HelpApply press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var service = _loop.Service ?? throw NotReady();
        var proposal = HelpProposals.Find(_loop.Home, id)
            ?? throw new DriverException($"there is no proposal `#{id}` on this machine.");
        if (proposal.State != "proposed") throw new DriverException($"proposal `#{id}` is already {proposal.State}.");
        // The card's revision: without one there is no card this Apply stands for.
        var shown = press.Revision;
        if (string.IsNullOrEmpty(shown)) throw new DriverException($"proposal `#{id}` was pressed with no card to stand for; its revision is missing, so it is drawn again.");

        // One Apply at a time: a second window's press of the same card waits for nothing and runs nothing.
        using var claim = HelpProposals.Claim(_loop.Home, id)
            ?? throw new DriverException($"proposal `#{id}` is being applied already, from another window.");
        // The winner may have settled it between the read above and the claim.
        proposal = HelpProposals.Find(_loop.Home, id) ?? proposal;
        if (proposal.State != "proposed") throw new DriverException($"proposal `#{id}` is already {proposal.State}.");
        // the editor's press on a draft card names each change it took into the draft and each it left.
        proposal = proposal with { Taken = press.Taken, Left = press.Left };

        var (config, facts) = await _facts.FactsAsync(service, [proposal], cancellationToken).ConfigureAwait(false);
        var plan = HelpProposals.Plan(proposal, config, facts);
        // The plan is judged again, and a refusal settles as before; but an allowed plan other than the one the card showed is not
        // applied: the person is told what it is now and asks again from the card drawn anew.
        if (plan.Refusal is null && Card(proposal, plan).Revision != shown)
        {
            var changed = $"The plan for `#{id}` changed since this card was drawn, so nothing was applied. It now reads: {plan.Describe}. Look at the new card and press Apply on it.";
            return new { Message = changed, Applied = false, Go = (object?)null, HarnessAction = (object?)null, Stands = true, Changed = true };
        }

        // What it did, and an agent action's end once it comes, into the conversation that proposed it, as Daoris's word.
        var told = 0;
        void Say(string text)
        {
            if (proposal.Session is { } session)
            {
                _loop.Chat?.Tell(session, $"Daoris: {InPersonsWords(text)}", DaorisWordKey("help-apply", $"{proposal.Id}/{Interlocked.Increment(ref told)}"));
            }
        }

        // an Apply marks by the plan it applies, not its route: the target its door declares, its gates
        // taken and marked the owner's before the edit, the edit inside them.
        await using var owned = await OwnerMarks.EnterAsync(
            _loop.Pipe?.StandingConfirmation, _context.OwnerScopesOf(), plan.Refusal is null ? Owned(HelpProposals.Owned(proposal)) : [],
            PipeDoor.Standing, cancellationToken).ConfigureAwait(false);
        var applied = await HelpProposals.ApplyAsync(
            _loop.Home, proposal, plan, new HelpScreenDoors(_context, service), Say, cancellationToken).ConfigureAwait(false);
        // A delete or an ask moves the work; a set-up publishes an ask its session takes at the next look.
        if (proposal.Kind is "ask" or "delete" or "session" or "pause" or "clear") _loop.Nudge();

        return ApplyAnswer(proposal, applied);
    }

    /// <summary>What an Apply's plan acts on (<see cref="HelpProposals.Owned"/>), as the owner's mark names it.</summary>
    public static IReadOnlyList<OwnerNamed> Owned(HelpOwned owned) =>
    [
        .. owned.Repositories.Select(each => new OwnerNamed(OwnerKinds.Repository, each)),
        .. owned.Workspaces.Select(each => new OwnerNamed(OwnerKinds.Workspace, each)),
        .. owned.Quests.Select(each => new OwnerNamed(OwnerKinds.Quest, each)),
    ];

    /// <summary>
    /// What an Apply answers the page, the page's <c>HelpSettled</c>: the driver's sentence, whether it was
    /// applied, where a go takes the person, and the agent action the Agents screen follows. Public so the fast half holds
    /// the answer without a service, as it holds a conversation's options.
    /// </summary>
    public static object ApplyAnswer(HelpProposal proposal, HelpApplied applied) => new
    {
        Message = applied.Told,
        applied.Applied,
        // Where a go takes the person: the page navigates, as its starters' doors do, to the one quest or ask a go
        // names in it, the driver's judged spelling, and opens Add repository or Import a folder with the workspace
        // the driver judged filled, or New conversation with the repository and words it judged, or an Agents
        // place's Setup with its set-up open on the ask; each null on any other go.
        Go = applied.Go is { } place ? new { place.View, place.Domain, place.Part, place.Item, place.Workspace, place.Repository, place.Sentence, place.Setup } : null,
        // The action an update or a pin started, so the Agents screen follows its console and its end. A default
        // is a file edit that starts nothing, so there is nothing to follow.
        HarnessAction = applied.Applied && proposal.Kind == "agent" && proposal.Door is "update" or "pin"
            ? new { Harness = proposal.Target!.Trim(), Action = proposal.Door }
            : null,
        // The card stands for another press: a sync card's look settles nothing, so the page logs no settlement.
        applied.Stands,
    };

    // The person's Not now: nothing changes but the proposal, and the agent is told so. A go's Dismiss takes its card
    // away and is told to nobody: a go changes nothing, so its dismiss is no word on the quest or ask it names.
    [DriverRoute("HELP_DISMISS", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("HELP_DISMISS", None = OwnerNone.ActsOnNothing)]
    [GroundWait("HELP_DISMISS", "help", "id")]
    [Publishes("HELP_DISMISS", StateScopes.Help)]
    private async Task<object?> HelpDismissAsync(HelpDismiss press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var proposal = HelpProposals.Find(_loop.Home, id)
            ?? throw new DriverException($"there is no proposal `#{id}` on this machine.");
        if (proposal.State != "proposed") throw new DriverException($"proposal `#{id}` is already {proposal.State}.");

        var told = HelpProposals.Dismiss(_loop.Home, proposal);
        if (told is not null && proposal.Session is { } said) _loop.Chat?.Say(said, InPersonsWords(told));
        await Task.CompletedTask.ConfigureAwait(false);
        return new { Message = told ?? $"Dismissed `#{id}`: its card left the conversation, nothing was said to Ask Daoris, and nothing else changed." };
    }

    /// <summary>
    /// What a workflow card shows, the page's <c>HelpWorkflowShown</c>: the card's own fields only. The
    /// steps Apply saves and the choice it makes stay with the driver, which sends exactly those when the person presses it.
    /// Null for every other kind.
    /// </summary>
    /// <remarks>FABRIC-PIPELINE1i: the route it chooses for, whether it saves a version, whether it is a verification pipeline, and what it
    /// asks less of the person. FLOWCHART2e: a draft card's fingerprint, its changes with what each needs, what a save would refuse and its
    /// candidate graph whole, which the editor takes changes from and nothing saves; null and empty on every other card.</remarks>
    public static object? WorkflowShown(HelpWorkflowCard? card) => card is null ? null : new
    {
        card.Id, card.Name, card.Base, card.Version, card.Scope,
        card.Diff, card.YourPart, card.Outward, card.Needs, card.AppliesTo, card.Choose,
        card.Route, card.Saves, card.Verifies, card.Lowers, card.NoGates,
        card.Draft,
        Changes = card.Changes.Select(change => new
        {
            change.Id, change.What, change.Change, change.Step, change.Way, change.Was, change.Kind, change.Fields, change.Needs, change.Said,
        }).ToArray(),
        Problems = card.Problems.Select(problem =>
        {
            var (code, way, node, words) = problem;
            return new { Code = code, Node = node, Wire = way, Words = words };
        }).ToArray(),
        card.Proposed,
    };

    /// <summary>
    /// A sentence said into Ask Daoris's conversation: plain words, since a turn's words render verbatim and a sentence full
    /// of backticks read as noise on the window. The person's Not now, and since ASKQUESTS1 Daoris's word of what an Apply did.
    /// </summary>
    private static string InPersonsWords(string told) => told.Replace("`", "", StringComparison.Ordinal);

    /// <summary>
    /// The key a word of Daoris's to Ask Daoris's conversation carries: its kind and the proposal
    /// it is about. No fact of Daoris's is judged by it (<see cref="DaorisKeys.Read"/> names none), so the word always goes.
    /// </summary>
    private static string DaorisWordKey(string kind, string about) => $"{kind}/{about}";

    // what one conversation's Applies published, the newest first, each by the ids the
    // service answered, as `HelpPublished` keeps them beside its other files. The page reads each one's state from the pushed quests,
    // asks and sessions. A read of this machine's home, off the window's thread: it declares nothing, and its key is under
    // `help`, which an Apply publishes. Desktop-only, as the conversation is.
    [DriverRoute("HELP_PUBLISHED", PipeClass.Open)]
    private Task<object?> HelpPublishedAsync(HelpPublishedRead press, CancellationToken cancellationToken)
    {
        var session = press.Session;
        var home = _loop.Home;
        return Task.Run<object?>(() => PublishedAnswer(session, new HelpPublished(home).Read(session)), cancellationToken);
    }

    /// <summary>What <c>HELP_PUBLISHED</c> answers: <c>{ session, published }</c>, an id the service's answer did not name left out.</summary>
    public static object PublishedAnswer(string session, IReadOnlyList<HelpPublishedEntry> published) => new
    {
        Session = session,
        Published = published.Select(entry => new { entry.At, entry.Proposal, entry.Kind, entry.Workspace, entry.Ask, entry.Quest }).ToArray(),
    };

    // ——— Ask Daoris's history: its conversations listed and searched, named, pinned and started from. Going on in
    // one is the person's words to it (`SESSION_INPUT`), and deleting one is `SESSION_DELETE`'s, so each has the one owner the
    // rest of the window uses; `daoris-driver help` is the terminal's door to the same. This machine's and the person's:
    // the words are this machine's record, what the person keeps of each a file beside them (`HelpConversations`), and
    // nothing here reaches a remote.

    // A page of Ask Daoris's conversations, pinned first, then the newest; with `q`, those whose name or words hold it, each with
    // where. `offset` and `limit` are the terminal's `--offset` and `--limit`: every record is ordered and searched
    // before the page is taken, and the answer says how many there are and where the next page starts. `workspace` is the
    // terminal's `--workspace`: that workspace's conversations, searched and counted
    // before the page; none named is every workspace's, as the list was and as `--all` says, each row naming its own.
    [DriverRoute("HELP_CONVERSATIONS", PipeClass.Open)]
    private async Task<object?> HelpConversationsAsync(HelpConversationsRead press, CancellationToken cancellationToken)
    {
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
            press.Workspace);
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
            // the mode it ran under, its record's, after whether it runs; ASKSCOPE1d: the workspace it started in after it.
            row.Session, row.Title, row.Name, row.Opening, row.About, row.Created, row.Last, row.Pinned, row.Live, row.Mode, row.Workspace,
            row.Resumable, row.From, row.Handed, row.Found, row.FoundLine, row.Parts,
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

    // Name an Ask Daoris conversation, or clear its name with none: its first question is its title again. A history act
    // declares the history and the conversations the panel shows, as pinning and starting from one do.
    [DriverRoute("HELP_RENAME", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("HELP_RENAME", Session = "id")]
    [Timeless("HELP_RENAME", "a conversation's name")]
    [Publishes("HELP_RENAME", Kept, Session = "id")]
    private async Task<object?> HelpRenameAsync(HelpRename press, CancellationToken cancellationToken)
    {
        // `help rename <id> "…"` or `--clear`, said in the verb's words, a conversation that is not Ask Daoris's among them.
        if (press.Said)
        {
            var renamed = new HelpVerbAsk("rename") { Id = press.Id, Text = press.Name, Clear = press.Name is null };
            return await SessionsVerbs.HelpSaidAsync(_context, renamed, cancellationToken).ConfigureAwait(false);
        }

        var id = await HelpConversationOfAsync(press.Id, cancellationToken).ConfigureAwait(false);
        var kept = new HelpConversations(_loop.Home);
        kept.Rename(id, press.Name);
        return new { Session = id, kept.Read(id).Name };
    }

    // Pin an Ask Daoris conversation to the top of its history, or unpin it.
    [DriverRoute("HELP_PIN", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("HELP_PIN", Session = "id")]
    [Timeless("HELP_PIN", "a conversation's pin")]
    [Publishes("HELP_PIN", Kept, Session = "id")]
    private async Task<object?> HelpPinAsync(HelpPin press, CancellationToken cancellationToken)
    {
        // `help pin <id>` and `unpin`, said in the verb's words.
        if (press.Said)
        {
            var pin = new HelpVerbAsk(press.Pinned ? "pin" : "unpin") { Id = press.Id };
            return await SessionsVerbs.HelpSaidAsync(_context, pin, cancellationToken).ConfigureAwait(false);
        }

        var id = await HelpConversationOfAsync(press.Id, cancellationToken).ConfigureAwait(false);
        var kept = new HelpConversations(_loop.Home);
        kept.Pin(id, press.Pinned, DateTimeOffset.UtcNow);
        return new { Session = id, kept.Read(id).Pinned };
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
        if (config.HelperAdapter is not { Length: > 0 } helper)
        {
            throw new DriverException(
                "Ask Daoris has no agent to run on — name one under Agents → Roles, Ask Daoris, or "
                + "`daoris role set ask-daoris \"<agent>\"`. Its starters need none.");
        }

        var id = await HelpConversationOfAsync(press.Id, cancellationToken).ConfigureAwait(false);
        var chat = _loop.Chat ?? throw NotReady();
        var service = _loop.Service ?? throw NotReady();

        // in the workspace its press names, as START_HELP's, whatever the earlier one's was; its running one set aside.
        var named = WorkspaceAsked(press.Workspace);
        var circle = RemoteTarget.Workspace(named);
        var spelled = HeldWorkspace(await service.SnapshotAsync(cancellationToken).ConfigureAwait(false), circle);

        // Placed as START_HELP places one: answered once the record is open and the earlier words kept; the room is
        // read through the loop's DescribeHelp and the agent spawned after, off the request, so a slow room cannot pass the page's bound.
        var starting = _loop.HelpStarting;
        var placing = starting.PlacingIn(circle);
        await placing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var place = await chat.PlaceHelpFromAsync(
                id, helper, config, named is null ? null : spelled, cancellationToken, ActorChoices.Effective("ask-daoris", config)).ConfigureAwait(false);
            if (place.SessionId is not { } placedId) return new { SessionId = (string?)null, place.Message, From = id, Workspace = circle };

            starting.Placed(circle, placedId);
            _ = Task.Run(() => RunPlacedAsync(chat, place, config));
            _loop.Nudge();
            return new { SessionId = placedId, place.Message, From = id, Workspace = circle };
        }
        finally
        {
            placing.Release();
        }
    }

    /// <summary>
    /// The id when it names an Ask Daoris conversation of this machine's, judged from the records as the service answers them;
    /// otherwise the sentence why not, which the page says where it was pressed.
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
            return _loop.Harnesses.Adapters.Resolve(adapter).Resumes;
        }
        catch (DriverException)
        {
            return false;
        }
    }
}
