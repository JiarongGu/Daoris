namespace Daoris.Knowledge;

/// <summary>Why a publish did not produce a quest — or <see cref="None"/> when it did.</summary>
public enum QuestPublishRefusal
{
    None,

    /// <summary>Addressed to the repository doing the asking. Its own backlog is the place for that.</summary>
    SelfAddressed,

    /// <summary>The target has not adopted, so it has no client to see the quest (D32, D34).</summary>
    NotAddressable,

    /// <summary>
    /// The two repositories are in different workspaces, and the workspace is the unit of sharing
    /// (D48 §4). Cross-workspace asking is deliberately out of scope — a person carries it, or a
    /// later, explicit door does, with its own disclosure argument (design §10).
    /// </summary>
    CrossWorkspace,

    /// <summary>A link that is not an absolute http or https address — shown as a link, it would be something else.</summary>
    BadLink,

    /// <summary>More files, or more bytes, than a quest carries — or a file named in a shape no record keeps.</summary>
    BadAttachment,

    /// <summary>Files arrived with their bytes and this host has no Daoris home to keep them under (D63).</summary>
    NoHome,

    /// <summary>
    /// A chain (D65 §4) with a step that cannot be published when its turn comes: nobody there can see
    /// it, it asks the asker, it has no words, it is shared where the chain is local or local where it
    /// is shared, or there are too many.
    /// </summary>
    BadChain,
}

/// <summary>
/// What a publish is asked for: who asks, of whom, what — and what the quest carries beside its words
/// (D65 §2).
/// </summary>
/// <remarks>
/// A file arrives one of two ways and the type says which. <see cref="Uploads"/> come WITH their
/// bytes, from a door on the machine that has them, and are kept under this machine's home.
/// <see cref="Named"/> come by name only, from a machine that keeps the bytes itself and is relaying
/// to the quest's home — the only shape a shared deployment ever takes, because it keeps names and
/// never bytes. A relay has no field for bytes at all: absent, not policed (D47 §4).
/// </remarks>
public sealed record QuestAsk(string From, string To, string Title, string Body)
{
    public IReadOnlyList<string> Links { get; init; } = [];

    public IReadOnlyList<QuestUpload> Uploads { get; init; } = [];

    public IReadOnlyList<QuestAttachment> Named { get; init; } = [];

    /// <summary>What to ask next when this closes done (D65 §4) — judged here, when the chain is composed.</summary>
    public IReadOnlyList<QuestStep> Then { get; init; } = [];

    /// <summary>
    /// The circle a sender that is NOT a repository asks from — an ask (D65 §1a), which has no registry
    /// row to say it. Ignored for a registered sender, whose row is the machine's wiring and decides.
    /// </summary>
    public string? Workspace { get; init; }
}

/// <param name="Refusal"><see cref="QuestPublishRefusal.None"/> when the quest was published.</param>
/// <param name="Message">The full answer, phrased once here so no two hosts can drift apart on it.</param>
/// <param name="Quest">The published quest, when there is one.</param>
/// <param name="Addressable">Who can be asked — the actionable half of a refusal.</param>
public sealed record QuestPublishOutcome(
    QuestPublishRefusal Refusal,
    string Message,
    Quest? Quest,
    IReadOnlyList<string> Addressable);

/// <summary>Why a response did not move a quest — or <see cref="None"/> when it did.</summary>
public enum QuestRespondRefusal
{
    None,

    /// <summary>Not take, done, or decline.</summary>
    UnknownAction,

    /// <summary>Declining needs a reason: it is the part the asker can act on.</summary>
    MissingReason,

    /// <summary>No quest under that id.</summary>
    NotFound,

    /// <summary>Somebody got there first — the losing side of the race stands down (D47 §5).</summary>
    AlreadyTaken,

    /// <summary>Done and Declined are terminal: one title is one quest forever (D46 §3).</summary>
    Closed,
}

