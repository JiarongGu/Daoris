using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The waits that <i>What needs you</i> and the routes answer, each read off a home's files or a part's memory and written as the
/// page's rows read them, oldest first (<see cref="AttentionStateSlice"/> keeps the doors).
/// </summary>
public static class AttentionWaits
{
    /// <summary>
    /// What the agents' asks for a helper wait on the person's word (MIXAGENT2c, D163 point 3), as <c>HELPER_WAITS</c> answers them: each
    /// the driver's look holds <c>waiting</c> with no code, oldest first, with the session that asked. A read off the home's files.
    /// </summary>
    public static IReadOnlyList<object> HelperWaits(string home)
    {
        var files = new HelperFiles(home);
        return
        [
            .. files.Asks()
                .Where(ask => ask.AskedBy == HelperAsk.ByAgent && files.ReadState(ask.Id) is { State: HelperStates.Waiting, Code: null })
                .OrderBy(ask => ask.At)
                .Select(ask => (object)new { ask.Id, Session = ask.Asker, ask.Question, ask.Agent, At = Moments.Write(ask.At) }),
        ];
    }

    /// <summary>
    /// The sessions that waited on their own helper whose move back to the person the service refused (HELPERWAIT1c; the co-worker design
    /// §5 point 4), oldest first, as the driver's look kept them while the refusal stands: each session, its quest, the service's words and
    /// since its first refusal. A read off the home's helpers folder.
    /// </summary>
    public static IReadOnlyList<object> BackWaits(string home) =>
    [
        .. new HelperFiles(home).Backs().Select(back => (object)new { back.Session, back.Quest, back.Words, At = Moments.Write(back.At) }),
    ];

    /// <summary>
    /// What the agents' asks to hand their sessions over wait on the person's word (NOTIFY2e, MIXAGENT1c), oldest first: each the
    /// driver's look holds <c>waiting</c> with no code and the agent it goes to, by the ask's own id, with the session that asked and
    /// its note. One approved, taken or refused waits on nothing of theirs. A read off the home's sessions folder.
    /// </summary>
    public static IReadOnlyList<object> HandOverWaits(string home)
    {
        var files = new HandOverAskFiles(home);
        return
        [
            .. files.Asks()
                .Select(ask => (Ask: ask, State: files.ReadState(ask.Session)))
                .Where(each => each.State is { State: HandOverAskStates.Waiting, Code: null, Agent: { Length: > 0 } } kept && kept.Id == each.Ask.Id)
                .Select(each => (object)new { each.Ask.Id, each.Ask.Session, each.Ask.Note, each.State!.Agent, At = Moments.Write(each.Ask.At) }),
        ];
    }

    /// <summary>
    /// What this machine's driven sessions hold for the person under asking (AUTONOMY1b3a, D184 point 3), as <c>PERMISSION_WAITS</c>
    /// answers them (the route answers this list), oldest first, from the driver's memory: each with when its bound releases it
    /// (<c>until</c>, PERMUNTIL1), the page's one source for the bound.
    /// </summary>
    public static IReadOnlyList<object> PermissionWaits(HeldPermissions holds) =>
    [
        .. holds.Now().Select(held => (object)new
        {
            held.Session, held.Request, held.Call, held.Title, held.Kind,
            Choices = held.Choices.Select(choice => new { choice.Id, choice.Name, choice.Kind }),
            Since = Moments.Write(held.Since),
            Until = Moments.Write(held.Until),
        }),
    ];

    /// <summary>
    /// What a terminal sent that waits for the window's Confirm (CONFIRMSEEN1), oldest first, from the pipe's memory: each by its id,
    /// route and terminal, with its times written as every other wait's are, so the band orders them among the rest. Never a value it
    /// carries. None where this shell serves no pipe.
    /// </summary>
    public static IReadOnlyList<object> ConfirmationWaits(PipeConfirmations? confirmations) =>
    [
        .. (confirmations?.Now().Waiting ?? []).Select(waiting => (object)new
        {
            waiting.Id, waiting.Module, waiting.Route,
            Asker = new { waiting.Asker.Process, waiting.Asker.Pid },
            Asked = Moments.Write(waiting.Asked),
            Expires = Moments.Write(waiting.Expires),
            ShownAt = waiting.ShownAt is { } shown ? Moments.Write(shown) : null,
        }),
    ];

    /// <summary>
    /// Each volume still below its free-space floor once the floor's acts ran (KEEP1f; the cleanup design §3.2), as the look's pass said
    /// it, oldest first: by the volume's identity, since it was said, its root, free space, size and floor as read then, what the floor
    /// freed in this stretch and what the person's press could free (null where none was read). A volume above its floor by the margin
    /// holds no stretch, so none is said. From the process's memory.
    /// </summary>
    public static IReadOnlyList<object> SpaceWaits(SpaceFloors floors, string home) =>
    [
        .. floors.Of(home).Values
            .Where(each => each is { Said: not null, Read: not null })
            .OrderBy(each => each.Said).ThenBy(each => each.Volume, StringComparer.Ordinal)
            .Select(each => (object)new
            {
                each.Volume,
                Said = Moments.Write(each.Said!.Value),
                each.Read!.Read.Root, each.Read.Read.Free, each.Read.Read.Total, each.Read.Floor,
                each.Freed,
                each.Could,
            }),
    ];

    /// <summary>
    /// The answers the answers-alone bound holds for the person (COWORK1e, D188 point 4; the message-urgency design §8.3), as
    /// <c>ANSWER_WAITS</c> answers them, oldest first: each session's mark <see cref="GoOnMarks.AnswersAlone"/> with the agent that
    /// answered, the answers' ids and since when. A read off the home's marks; one the person kept with *Not now*, and a mark of any
    /// other reason, waits on nothing of theirs.
    /// </summary>
    public static IReadOnlyList<object> AnswerWaits(string home) =>
    [
        .. new GoOnMarks(home).All()
            .Where(each => each.Mark.Why == GoOnMarks.AnswersAlone)
            .OrderBy(each => each.Mark.At).ThenBy(each => each.Session, StringComparer.Ordinal)
            .Select(each => (object)new { each.Session, each.Mark.Agent, Words = each.Mark.Said, Since = Moments.Write(each.Mark.At) }),
    ];

    /// <summary>
    /// The sessions the watcher brings to the person (SESSWATCH1a, D200, the session-watch design §2.3, §5.2), oldest heard first, from its
    /// memory: each with its kind and quest, the minutes nothing was heard from it, whether something it runs keeps the machine busy (a row
    /// offered no end), when it was last heard, and whether the person's words wait to reach it, which ending it hands to the next part.
    /// </summary>
    public static IReadOnlyList<object> QuietWaits(SessionWatch watch, SessionProcesses processes) =>
    [
        .. watch.Rows().Select(row => (object)new
        {
            row.Session, row.Kind, row.Quest, row.Minutes, row.Busy, row.Heavy,
            Since = Moments.Write(row.Since),
            Words = processes.InboxOf(row.Session)?.State.Queued.Any(word => word.Persons) == true,
        }),
    ];

    /// <summary>
    /// Each remote the home holds since it could not authenticate (GITAUTH1a, the git-auth design §5.2), oldest first: its repository,
    /// workspace, remote, host, class, what it holds, what the transport said (redacted when it was kept) and since when; its way, and what
    /// serves it now as the doors resolve it (GITAUTH1e, §7.4: the identity a refusal names, or <c>own</c>). No machine path: the checkout
    /// it is keyed by stays in the file.
    /// </summary>
    public static IReadOnlyList<object> GitWaits(string home)
    {
        var holds = GitHolds.Read(home);
        if (holds.Count == 0) return [];
        var identities = GitIdentities.Read(home);
        var pointers = GitIdentityUse.Read(home);
        return
        [
            .. holds
                .OrderBy(each => each.Since).ThenBy(each => each.Repository, StringComparer.Ordinal)
                .Select(each =>
                {
                    var way = each.Address.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? GitIdentity.Https : GitIdentity.Ssh;
                    var workspace = each.Workspace ?? RemoteTarget.DefaultWorkspace;
                    var serves = each.Host is { Length: > 0 } host
                        ? GitIdentityUse.Resolve(identities, pointers, each.Repository, workspace, [each.Remote], GitHosts.Canonical(host), way)
                        : GitServes.Nothing([]);
                    return (object)new
                    {
                        each.Repository, Workspace = workspace, each.Remote, each.Host, each.Class, each.Holds, each.Words,
                        Since = Moments.Write(each.Since),
                        Way = way,
                        Served = serves.Kind switch { GitServeKind.Identity => "identity", GitServeKind.Own => GitIdentityUse.Own, _ => "none" },
                        Identity = serves.Identity?.Label,
                        IdentityId = serves.Identity?.Id,
                        KeyPage = serves.Identity is { Way: GitIdentity.Ssh } ssh && GitHosts.Known.TryGetValue(ssh.Host, out var known) ? known.KeyPage : null,
                    };
                }),
        ];
    }