/// <param name="Refusal"><see cref="QuestRespondRefusal.None"/> when the status moved.</param>
/// <param name="Message">The full answer, phrased once here.</param>
/// <param name="Quest">The quest as it now stands, when the status moved.</param>
public sealed record QuestRespondOutcome(QuestRespondRefusal Refusal, string Message, Quest? Quest);

/// <summary>
/// The judgement half of the quest system: who may be addressed, what a refusal says, what a response
/// requires. The stores hold state; this decides.
/// </summary>
/// <remarks>
/// <para>It exists because there are two doors — MCP for an agent beside the repositories, HTTP for a
/// remote deployment — and the rules were about to be written twice. Two copies would drift, and the
/// same ask would then be deliverable through one door and refused at the other, which for a quest
/// system is the worst available bug: it looks like the sibling ignoring you.</para>
///
/// <para>The messages are composed here too, not only the verdicts. They are what an agent acts on, so
/// two hosts phrasing them differently is two behaviours in all the ways that matter.</para>
///
/// <para><b>Every verb commits here</b> (D68): a quest shared with a team is published, taken and
/// closed on this machine like any other, and reaches the remote on the next sync. Nothing fails
/// because a remote is down. One verb WAITS on it when it answers: a take on a shared quest claims by
/// push (D69), so a take that lost is known before any work starts.</para>
/// </remarks>
/// <param name="remotes">
/// This machine's remotes, by workspace — what a take claims at, and what a chain's composition asks,
/// because a chain's steps must all be shared or all be local. Null on a machine with none, and on a
/// remote itself.
/// </param>
public sealed class QuestExchange(
    KnowledgeService service, QuestStore quests, IQuestRemotes? remotes = null, QuestFiles? files = null)
{
    /// <summary>How many links a quest carries — a ticket and its neighbours, not a bibliography.</summary>
    public const int MaxLinks = 20;

    /// <summary>How long one link may be.</summary>
    public const int MaxLinkLength = 2048;

    /// <summary>How many files a quest carries.</summary>
    public const int MaxAttachments = 10;

    /// <summary>
    /// How many bytes a quest's files come to TOGETHER — screenshots, a log, a document. Anything
    /// larger has a home of its own already, and a link to it is the better thing to carry.
    /// </summary>
    public const long MaxAttachmentBytes = 20L * 1024 * 1024;

    /// <summary>
    /// How many steps a chain carries after its first quest — develop, verify, report is two. A longer
    /// chain is a plan, and a plan belongs to whoever composes it, step by step.
    /// </summary>
    public const int MaxChain = 5;

    /// <summary>A quest that carries nothing but its words — every caller before D65.</summary>
    public Task<QuestPublishOutcome> PublishAsync(
        string from, string to, string title, string body, DateTimeOffset now, CancellationToken ct = default) =>
        PublishAsync(new QuestAsk(from, to, title, body), now, ct);

    /// <summary>
    /// Publish a quest to another repository. Refuses a self-addressed quest and a target that has not
    /// adopted; warns when the target has declared nothing about itself (D34). It is published HERE,
    /// whoever the receiver is (D68), and a joined receiver's quest reaches the remote on the next sync.
    /// What it carries is judged before anything is written anywhere, and its files are kept HERE —
    /// under this machine's home — wherever the record travels (D65 §2).
    /// </summary>
    public async Task<QuestPublishOutcome> PublishAsync(QuestAsk ask, DateTimeOffset now, CancellationToken ct = default)
    {
        var (from, to, title, body) = (ask.From, ask.To, ask.Title, ask.Body);

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return new(
                QuestPublishRefusal.SelfAddressed,
                "That is the repository you are in — a quest is work for someone else. Use its own backlog.",
                Quest: null, Addressable: []);
        }

        // Unscoped deliberately: the clause below needs BOTH sides' workspaces, and a registry already
        // narrowed to one would answer the cross-workspace case as "no such repository" — a refusal
        // that sends the asker looking for a name they can see perfectly well.
        var registered = await service.RegistryAsync(ct: ct).ConfigureAwait(false);
        var sender = registered.FirstOrDefault(r =>
            string.Equals(r.Repository, from, StringComparison.OrdinalIgnoreCase));
        // A registered sender's row decides its circle. A sender with none — an ask (D65 §1a) — names
        // its own, and silence places it where an unregistered sender always was.
        var home = sender?.InWorkspace ?? Workspaces.Normalize(ask.Workspace);

        // Who can be asked is who shares the asker's circle. A refusal that listed the whole machine
        // would be offering repositories this one may not address.
        var addressable = registered
            .Where(r => r.Adopted && Workspaces.Same(r.InWorkspace, home))
            .Select(r => r.Repository)
            .ToList();
        var target = registered.FirstOrDefault(r =>
            string.Equals(r.Repository, to, StringComparison.OrdinalIgnoreCase));

        if (target is null || !target.Adopted)
        {
            // A quest for a repository with no client has nobody to read it, so it would sit in a queue
            // that is never opened. Saying so now beats letting it look delivered.
            return new(
                QuestPublishRefusal.NotAddressable,
                $"`{to}` has not adopted Daoris, so it has no way to see a quest. Addressable: "
                + $"{string.Join(", ", addressable)}.",
                Quest: null, addressable);
        }

        if (!Workspaces.Same(target.InWorkspace, home))
        {
            // The workspace is the unit of sharing (D48), so this is not a permission failure — it is
            // two circles that were never joined. The sentence names BOTH sides, because "no" that
            // does not say which side is where leaves the asker with the question it should settle:
            // the answer is either to re-wire one of them, or to carry the request by hand.
            return new(
                QuestPublishRefusal.CrossWorkspace,
                $"`{from}` is in workspace `{home}` and `{target.Repository}` is in `{target.InWorkspace}` — "
                + "a quest does not cross workspaces, because the workspace is the unit of sharing. "
                + $"Either wire one of them to the other's workspace on this machine, or carry the request "
                + $"across yourself. Addressable from `{home}`: {string.Join(", ", addressable)}.",
                Quest: null, addressable);
        }

        // What it carries is judged here, before any door writes anything: a refused ask must leave
        // nothing behind — not a record, and not a file no record names.
        var carried = Judge(ask);
        if (carried.Refusal is { } unfit)
        {
            return new(unfit, carried.Message, Quest: null, addressable);
        }

        // A chain is judged now, while the person or the intake composing it can still act on the
        // answer — not at a close nobody is watching (D65 §4). Whether this circle shares with a team
        // is this machine's wiring; whether a receiver is shared is its registration (design §8).
        var circleWired = remotes?.For(home) is not null;
        if (JudgeChain(ask, registered, home, circleWired, circleWired && target.Joined, addressable) is { } unfitChain)
        {
            return new(QuestPublishRefusal.BadChain, unfitChain, Quest: null, addressable);
        }

        var quest = await quests.PublishAsync(
            from, to, title, body, now, home, carried.Links, carried.Attachments, ask.Then, ct: ct).ConfigureAwait(false);

        var caution = target.Registered
            ? ""
            : $"\n\n⚠ `{target.Repository}` has not declared what it owns or accepts, so this may not be "
              + "its problem. Worth checking before you rely on it.";

        return new(
            QuestPublishRefusal.None,
            $"Published quest `#{quest.Id}` to `{quest.To}` — {quest.Status}.{caution}\n\n"
            + "It is held by the service, not written into that repository. Its agent will see it and "
            + "decide. Do not make the change yourself."
            + await KeepAsync(quest, ask, carried, ct).ConfigureAwait(false),
            quest, addressable);
    }

    /// <summary>
    /// Every step of a chain must be publishable when its turn comes — so each is judged as its own
    /// publish would be, asked on behalf of the chain's asker, and each must be shared exactly when the
    /// quest it follows is. Null when the chain is fit (or there is none).
    /// </summary>
    /// <remarks>
    /// <b>All shared or all local</b> (D65 §4, D68): a step is published by the close of the one before
    /// it, on whichever machine closed it. A shared quest may be closed on a teammate's machine, which
    /// cannot see a repository local to this one; and a local quest's step to a shared receiver would
    /// publish only here, where no teammate's driver can see it close. A chain that straddles the two is
    /// refused when composed rather than stranded later.
    /// </remarks>
    private static string? JudgeChain(
        QuestAsk ask, IReadOnlyList<Registration> registered, string home, bool circleWired, bool shared,
        IReadOnlyList<string> addressable)
    {
        if (ask.Then.Count > MaxChain)
        {
            return $"A chain carries at most {MaxChain} steps after its first quest — this one was given "
                   + $"{ask.Then.Count}. Publish the rest when the work gets there.";
        }

        foreach (var (step, index) in ask.Then.Select((step, index) => (step, index + 1)))
        {
            if (string.IsNullOrWhiteSpace(step.To) || string.IsNullOrWhiteSpace(step.Title) || string.IsNullOrWhiteSpace(step.Body))
            {
                return $"Step {index} of the chain needs whom to ask, a title and a body — the same three words any quest does.";
            }

            if (string.Equals(step.To, ask.From, StringComparison.OrdinalIgnoreCase))
            {
                return $"Step {index} asks `{step.To}`, which is the chain's own asker — every step is asked on its "
                       + "behalf, so that would be a quest to itself. Its own backlog is the place for that.";
            }

            var receiver = registered.FirstOrDefault(r =>
                string.Equals(r.Repository, step.To, StringComparison.OrdinalIgnoreCase));
            if (receiver is null || !receiver.Adopted || !Workspaces.Same(receiver.InWorkspace, home))
            {
                return $"Step {index} asks `{step.To}`, which cannot be asked from `{ask.From}` — nothing would see it "
                       + $"when its turn came. Addressable: {string.Join(", ", addressable)}.";
            }

            var stepShared = circleWired && receiver.Joined;
            if (stepShared != shared)
            {
                return $"Step {index} asks `{step.To}`, which is {(stepShared ? "shared with the team" : "local to this machine")}, "
                       + $"and the chain starts at `{ask.To}`, which is {(shared ? "shared with the team" : "local to this machine")}. "
                       + "Each step is published on the machine that closes the one before it, so a chain is all "
                       + "shared or all local. Publish the other half as its own quest when this one is done.";
            }
        }

        return null;
    }

    /// <summary>
    /// A take on a shared quest claims by push (D69): committed here a moment ago, it is pushed now and
    /// its answer awaited, so a take that lost is known before any work starts. Null for a quest this
    /// machine does not share — its take is the local one, and the answer the ordinary one.
    /// </summary>
    /// <returns>
    /// Taken when the remote numbered it. <see cref="QuestRespondRefusal.AlreadyTaken"/> when another
    /// machine's take reached it first — the take is a conflict on the quest now, and the session stands
    /// down on the same sentence it always has. Taken, UNCONFIRMED, when the remote did not answer or did
    /// not take it: offline work is allowed (D68 §4), and the next sync decides.
    /// </returns>
    private async Task<QuestRespondOutcome?> ClaimByPushAsync(Quest quest, CancellationToken ct)
    {
        if (remotes?.For(quest.Workspace) is not { } remote) return null;

        // Only a quest whose receiver is joined in its circle ever leaves this machine (design §8); a
        // take on any other is complete the moment it commits.
        var registered = await service.RegistryAsync(ct: ct).ConfigureAwait(false);
        if (!registered.Any(r => r.Joined && Workspaces.Same(r.InWorkspace, quest.Workspace)
                                 && string.Equals(r.Repository, quest.To, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var pass = await QuestSync.RunAsync(quests, service, remote, quest.Workspace, ct).ConfigureAwait(false);
        return await quests.ClaimAsync(quest.Id, ct).ConfigureAwait(false) switch
        {
            QuestClaim.Held => new(
                QuestRespondRefusal.None,
                $"Quest `#{quest.Id}` is now Taken — the remote confirmed the claim.",
                await quests.FindAsync(quest.Id, ct).ConfigureAwait(false)),
            QuestClaim.Lost => new(
                QuestRespondRefusal.AlreadyTaken,
                $"Quest `#{quest.Id}` was taken on another machine first — its take reached the remote before this "
                + "one, which is kept on the quest as a conflict. Stand down rather than doubling the work.",
                Quest: null),
            _ => new(
                QuestRespondRefusal.None,
                $"Quest `#{quest.Id}` is now Taken, UNCONFIRMED: "
                + (pass.Problem ?? pass.Refused.FirstOrDefault(r => r.Quest == quest.Id)?.Reason
                    ?? "the remote has not taken the claim yet")
                + ". The take stands on this machine until a sync reaches the remote. Carry on — and if "
                + "another machine's take reached it first, this session will be stopped.",
                await quests.FindAsync(quest.Id, ct).ConfigureAwait(false)),
        };
    }

    /// <summary>
    /// A remote's say over a publish a machine pushed (design §8) — the judgement the machine's own
    /// exchange already ran, run again where it lands, because a remote takes nobody's word for it.
    /// Null when the quest is fit to keep here.
    /// </summary>
    /// <remarks>
    /// The receiver must be registered here, since a quest nobody here can see has nobody to answer it.
    /// What it carries must be what a record may name: links, and files by name. There are no bytes to
    /// judge, because a pushed operation has no field for them.
    /// </remarks>
    public string? JudgeReceived(Quest asked, IReadOnlyList<Registration> registered)
    {
        if (!registered.Any(r => r.Adopted && string.Equals(r.Repository, asked.To, StringComparison.OrdinalIgnoreCase)))
        {
            return $"`{asked.To}` is not registered at this deployment, so nobody here could answer quest "
                   + $"`#{asked.Id}`. Its registration travels first; the next sync tries again.";
        }

        var carried = Judge(new QuestAsk(asked.From, asked.To, asked.Title, asked.Body)
        {
            Links = asked.Links,
            Named = asked.Attachments,
        });
        if (carried.Refusal is not null) return carried.Message;

        return carried.Links.Count == asked.Links.Count && carried.Attachments.Count == asked.Attachments.Count
            ? null
            : $"Quest `#{asked.Id}` carries a link or a file twice, which a publish here would not have kept.";
    }

    /// <summary>
    /// The same judgement over what an ASK carries (D65 §1a) — an ask keeps its links and files until
    /// it becomes quests, and a limit it did not share with them would be refused only later, at a
    /// publish nobody is watching.
    /// </summary>
    public (QuestPublishRefusal? Refusal, string Message, IReadOnlyList<string> Links) JudgeCarry(
        IReadOnlyList<string> links, IReadOnlyList<QuestUpload> uploads)
    {
        var carried = Judge(new QuestAsk("", "", "", "") { Links = links, Uploads = uploads });
        return (carried.Refusal, carried.Message, carried.Links);
    }

    /// <summary>What a publish carries once judged: the links and files a record may name, or why not.</summary>
    private sealed record Carried(
        QuestPublishRefusal? Refusal, string Message,
        IReadOnlyList<string> Links, IReadOnlyList<QuestAttachment> Attachments);

    /// <summary>
    /// The judgement over what a quest carries — one place, because an HTTP door, the MCP door and a
    /// relay from another machine all arrive here, and a limit two of them enforced would be a limit
    /// the third quietly did not.
    /// </summary>
    private Carried Judge(QuestAsk ask)
    {
        var links = new List<string>();
        foreach (var given in ask.Links)
        {
            var link = given.Trim();
            if (link.Length == 0 || links.Contains(link, StringComparer.Ordinal)) continue;

            // Shown as a link, so it must BE an address: a `javascript:` "link" in a drawer is a
            // script, and a `file:` one names somebody's disk.
            if (link.Length > MaxLinkLength
                || !Uri.TryCreate(link, UriKind.Absolute, out var address)
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            {
                return Refuse(
                    QuestPublishRefusal.BadLink,
                    $"`{Clip(link)}` is not a link a quest can carry — a link is an absolute http or https "
                    + $"address of at most {MaxLinkLength} characters. Put anything else in the body.");
            }

            links.Add(link);
        }

        if (links.Count > MaxLinks)
        {
            return Refuse(
                QuestPublishRefusal.BadLink,
                $"A quest carries at most {MaxLinks} links — this one was given {links.Count}.");
        }

        if (ask.Uploads.Count > 0 && files is null)
        {
            // No home, no files (D63): writing them somewhere nobody pointed this host is the thing
            // that was removed. A shared deployment never reaches here — its door takes names only.
            return Refuse(QuestPublishRefusal.NoHome, DaorisHome.Sentence);
        }

        var attachments = new List<QuestAttachment>();
        foreach (var named in ask.Named)
        {
            // A name that arrives already described was made safe by the machine that kept the file;
            // one that is not safe was never kept by Daoris, and a record naming it would lie.
            if (named.Name != QuestFiles.SafeName(named.Name)
                || named.Sha256.Length != 64 || !named.Sha256.All(Uri.IsHexDigit) || named.Sha256 != named.Sha256.ToLowerInvariant()
                || named.Bytes < 0)
            {
                return Refuse(
                    QuestPublishRefusal.BadAttachment,
                    $"`{Clip(named.Name)}` is not a file a record can name — an attachment by name carries a "
                    + "safe file name, its lowercase sha256 and its size.");
            }

            attachments.Add(named);
        }

        attachments.AddRange(ask.Uploads.Select(QuestFiles.Describe));

        // The same content twice is one file — its hash is its identity; the first name given wins.
        var distinct = attachments
            .GroupBy(a => a.Sha256, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        if (distinct.Count > MaxAttachments)
        {
            return Refuse(
                QuestPublishRefusal.BadAttachment,
                $"A quest carries at most {MaxAttachments} files — this one was given {distinct.Count}.");
        }

        var total = distinct.Sum(a => a.Bytes);
        if (total > MaxAttachmentBytes)
        {
            return Refuse(
                QuestPublishRefusal.BadAttachment,
                $"A quest's files come to at most {Megabytes(MaxAttachmentBytes)} together — these come to "
                + $"{Megabytes(total)}. Carry a link to the large ones instead.");
        }

        return new(null, "", links, distinct);

        static Carried Refuse(QuestPublishRefusal refusal, string message) => new(refusal, message, [], []);
    }

    /// <summary>
    /// Keep the bytes the record names, under THIS machine's home — after the record exists, so a
    /// refused publish kept nothing. Answers what the asker should hear about it, or nothing.
    /// </summary>
    /// <remarks>
    /// Only bytes the record actually names are kept: a quest published before, under the same title,
    /// is answered as it stands (D46 §3), and what this call carried beyond it is SAID rather than
    /// quietly dropped. A file the disk refuses leaves the quest published and says which — the
    /// record's name for it is honest either way, because every reader asks whether it is here.
    /// </remarks>
    private async Task<string> KeepAsync(Quest quest, QuestAsk ask, Carried carried, CancellationToken ct)
    {
        var onRecord = quest.Attachments.Select(a => a.Sha256).ToHashSet(StringComparer.Ordinal);
        var notKept = new List<string>();
        foreach (var upload in ask.Uploads)
        {
            if (!onRecord.Contains(QuestFiles.Describe(upload).Sha256)) continue;
            try
            {
                await files!.KeepAsync(quest.Id, upload, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                notKept.Add($"`{QuestFiles.SafeName(upload.Name)}` ({error.Message})");
            }
        }

        var said = "";
        var unadded = carried.Attachments.Where(a => !onRecord.Contains(a.Sha256)).Select(a => $"`{a.Name}`")
            .Concat(carried.Links.Where(l => !quest.Links.Contains(l, StringComparer.Ordinal)).Select(l => $"<{l}>"))
            .ToList();
        if (unadded.Count > 0)
        {
            said += $"\n\n⚠ `#{quest.Id}` was already published, and a quest carries what it was first published "
                    + $"with — not added: {string.Join(", ", unadded)}. A new ask is a new title.";
        }

        if (notKept.Count > 0)
        {
            said += $"\n\n⚠ Published, but this machine could not keep: {string.Join(", ", notKept)}. The record "
                    + "names them; a session will be told they are not here.";
        }

        return said;
    }

    private static string Megabytes(long bytes) => $"{bytes / (1024.0 * 1024.0):0.#} MB";

    private static string Clip(string text) => text.Length <= 120 ? text : text[..120] + "…";

    /// <summary>
    /// Answer a quest: take, done, or decline. Declining without a reason is refused — a bare refusal
    /// gives the asker nothing to act on.
    /// </summary>
    public async Task<QuestRespondOutcome> RespondAsync(
        string id, string action, string? reason, DateTimeOffset now, CancellationToken ct = default)
    {
        var status = action.ToLowerInvariant() switch
        {
            "take" => QuestStatus.Taken,
            "done" => QuestStatus.Done,
            "decline" => QuestStatus.Declined,
            _ => (QuestStatus?)null,
        };

        if (status is null)
        {
            return new(
                QuestRespondRefusal.UnknownAction,
                $"Unknown action '{action}' — one of: take, done, decline.",
                Quest: null);
        }

        if (status == QuestStatus.Declined && string.IsNullOrWhiteSpace(reason))
        {
            return new(
                QuestRespondRefusal.MissingReason,
                "Declining needs a reason: it is the part the asker can act on.",
                Quest: null);
        }

        // Every verb commits here, shared or not (D68): the next sync carries it to the remote, where the
        // first push wins and a later one is kept as a conflict rather than lost (design §5).
        var move = await quests.MoveAsync(id.TrimStart('#'), status.Value, reason, now, ct)
            .ConfigureAwait(false);

        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{id.TrimStart('#')}`. Ids come from `quest_list`.", Quest: null);
        }

        if (move.Moved && status == QuestStatus.Taken
            && await ClaimByPushAsync(move.Quest, ct).ConfigureAwait(false) is { } claimed)
        {
            return claimed;
        }

        if (move.Moved)
        {
            // The chain moved on with the close (D65 §4) — said here, because the next step is now
            // somebody's open quest and whoever closed this one should know whose.
            var then = move.FollowUp is { } next
                ? $"\n\nThen: published `#{next.Id}` to `{next.To}` on behalf of `{next.From}` — {next.Status}."
                  + (next.Then.Count > 0 ? $" {next.Then.Count} more step(s) follow it." : "")
                : "";
            return new(QuestRespondRefusal.None, $"Quest `#{move.Quest.Id}` is now {move.Quest.Status}.{then}", move.Quest);
        }

        // The store refused the transition; the quest comes back unchanged so the answer can name the
        // state that refused it. For a take that is the race resolving (D47 §5) — the same message a
        // session racing an outside session already acts on.
        return move.Quest.Status == QuestStatus.Taken
            ? new(
                QuestRespondRefusal.AlreadyTaken,
                $"Quest `#{move.Quest.Id}` is already taken — someone is working it. Stand down rather than doubling the work.",
                Quest: null)
            : new(
                QuestRespondRefusal.Closed,
                $"Quest `#{move.Quest.Id}` is {move.Quest.Status} — a closed quest does not move; a new ask is a new title.",
                Quest: null);
    }
}