    /// <summary>
    /// The integration's waits on the owner (INTEGRATE1h; the integration design §5.8, §17.4), oldest first by kind: each standing candidate
    /// whose judge the policy held for a decide no one has recorded yet, with its members' branches and the judge's words; each superseded
    /// commit no one has answered; each landing the runtime last found waiting for the owner's card; and each repository whose committed
    /// declaration the look found waiting for the owner's acceptance (§5.8), <c>unaccepted</c> or <c>changed</c>. A read of the home's
    /// integration record, which a store from before the family answers with none.
    /// </summary>
    public static IReadOnlyList<object> IntegrationWaits(string home, IReadOnlyList<IntegrationCardWait> cards, IReadOnlyList<DeclarationOwed>? declarations = null)
    {
        var store = new IntegrationStore(home);
        var waits = new List<object>();
        foreach (var candidate in store.Standing().Where(each => each.PromotedAt is null))
        {
            if (store.Steps(candidate.Id).LastOrDefault() is not { Step: IntegrationStepNames.Judge, Outcome: IntegrationStepOutcomes.Held } held) continue;
            if (store.DecideOf(candidate.Id) is not null) continue;
            var branches = store.Members(candidate.Id).Where(member => member.Stands).Select(member => store.Request(member.Request)?.Branch).OfType<string>();
            waits.Add(new
            {
                Wait = "decide", Id = $"decide:{candidate.Id}", candidate.Repository, candidate.Workspace, candidate.Line, Candidate = candidate.Id,
                Branches = branches.ToArray(), Why = held.Said, Since = Moments.Write(held.EndedAt),
            });
        }

        foreach (var pending in store.Pending())
        {
            if (store.Request(pending.Request) is not { } request) continue;
            waits.Add(new
            {
                Wait = "superseded", Id = $"superseded:{pending.Request}:{pending.Commit}", request.Repository, request.Workspace, request.Line, request.Branch,
                Request = pending.Request, pending.Commit, pending.Subject, Since = Moments.Write(pending.At),
            });
        }

        foreach (var card in cards)
        {
            waits.Add(new
            {
                Wait = "card", Id = $"card:{card.Unit}", card.Repository, card.Workspace, card.Branch, card.Unit, card.Tip, Why = card.Said,
                Since = Moments.Write(card.Since),
            });
        }

        foreach (var owed in declarations ?? [])
        {
            waits.Add(new
            {
                Wait = "declaration", Id = $"declaration:{owed.Repository}", owed.Repository, owed.Workspace, owed.Line, Tip = owed.Blob, State = owed.State,
                Since = Moments.Write(owed.Since),
            });
        }

        return waits;
    }

    /// <summary>
    /// Each git go-ahead a door holds (GITAUTH1f, the git-auth design §6.6), oldest first: its id, repository, workspace, remote, host,
    /// class, session, since when and whether its door has gone. Never its address's key, which only the door and a grant compare.
    /// </summary>
    public static IReadOnlyList<object> GitAskWaits(string home) =>
    [
        .. GitAsks.List(home, DateTimeOffset.UtcNow).Select(each => (object)new
        {
            each.Id, each.Repository, each.Workspace, each.Remote, each.Host, each.Class, each.Session, At = Moments.Write(each.At), each.Gone,
        }),
    ];
}
