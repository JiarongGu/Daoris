namespace Daoris.Knowledge;

/// <summary>Why a publish did not produce a quest — or <see cref="None"/> when it did.</summary>
public enum QuestPublishRefusal
{
    None,

    /// <summary>Addressed to the repository doing the asking. Its own backlog is the place for that.</summary>
    SelfAddressed,

    /// <summary>
    /// Nothing could answer it: the target is not registered, or it has neither adopted nor a root on
    /// this machine (D34 as amended by D70).
    /// </summary>
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

    /// <summary>
    /// The address names a lane its repository does not declare, or any lane of a repository that
    /// declares none (D115 §2.2). The answer names the lanes there are.
    /// </summary>
    UnknownLane,

    /// <summary>
    /// A requirement that is not one (DRIFT1c, D133 §3): it lacks the person's words or its check, there
    /// are too many or one is too long, or the quest is asked on no ask whose words it could quote.
    /// </summary>
    BadRequirement,

    /// <summary>A requirement quotes words the person never said on the ask (DRIFT1c) — the answer names them.</summary>
    NotQuoted,

    /// <summary>A short title past <see cref="QuestTitles.MaxShort"/> characters, or over a line break (SESSUX1j).</summary>
    BadShortTitle,

    /// <summary>
    /// The same words as a quest this machine cleared after its remote numbered it (HIST1b, D153 point 3): the remote holds
    /// it closed, and would refuse a fresh copy on every pass, so a new ask is a new title.
    /// </summary>
    Cleared,
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

    /// <summary>The session whose connector publishes, when one does (SESS1) — kept on the quest.</summary>
    public string? PublishedBy { get; init; }

    /// <summary>
    /// What the person requires (DRIFT1c, D133 §3), each quoting their words on the ask that asks it —
    /// judged here against that ask's words, so every door refuses the same quote.
    /// </summary>
    public IReadOnlyList<QuestRequirement> Requirements { get; init; } = [];

    /// <summary>
    /// Its short title (SESSUX1j): the few words that tell it apart in a list, the publisher's own — judged here, so every
    /// door refuses the same one. Null or blank is none, and the quest is then named from its words.
    /// </summary>
    public string? Short { get; init; }
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

    /// <summary>Not take, done, decline or wait — or <c>whileOpen</c> on anything but a decline (PAUSE1c).</summary>
    UnknownAction,

    /// <summary>Declining needs a reason: it is the part the asker can act on.</summary>
    MissingReason,

    /// <summary>No quest under that id.</summary>
    NotFound,

    /// <summary>
    /// Somebody got there first — the losing side of the race stands down (D47 §5). Also a done, a decline or a wait this
    /// machine makes on a quest whose take it lost (WAITCLAIM2): made on a claim it never held.
    /// </summary>
    AlreadyTaken,

    /// <summary>Done and Declined are terminal: one title is one quest forever (D46 §3).</summary>
    Closed,

    /// <summary>A wait that waits on nothing (D79): no question named, an unknown or answered one, itself, or a quest nobody has taken.</summary>
    CannotWait,

    /// <summary>A done that leaves one of the quest's requirements unanswered (DRIFT1d, D133 §4) — the answer names each.</summary>
    Unanswered,

    /// <summary>
    /// An answer that is not one (DRIFT1d): it names no requirement the quest carries, or one twice; it says both met
    /// and departed, or neither; a departure lacks its reason or the person's words; or it came with no done to answer.
    /// </summary>
    BadAnswer,

    /// <summary>A departure quotes words the person never said (DRIFT1d) — the answer names them.</summary>
    NotQuoted,

    /// <summary>A yes to a quest nothing holds (DRIFT1d, EVID1a): not closed done held for the person, or already accepted.</summary>
    NotHeld,

    /// <summary>
    /// A verdict on a quest whose done waits on no evidence (EVID1a): not closed done, none of its met requirements names
    /// any, its evidence was already found, or the person accepted the done as it stands.
    /// </summary>
    NotAwaitingEvidence,

    /// <summary>
    /// A verdict that is not one (EVID1a): not one in shape, by the judge the wire calls too (REFAC3,
    /// <see cref="QuestEvidenceVerdict.JudgeShape"/>) — no full commit id, a way of reading or a result nobody wrote, a
    /// spelling off a <c>case</c> read — or an item the quest does not wait on, one read twice, or an item it waits on
    /// left unread. The answer names which.
    /// </summary>
    BadVerdict,
}

/// <param name="Refusal"><see cref="QuestRespondRefusal.None"/> when the status moved.</param>
/// <param name="Message">The full answer, phrased once here.</param>
/// <param name="Quest">The quest as it now stands, when the status moved.</param>
public sealed record QuestRespondOutcome(QuestRespondRefusal Refusal, string Message, Quest? Quest);

/// <summary>Why a delete did not delete — or <see cref="None"/> when it did (D95).</summary>
public enum QuestDeleteRefusal
{
    None,

    /// <summary>No quest under that id.</summary>
    NotFound,

    /// <summary>
    /// Something stands on it — taken, done or declined, a session started for it, or a taken quest
    /// waiting on it — so its record stays.
    /// </summary>
    Kept,

    /// <summary>Another machine's take reached the remote first: the delete lost, and the quest stays, taken.</summary>
    TakenElsewhere,
}

/// <param name="Refusal"><see cref="QuestDeleteRefusal.None"/> when the quest was deleted.</param>
/// <param name="Message">The full answer, phrased once here for every door.</param>
/// <param name="Quest">The quest as it stood before the delete, when there was one.</param>
public sealed record QuestDeleteOutcome(QuestDeleteRefusal Refusal, string Message, Quest? Quest)
{
    /// <summary>Deleted here, and its remote has not taken the delete yet — the next pass carries it.</summary>
    public bool Unconfirmed { get; init; }
}

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
/// <param name="sessions">
/// The session records — what a delete asks whether any session was started for a quest (D95). Null
/// where none are kept, where no session can stand in a delete's way.
/// </param>
/// <param name="asks">
/// The asks — whose words a requirement's quote is checked against (DRIFT1c, D133 §3). Null where none
/// are kept, where a requirement has no words to quote and is refused rather than kept unchecked.
/// </param>
public sealed class QuestExchange(
    KnowledgeService service, QuestStore quests, IRemotes? remotes = null, QuestFiles? files = null,
    SessionStore? sessions = null, AskStore? asks = null)
{
    /// <summary>How many requirements a quest carries — what the work is measured by, not everything said.</summary>
    public const int MaxRequirements = 20;

    /// <summary>How long a requirement's quote, or its check, may be.</summary>
    public const int MaxRequirementLength = 2000;

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

    /// <summary>
    /// The store this exchange judges over — for the ask desk, which reads an ask's standing from the
    /// quests asked by it (USE1c) and judges nothing of its own about them.
    /// </summary>
    internal QuestStore Store => quests;

    /// <summary>A quest that carries nothing but its words — every caller before D65.</summary>
    public Task<QuestPublishOutcome> PublishAsync(
        string from, string to, string title, string body, DateTimeOffset now, CancellationToken ct = default) =>
        PublishAsync(new QuestAsk(from, to, title, body), now, ct);

    /// <summary>
    /// Publish a quest to another repository, or to lanes of a repository (`repository:lane+lane`,
    /// D115 §2.2), its own included. Refuses a self-addressed quest that names no lane, a target nothing
    /// could answer, and a lane the target does not declare; warns when the target has not adopted, or
    /// has declared nothing about itself (D34, D70). It is published HERE,
    /// whoever the receiver is (D68), and a joined receiver's quest reaches the remote on the next sync.
    /// What it carries is judged before anything is written anywhere, and its files are kept HERE —
    /// under this machine's home — wherever the record travels (D65 §2).
    /// </summary>
    public async Task<QuestPublishOutcome> PublishAsync(QuestAsk ask, DateTimeOffset now, CancellationToken ct = default)
    {
        // `repository:lane+lane` is split once, here (D115 §2.2): from now on `to` is the repository,
        // and the lanes ride beside it to the store.
        var address = QuestAddress.Parse(ask.To);
        var (from, to, title, body) = (ask.From, address.Repository, ask.Title, ask.Body);

        // Work for another lane is work for another session, so a repository may ask one of its own
        // lanes (D115, amending this refusal). To itself whole is still its own backlog's.
        if (!address.Named && string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
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
        var sender = registered.Named(from);
        // A registered sender's row decides its circle. A sender with none — an ask (D65 §1a) — names
        // its own, and silence places it where an unregistered sender always was.
        var home = sender?.InWorkspace ?? Workspaces.Normalize(ask.Workspace);

        // Who can be asked is who shares the asker's circle. A refusal that listed the whole machine
        // would be offering repositories this one may not address.
        var addressable = registered
            .Where(r => r.Addressable && Workspaces.Same(r.InWorkspace, home))
            .Select(r => r.Repository)
            .ToList();
        var target = registered.Named(to);

        if (target is null || !target.Addressable)
        {
            // A quest nothing could answer would sit in a queue that is never opened. Saying so now
            // beats letting it look delivered.
            return new(
                QuestPublishRefusal.NotAddressable,
                (target is null
                    ? $"`{to}` is not registered here, so nothing could see a quest to it."
                    : $"`{to}` has not adopted Daoris and has no root on this machine, so it has no client "
                      + "to see a quest and no tree a session could be started in.")
                + $" Addressable: {string.Join(", ", addressable)}.",
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

        // The registration's lanes are what an address is judged by (D115 §2.2): the line's file is
        // the authority for what runs, and this is what askers read.
        var (lanes, unfitLane) = JudgeLanes(address, target);
        if (unfitLane is not null)
        {
            return new(QuestPublishRefusal.UnknownLane, unfitLane, Quest: null, addressable);
        }

        // What it carries is judged here, before any door writes anything: a refused ask must leave
        // nothing behind — not a record, and not a file no record names.
        var carried = Judge(ask);
        if (carried.Refusal is { } unfit)
        {
            return new(unfit, carried.Message, Quest: null, addressable);
        }

        // The short title is the publisher's words (SESSUX1j), judged here so every door refuses the same one.
        var (shortTitle, unfitShort) = QuestTitles.Judge(ask.Short);
        if (unfitShort is not null)
        {
            return new(QuestPublishRefusal.BadShortTitle, unfitShort, Quest: null, addressable);
        }

        // A chain is judged now, while the person or the intake composing it can still act on the
        // answer — not at a close nobody is watching (D65 §4). Whether this circle shares with a team
        // is this machine's wiring; whether a receiver is shared is its registration (design §8).
        var circleWired = remotes?.For(home) is not null;
        if (JudgeChain(ask, registered, home, circleWired, circleWired && target.Joined, addressable) is { } unfitChain)
        {
            return new(QuestPublishRefusal.BadChain, unfitChain, Quest: null, addressable);
        }

        // What the person requires is judged against what they said (DRIFT1c, D133 §3), here, so every door
        // refuses the same quote: its shape first, then whether they said it.
        var (required, unfitRequirement) = JudgeRequirementShape(ask.Requirements);
        if (unfitRequirement is not null)
        {
            return new(QuestPublishRefusal.BadRequirement, unfitRequirement, Quest: null, addressable);
        }

        if (await JudgeQuotesAsync(from, required, ct).ConfigureAwait(false) is { } unquoted)
        {
            return new(unquoted.Refusal, unquoted.Message, Quest: null, addressable);
        }

        // A cleared quest a remote numbered is forgotten here (HIST1b, D153 point 3), and its words make its id again: the
        // remote still holds it closed and would refuse a fresh copy on every pass, so this is answered as a closed quest is.
        // Where the quest simply went (no remote ever numbered it), nothing is marked and the words make it again.
        if (await quests.ForgottenIdAsync(from, to, title, lanes, ct).ConfigureAwait(false) is { } cleared)
        {
            return new(
                QuestPublishRefusal.Cleared,
                $"Quest `#{cleared}` was cleared from this machine; the remote for `{home}` holds it closed. A new ask is a new title.",
                Quest: null, addressable);
        }

        var quest = await quests.PublishAsync(
            from, to, title, body, now, home, carried.Links, carried.Attachments, ask.Then, ct: ct,
            publishedBy: ask.PublishedBy, lanes: lanes, requirements: required, shortTitle: shortTitle).ConfigureAwait(false);

        var caution = !target.Adopted
            // Registered is addressable; adopted is disciplined (D70). Said at publish, because it is
            // the one fact that decides whether the quest is ever answered: a person working there by
            // hand has no connector, and a machine driving over the pipe door holds it.
            ? $"\n\n⚠ `{target.Repository}` has not adopted Daoris, so only a session the driver starts "
              + "in it over the protocol door can answer this — it is handed a connector there, while "
              + "a person working in it by hand has none to see the quest."
            : target.Registered
                ? ""
                : $"\n\n⚠ `{target.Repository}` has not declared what it owns or accepts, so this may not be "
                  + "its problem. Worth checking before you rely on it.";

        // Said of the quest as it stands: a publish already held answers with the requirements it was kept with.
        var requirements = quest.Requirements.Count switch
        {
            0 => "",
            1 => " It carries 1 requirement, in the person's own words.",
            var count => $" It carries {count} requirements, each in the person's own words.",
        };

        return new(
            QuestPublishRefusal.None,
            $"Published quest `#{quest.Id}` to `{QuestAddress.Spell(quest.To, quest.Lanes)}` — {quest.Status}.{requirements}{caution}\n\n"
            + "It is held by the service, not written into that repository. Its agent will see it and "
            + "decide. Do not make the change yourself."
            + await KeepAsync(quest, ask, carried, ct).ConfigureAwait(false),
            quest, addressable);
    }

    /// <summary>
    /// Whether the lanes an address names are the receiver's (D115 §2.2): each must be one its
    /// registration declares, matched in any case and kept in the declared spelling, once, sorted. Any
    /// lane of a repository that declares none is refused, saying to address the repository; an
    /// unknown lane is refused, naming the lanes there are and how to address one.
    /// </summary>
    /// <returns>The lanes to keep on the quest, or why the address cannot be taken.</returns>
    private static (IReadOnlyList<string> Lanes, string? Refusal) JudgeLanes(QuestAddress address, Registration target)
    {
        if (!address.Named) return ([], null);

        var asked = $"{address.Repository}:{string.Join('+', address.Lanes)}";
        var declared = target.DeclaredLanes;
        if (declared.Count == 0)
        {
            return ([], $"`{target.Repository}` declares no lanes, so a quest to it addresses the repository: "
                        + $"`{target.Repository}`, not `{asked}`.");
        }

        var theirs = string.Join(", ", declared.Select(lane => $"`{lane.Id}`" + (lane.Title.Length > 0 ? $" ({lane.Title})" : "")));
        var how = $"Address one as `{target.Repository}:{declared[0].Id}`, or several as `{target.Repository}:<lane>+<lane>`.";
        if (address.Lanes.Count == 0)
        {
            return ([], $"`{asked}` names no lane after the colon. `{target.Repository}`'s lanes: {theirs}. {how}");
        }

        var kept = new List<string>();
        var unknown = new List<string>();
        foreach (var name in address.Lanes)
        {
            if (declared.FirstOrDefault(lane => string.Equals(lane.Id, name, StringComparison.OrdinalIgnoreCase)) is { } lane)
            {
                if (!kept.Contains(lane.Id, StringComparer.Ordinal)) kept.Add(lane.Id);
            }
            else if (!unknown.Contains(name, StringComparer.Ordinal))
            {
                unknown.Add(name);
            }
        }

        if (unknown.Count > 0)
        {
            return ([], $"`{target.Repository}` declares no lane {string.Join(" or ", unknown.Select(name => $"`{name}`"))}. "
                        + $"Its lanes: {theirs}. {how}");
        }

        return (kept.Order(StringComparer.Ordinal).ToList(), null);
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

            var receiver = registered.Named(step.To);
            if (receiver is null || !receiver.Addressable || !Workspaces.Same(receiver.InWorkspace, home))
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
    /// The requirements as a quest keeps them (DRIFT1c, D133 §3): each trimmed at its ends, with both its
    /// quote and its check, at most <see cref="MaxRequirements"/> of them, each half at most
    /// <see cref="MaxRequirementLength"/> characters — or why not, naming which.
    /// </summary>
    /// <remarks>
    /// The shape alone, which a remote judges of a pushed quest too: whether the person said the quote is
    /// judged where the ask is (<see cref="JudgeQuotesAsync"/>), and a remote holds no asks.
    /// </remarks>
    private static (IReadOnlyList<QuestRequirement> Requirements, string? Refusal) JudgeRequirementShape(
        IReadOnlyList<QuestRequirement> given)
    {
        if (given.Count > MaxRequirements)
        {
            return ([], $"A quest carries at most {MaxRequirements} requirements — this one was given {given.Count}. "
                        + "Quote the words the work is measured by; everything the person said stays on the ask.");
        }

        var kept = new List<QuestRequirement>();
        foreach (var (requirement, index) in given.Select((requirement, index) => (requirement, index + 1)))
        {
            var quote = requirement.Quote?.Trim() ?? "";
            var check = requirement.Check?.Trim() ?? "";
            if (quote.Length == 0 || check.Length == 0)
            {
                return ([], $"Requirement {index} needs both halves: the person's own words, quoted, and the check "
                            + "that proves the work meets them.");
            }

            if (quote.Length > MaxRequirementLength || check.Length > MaxRequirementLength)
            {
                return ([], $"Requirement {index}'s {(quote.Length > MaxRequirementLength ? "quote" : "check")} is longer "
                            + $"than {MaxRequirementLength} characters — quote the words the work turns on, and say the "
                            + "check in a few lines.");
            }

            var (evidence, unfit) = JudgeEvidence(index, requirement.Evidence ?? []);
            if (unfit is not null) return ([], unfit);
            kept.Add(new QuestRequirement(quote, check) { Evidence = evidence });
        }

        return (kept, null);
    }

    /// <summary>
    /// A requirement's evidence as a quest keeps it (EVID1a, D144 §2): at most <see cref="QuestEvidence.MaxItems"/> items,
    /// each exactly one of a path or a gate, a path trimmed at its ends and judged (<see cref="QuestEvidence.JudgePath"/>),
    /// a gate's name judged — or why not, naming the requirement. A gate is refused for now, naming the queue it waits for.
    /// </summary>
    /// <remarks>
    /// The shape alone, which a remote judges of a pushed quest too: written by whoever writes the requirement, never by
    /// the session it will judge, and read later by the driver, never here.
    /// </remarks>
    private static (IReadOnlyList<QuestEvidence> Evidence, string? Refusal) JudgeEvidence(int index, IReadOnlyList<QuestEvidence> given)
    {
        if (given.Count > QuestEvidence.MaxItems)
        {
            return ([], $"Requirement {index} names {given.Count} items of evidence, and a requirement names at most "
                        + $"{QuestEvidence.MaxItems}: the facts its check turns on, not an inventory of the work.");
        }

        var kept = new List<QuestEvidence>();
        foreach (var (item, number) in given.Select((item, number) => (item, number + 1)))
        {
            var which = $"Requirement {index}'s evidence item {number}";
            var path = item.Path?.Trim();
            var gate = item.Gate?.Trim();
            if (string.IsNullOrEmpty(path) == string.IsNullOrEmpty(gate))
            {
                return ([], $"{which} names exactly one of `path` or `gate`: a file or folder the done's commit must hold, "
                            + "or a gate the receiving repository declares.");
            }

            if (!string.IsNullOrEmpty(path))
            {
                if (QuestEvidence.JudgePath(path) is { } why)
                {
                    return ([], $"{which}, `{Clip(path)}`, is not a path a commit can be asked for: {why}. Name it from the "
                                + "receiving repository's root with forward slashes, e.g. `docs/report.md`.");
                }

                if (!kept.Any(each => each.Path == path)) kept.Add(new QuestEvidence(path));
                continue;
            }

            if (QuestEvidence.JudgeGate(gate!) is { } unfit)
            {
                return ([], $"{which}, `{Clip(gate!)}`, is not a gate's name: {unfit}.");
            }

            // Until the landing queue reads gates (D144 point 5), nothing here could read one: refused, not kept unread.
            return ([], $"{which} names the gate `{gate}`. A gate is read from the landing queue's verdict, and this build "
                        + "has no landing queue to read it from yet, so a gate cannot be named. Say the gate in the check, "
                        + "and name a path the work leaves if there is one.");
        }

        return (kept, null);
    }

    /// <summary>
    /// Whether the person said what each requirement quotes (DRIFT1c, D133 §3): somewhere in one of their
    /// words on the ask that asks this quest — its sentence, an answer to a session, or a message added to
    /// one (DRIFT1a) — verbatim, whitespace and case aside. Null when every quote is theirs, or there are none.
    /// </summary>
    /// <remarks>
    /// <para><b>Only the asker's ask.</b> A quest one repository asks of another is asked on no ask, so there
    /// are no words of the person's to quote; which ask a repository's session works for is its record's, and
    /// is not read here.</para>
    ///
    /// <para><b>A fact, with no model</b> (D24, D54): a reading of the person's words is the body's, never a
    /// requirement, so a quote that is not theirs is refused rather than compared by meaning.</para>
    /// </remarks>
    private async Task<(QuestPublishRefusal Refusal, string Message)?> JudgeQuotesAsync(
        string from, IReadOnlyList<QuestRequirement> requirements, CancellationToken ct)
    {
        if (requirements.Count == 0) return null;

        if (AskDesk.AskOf(from) is not { } askId)
        {
            return (QuestPublishRefusal.BadRequirement,
                $"A requirement quotes the person's own words on the ask a quest is asked by, and this quest is asked "
                + $"by `{from}`, on no ask — so there are no words of theirs to quote. Say what is needed in the body.");
        }

        if (asks is null || await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } held)
        {
            return (QuestPublishRefusal.BadRequirement,
                $"This quest is asked by ask `#{askId}`, which this host does not hold, so there are no words to check "
                + "a requirement's quote against. Say what is needed in the body.");
        }

        var unsaid = requirements
            .Select((requirement, index) => (requirement.Quote, Index: index + 1))
            .Where(quoted => !held.Words.Any(word => QuestRequirement.QuotedIn(quoted.Quote, word.Text)))
            .ToList();
        if (unsaid.Count == 0) return null;

        var since = held.WordsKeptFrom is { } keptFrom
            ? " Its words are kept from "
              + keptFrom.UtcDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
              + " UTC: what the person said on it before then is in its sessions' records, not on the ask."
            : "";
        return (QuestPublishRefusal.NotQuoted,
            (unsaid.Count == 1 ? "A requirement quotes" : $"{unsaid.Count} requirements quote")
            + $" words the person never said on ask `#{held.Id}`:\n\n"
            + string.Join("\n", unsaid.Select(quoted => $"- requirement {quoted.Index}: \"{Clip(quoted.Quote)}\""))
            + "\n\nA requirement is the person's own words, verbatim (whitespace and case aside), from the ask or from "
            + "what they said on it since: an answer to a session, or a message added to one. Copy their words; your "
            + "reading of them belongs in the body." + since);
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
        // Only a quest whose receiver is joined in its circle ever leaves this machine (design §8); a
        // take on any other is complete the moment it commits.
        if (await SharedAtAsync(quest, ct).ConfigureAwait(false) is not { } remote) return null;

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
    /// The remote a quest leaves this machine for — its circle's, when its receiver is joined there
    /// (design §8) — or null for a quest that never leaves: no remote for its circle, or a receiver
    /// nobody shares.
    /// </summary>
    private async Task<IRemote?> SharedAtAsync(Quest quest, CancellationToken ct)
    {
        if (remotes?.For(quest.Workspace) is not { } remote) return null;

        var registered = await service.RegistryAsync(ct: ct).ConfigureAwait(false);
        return registered.Any(r => r.Joined && Workspaces.Same(r.InWorkspace, quest.Workspace)
                                   && string.Equals(r.Repository, quest.To, StringComparison.OrdinalIgnoreCase))
            ? remote
            : null;
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
        // Adopted, not merely addressable (D70): a deployment holds no roots, and a repository that
        // has not adopted has no manifest to declare a join with, so its quests never leave the
        // machine that can drive it. A pushed quest to one is a quest nobody here could answer.
        if (!registered.Any(r => r.Adopted && string.Equals(r.Repository, asked.To, StringComparison.OrdinalIgnoreCase)))
        {
            return $"`{asked.To}` is not registered at this deployment, so nobody here could answer quest "
                   + $"`#{asked.Id}`. Its registration travels first; the next sync tries again.";
        }

        // Its lanes were judged against the registration by the machine that published it (D115 §2.2),
        // and a registration here may lag its own; but a lane no registration could declare is no lane.
        if (asked.Lanes.FirstOrDefault(lane => !QuestAddress.IsLaneId(lane)) is { } malformed)
        {
            return $"Quest `#{asked.Id}` names `{Clip(malformed)}` as a lane, and no repository could declare it — a "
                   + "lane is lower-case letters, digits and dashes, starting with a letter.";
        }

        // Whether the person said a requirement's quote was judged where the ask is (DRIFT1c); a remote holds
        // no asks, but a requirement missing half of itself is no requirement wherever it lands.
        if (JudgeRequirementShape(asked.Requirements).Refusal is { } unfitRequirement)
        {
            return $"Quest `#{asked.Id}`: {unfitRequirement}";
        }

        // A short title a publish here would refuse is one no record should carry (SESSUX1j).
        if (QuestTitles.Judge(asked.Short).Refusal is { } unfitShort)
        {
            return $"Quest `#{asked.Id}`: {unfitShort}";
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
        string id, string action, string? reason, DateTimeOffset now, CancellationToken ct = default,
        // The question a `wait` waits on (D79): a quest the waiting session asked another repository.
        string? on = null,
        // How a `done` answers each of the quest's requirements (DRIFT1d, D133 §4).
        IReadOnlyList<QuestAnswer>? answers = null,
        // A decline that applies only while the quest is open (PAUSE1c, D132 point 10): an abandon's.
        bool whileOpen = false)
    {
        // Answers are a done's (DRIFT1d): carried by any other verb, a wait included, they would be dropped, and
        // dropped looks kept.
        if (answers is { Count: > 0 } && action.ToLowerInvariant() is "take" or "decline" or "wait")
        {
            return new(
                QuestRespondRefusal.BadAnswer,
                $"Answers are given when closing `done`, one for each requirement; a `{action.ToLowerInvariant()}` carries none. "
                + "Nothing moved.",
                Quest: null);
        }

        // The flag is a decline's (PAUSE1c): carried by any other verb it would be dropped, and dropped looks kept.
        if (whileOpen && !string.Equals(action, "decline", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                QuestRespondRefusal.UnknownAction,
                "`whileOpen` is a decline's: it makes the decline apply only while the quest is open, and a "
                + $"`{action.ToLowerInvariant()}` declines nothing. Nothing moved.",
                Quest: null);
        }

        if (string.Equals(action, "wait", StringComparison.OrdinalIgnoreCase))
        {
            return await WaitAsync(id.TrimStart('#'), on?.TrimStart('#'), now, ct).ConfigureAwait(false);
        }

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
                $"Unknown action '{action}' — one of: take, done, decline, wait.",
                Quest: null);
        }

        if (status == QuestStatus.Declined && string.IsNullOrWhiteSpace(reason))
        {
            return new(
                QuestRespondRefusal.MissingReason,
                "Declining needs a reason: it is the part the asker can act on.",
                Quest: null);
        }

        // A done answers each of the person's requirements (DRIFT1d, D133 §4), judged before the move. A quest's
        // requirements are fixed at its publish, so a look outside the write judges what the write will hold; whether
        // it may still close is the store's, inside it.
        IReadOnlyList<QuestAnswer> answered = [];
        if (status == QuestStatus.Done
            && await quests.FindAsync(id.TrimStart('#'), ct).ConfigureAwait(false) is { Status: QuestStatus.Open or QuestStatus.Taken } live)
        {
            var judged = await JudgeAnswersAsync(live, answers ?? [], ct).ConfigureAwait(false);
            if (judged.Refusal is { } refusal) return new(refusal, judged.Message!, Quest: null);
            answered = judged.Answers;
        }

        // Every verb commits here, shared or not (D68): the next sync carries it to the remote, where the
        // first push wins and a later one is kept as a conflict rather than lost (design §5).
        var move = await quests.MoveAsync(id.TrimStart('#'), status.Value, reason, now, ct, answered, whileOpen)
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
            return new(
                QuestRespondRefusal.None,
                $"Quest `#{move.Quest.Id}` is now {move.Quest.Status}.{await AnsweredAsync(move.Quest, ct).ConfigureAwait(false)}"
                // Only the next sync can say whether another machine took it first (PAUSE1c), so the answer says
                // what that would mean; a quest nobody shares has no other machine to take it.
                + (whileOpen && await SharedAtAsync(move.Quest, ct).ConfigureAwait(false) is not null
                    ? " It applies only while it is open: if another machine's take reached the remote first, the next "
                      + "sync keeps this decline on the quest as a conflict, and the take stands."
                    : "")
                + Then(move.FollowUp),
                move.Quest);
        }

        // A close on the winner's take, made after the pass that found this machine's take lost (WAITCLAIM2): the stand-down
        // the take's own loss says, since the session making it may not have heard that yet.
        if (move.ClaimLost)
        {
            return new(
                QuestRespondRefusal.AlreadyTaken,
                LostTake(move.Quest.Id, $"so this {action.ToLowerInvariant()} is not this machine's to make. Nothing moved."),
                Quest: null);
        }

        // A decline made while open, refused because somebody took the quest first (PAUSE1c): said as the decline it
        // is, never as the take race's stand-down, which is a taker's sentence.
        if (whileOpen && move.Quest.Status == QuestStatus.Taken)
        {
            return new(
                QuestRespondRefusal.AlreadyTaken,
                $"Quest `#{move.Quest.Id}` is Taken: this decline applies only while it is open, so nothing was declined "
                + "and the take stands. Its work is its taker's; decline it without `whileOpen` if you mean to stop that work.",
                Quest: null);
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

    /// <summary>
    /// Wait on a question asked of another repository (D79): the quest stays taken, marked, and the
    /// driver resumes it once the question is answered — done or declined — with the answer in hand.
    /// </summary>
    private async Task<QuestRespondOutcome> WaitAsync(string id, string? on, DateTimeOffset now, CancellationToken ct)
    {
        QuestRespondOutcome Refused(string why) => new(QuestRespondRefusal.CannotWait, why, Quest: null);

        if (string.IsNullOrWhiteSpace(on))
        {
            return Refused("A wait names the quest it waits on (`on`): publish the question to the repository "
                + "that knows, then wait on the quest that publish answered.");
        }

        if (string.Equals(on, id, StringComparison.OrdinalIgnoreCase))
        {
            return Refused($"Quest `#{id}` cannot wait on itself — wait on the question you asked another repository.");
        }

        if (await quests.FindAsync(on, ct).ConfigureAwait(false) is not { } question)
        {
            return Refused($"No quest `#{on}` to wait on. Ids come from `quest_publish` and `quest_list`.");
        }

        if (question.Status is QuestStatus.Done or QuestStatus.Declined)
        {
            return Refused($"Quest `#{on}` is already {question.Status}: its answer is there to read now "
                + "(`quest_list` with `includeClosed`), so there is nothing to wait for.");
        }

        var move = await quests.WaitAsync(id, on, now, ct).ConfigureAwait(false);
        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{id}`. Ids come from `quest_list`.", Quest: null);
        }

        // A wait on the winner's take, made after the pass that found this machine's take lost (WAITCLAIM2): the take's
        // stand-down, not a malformed wait, and the question the session asked is still asked.
        if (move.ClaimLost)
        {
            return new(
                QuestRespondRefusal.AlreadyTaken,
                LostTake(id, $"so it does not wait on `#{on}` here. Nothing moved; `#{on}` stays a quest of its own."),
                Quest: null);
        }

        if (!move.Moved)
        {
            return Refused($"Quest `#{id}` is {move.Quest.Status} — only a quest its taker is working can wait, "
                + "because the work in progress is what waits.");
        }

        return new(QuestRespondRefusal.None,
            $"Quest `#{id}` waits on `#{on}` (`{question.To}`). It stays taken by `{move.Quest.To}`; end your turn "
            + "now. The driver starts it again in the same tree once `#" + on + "` is answered, with the answer "
            + "in the instruction.", move.Quest);
    }

    /// <summary>
    /// A move or a wait refused because this machine's take lost (WAITCLAIM2), in one sentence for both: what happened to
    /// the take, then <paramref name="what"/>, what that means for this one, then the stand-down the take's own loss says.
    /// A remote refusing such a push says it in the same words (WAITCLAIM3), and the pushing machine's pass relays them.
    /// </summary>
    internal static string LostTake(string id, string what) =>
        $"Quest `#{id}` was taken on another machine first: this machine's take lost and is kept on the quest as a conflict, "
        + $"{what} Stand down rather than doubling the work.";

    /// <summary>The chain's next step a close or a yes published (D65 §4), said so whoever moved it knows whose it now is.</summary>
    private static string Then(Quest? followUp) => followUp is { } next
        ? $"\n\nThen: published `#{next.Id}` to `{next.To}` on behalf of `{next.From}` — {next.Status}."
          + (next.Then.Count > 0 ? $" {next.Then.Count} more step(s) follow it." : "")
        : "";

    // ——— A done answers each requirement, and a departure holds what follows for the person's yes (DRIFT1d, D133 §4).

    /// <summary>
    /// The person accepts a done's departure from what they required (DRIFT1d, D133 §4), or a done held for its evidence
    /// as it stands (EVID1a, D144 §6): what it held goes on — the chain's next step is published now, and a quest waiting
    /// on it resumes at the driver's next look. Only a held quest takes a yes; any other is refused saying why, as a
    /// state, not a malformed ask.
    /// </summary>
    /// <remarks>
    /// The person's door, never an agent's: no connector tool reaches it. It commits here and travels like any verb (D68).
    /// </remarks>
    public async Task<QuestRespondOutcome> AcceptAsync(string id, DateTimeOffset now, CancellationToken ct = default)
    {
        var quest = id.TrimStart('#');
        var move = await quests.AcceptAsync(quest, now, ct).ConfigureAwait(false);
        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{quest}`. Ids come from `quest_list`.", Quest: null);
        }

        if (!move.Moved)
        {
            var standing = move.Quest;
            return new(QuestRespondRefusal.NotHeld, standing switch
            {
                { Status: not QuestStatus.Done } =>
                    $"Quest `#{standing.Id}` is {standing.Status}: only a quest closed done with a departure from what you "
                    + "required waits for your yes.",
                { Accepted: { } at } =>
                    $"Quest `#{standing.Id}`'s departure was already accepted, {When(at)}: nothing waits for a yes.",
                _ => $"Quest `#{standing.Id}` closed done departing from none of what you required: nothing waits for a yes.",
            }, Quest: null);
        }

        var waiting = await quests.WaitingOnAsync(move.Quest.Id, ct).ConfigureAwait(false);
        var resumes = waiting.Count == 0
            ? ""
            : $"\n\n{Listed(waiting)} {(waiting.Count == 1 ? "waits" : "wait")} on it, and {(waiting.Count == 1 ? "resumes" : "resume")} "
              + "at the driver's next look.";
        // A done held for its evidence alone is accepted as it stands, unread or missing (EVID1a, D144 §6); a departure's
        // words are as they were, since its yes is the same yes.
        var accepted = move.Quest.Answers.Any(answer => answer.IsDeparture)
            ? $"Accepted the departure on quest `#{move.Quest.Id}`"
            : $"Accepted quest `#{move.Quest.Id}`'s done as it stands, its evidence {(move.Quest.Evidence is null ? "unread" : "not found")}";
        return new(QuestRespondRefusal.None, $"{accepted}: what it held goes on.{resumes}{Then(move.FollowUp)}", move.Quest);
    }

    /// <summary>
    /// The sentence a person's done writes on its quest (QUESTCLOSE1, D126's note): theirs, with their words after it where
    /// they gave any. Never translated: it is data, as a session's <i>The person finished this at a checkpoint.</i> is.
    /// </summary>
    public static string PersonDoneNote(string? words) =>
        Blank(words) is { } said ? $"The person marked this done: {said}" : "The person marked this done.";

    /// <summary>
    /// The person marks a quest done (QUESTCLOSE1, D126's note), open or taken: what the quest page's *Mark done…* and
    /// <c>daoris-driver quest done</c> say, after a session finished at a checkpoint left its quest taken, or whenever they
    /// decide its work is done. It is the person's done, so it answers none of their requirements one by one: they are the
    /// person's own words, and the done is their word on them. Its note says it was theirs (<see cref="PersonDoneNote"/>).
    /// </summary>
    /// <remarks>
    /// <para><b>The person's door, never an agent's</b>, as a yes is: no connector tool reaches it, and an agent's done still
    /// answers each requirement through <see cref="RespondAsync"/>. It commits here, publishes a chain's next step and lets a
    /// quest waiting on it resume, and travels like any done (D68): it is a done with a note and no answers.</para>
    ///
    /// <para><b>A closed quest does not move</b> (D46 §3), and a done on a take this machine lost is the lost take's
    /// stand-down (WAITCLAIM2), each said as the respond door says it.</para>
    /// </remarks>
    public async Task<QuestRespondOutcome> PersonDoneAsync(string id, string? words, DateTimeOffset now, CancellationToken ct = default)
    {
        var quest = id.TrimStart('#');
        var move = await quests.MoveAsync(quest, QuestStatus.Done, PersonDoneNote(words), now, ct).ConfigureAwait(false);
        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{quest}`. Ids come from `quest_list`.", Quest: null);
        }

        if (move.ClaimLost)
        {
            return new(
                QuestRespondRefusal.AlreadyTaken,
                LostTake(move.Quest.Id, "so this done is not this machine's to make. Nothing moved."),
                Quest: null);
        }

        if (!move.Moved)
        {
            return new(
                QuestRespondRefusal.Closed,
                $"Quest `#{move.Quest.Id}` is {move.Quest.Status} — a closed quest does not move; a new ask is a new title.",
                Quest: null);
        }

        var waiting = await quests.WaitingOnAsync(move.Quest.Id, ct).ConfigureAwait(false);
        var resumes = waiting.Count == 0
            ? ""
            : $"\n\n{Listed(waiting)} {(waiting.Count == 1 ? "waits" : "wait")} on it, and {(waiting.Count == 1 ? "resumes" : "resume")} "
              + "at the driver's next look.";
        return new(
            QuestRespondRefusal.None,
            $"Quest `#{move.Quest.Id}` is now Done: you marked it done.{resumes}{Then(move.FollowUp)}",
            move.Quest);
    }

    /// <summary>
    /// Daoris read a done's evidence (EVID1a, D144 §3): the driver's verdict at the end of the session that made the done,
    /// the orphan sweep's, or the person's check at the terminal. The quest must be done and waiting on its evidence, and
    /// the verdict must name the commit read and read every item of every met requirement, and nothing else. Found, what
    /// the done held goes on — its next step published in the same transaction; missing, it waits for the person.
    /// </summary>
    /// <remarks>
    /// <para>The driver's door, never an agent's: no connector tool reaches it, since the session a requirement judges
    /// never writes its verdict (D144 §1). It commits here and travels like any verb (D68).</para>
    ///
    /// <para><b>A fact, with no model</b> (D24, D54): the exchange cannot read a commit, so it judges only that the verdict
    /// reads what the done waits on, in codes, and keeps it. Whether the commit is the done's or follows it on the same
    /// history is the reader's to settle, which has the tree.</para>
    /// </remarks>
    public async Task<QuestRespondOutcome> EvidenceAsync(
        string id, QuestEvidenceVerdict verdict, DateTimeOffset now, CancellationToken ct = default)
    {
        var name = id.TrimStart('#');
        if (await quests.FindAsync(name, ct).ConfigureAwait(false) is not { } quest)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        if (NotAwaiting(quest) is { } why) return new(QuestRespondRefusal.NotAwaitingEvidence, why, Quest: null);
        if (JudgeVerdict(quest, verdict) is { } unfit) return new(QuestRespondRefusal.BadVerdict, unfit + " Nothing was kept.", Quest: null);

        var judged = verdict with { Commit = verdict.Commit.ToLowerInvariant() };
        var move = await quests.EvidenceAsync(name, judged, now, ct).ConfigureAwait(false);
        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        // Judged again inside the write: another verdict or the person's yes may have come first.
        if (!move.Moved)
        {
            return new(QuestRespondRefusal.NotAwaitingEvidence, NotAwaiting(move.Quest)
                ?? $"Quest `#{name}` no longer waits on what this verdict reads.", Quest: null);
        }

        return new(QuestRespondRefusal.None, await EvidencedAsync(move.Quest, judged, ct).ConfigureAwait(false) + Then(move.FollowUp), move.Quest);
    }

    /// <summary>Why a verdict on <paramref name="quest"/> is not taken, as a state — or null while its done waits on evidence.</summary>
    private static string? NotAwaiting(Quest quest) => quest switch
    {
        { AwaitsEvidence: true } => null,
        { Status: not QuestStatus.Done } =>
            $"Quest `#{quest.Id}` is {quest.Status}: evidence is read on a done, once the session that closed it ends.",
        { Accepted: { } at } =>
            $"Quest `#{quest.Id}`'s done was accepted as it stood, {When(at)}: nothing waits on its evidence.",
        { Evidence: { Found: true } found } =>
            $"Quest `#{quest.Id}`'s evidence was already found, at `{Short(found.Commit)}`: nothing waits on it.",
        _ => $"Quest `#{quest.Id}`'s done names no evidence on a requirement it met: nothing waits on it.",
    };

    /// <summary>
    /// Why <paramref name="verdict"/> does not read exactly what <paramref name="quest"/> waits on, naming the first fault
    /// and every item left unread — or null when it does. Its shape is the one judge's (REFAC3,
    /// <see cref="QuestEvidenceVerdict.JudgeShape"/>), which the wire calls too; what it reads is judged here, against the
    /// quest, as the replay's <see cref="QuestEvidenceVerdict.Covers"/> judges it.
    /// </summary>
    private static string? JudgeVerdict(Quest quest, QuestEvidenceVerdict verdict)
    {
        if (verdict.JudgeShape() is { } fault) return Worded(fault, verdict);

        var wanted = quest.EvidenceWanted;
        var read = new HashSet<(int, string?, string?)>();
        foreach (var (item, index) in verdict.Items.Select((item, index) => (item, index + 1)))
        {
            var which = $"Item {index}";
            var named = item.Path ?? item.Gate!;
            if (!wanted.Any(each => each.Requirement == item.Requirement && item.Names(each.Item)))
            {
                return item.Requirement > quest.Requirements.Count
                    ? $"{which} reads requirement {item.Requirement}, and quest `#{quest.Id}` carries {quest.Requirements.Count}."
                    : $"{which} reads `{Clip(named)}` for requirement {item.Requirement}, which its done does not wait on: "
                      + "a verdict reads each item of each requirement the done met, as the requirement names it.";
            }

            if (!read.Add((item.Requirement, item.Path, item.Gate)))
            {
                return $"{which} reads `{Clip(named)}` for requirement {item.Requirement}, which is read twice: read each once.";
            }
        }

        var unread = wanted.Where(each => !read.Contains((each.Requirement, each.Item.Path, each.Item.Gate))).ToList();
        return unread.Count == 0
            ? null
            : $"Quest `#{quest.Id}`'s done waits on {Count(wanted.Count, "item")} of evidence, and this verdict leaves "
              + $"{(unread.Count == 1 ? "one" : unread.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))} unread:\n\n"
              + string.Join("\n", unread.Select(each => $"- requirement {each.Requirement}: `{each.Item.Named}`"))
              + "\n\nRead each in the commit, and say what each was found to be.";
    }

    /// <summary>A verdict's shape fault as the evidence door answers it: which field, on which item, and why.</summary>
    private static string Worded(QuestVerdictFault fault, QuestEvidenceVerdict verdict)
    {
        if (fault.Item is not { } number)
        {
            return fault.Field switch
            {
                QuestVerdictField.Commit =>
                    $"A verdict names the commit it read by its full id (40 or 64 hex characters), and `{Clip(verdict.Commit ?? "")}` is not one.",
                QuestVerdictField.How =>
                    $"A verdict says how its commit was chosen: `session-end`, `sweep` or `terminal`, and `{Clip(verdict.How ?? "")}` is none of them.",
                _ => $"A verdict names the session whose end it read by its id, and `{Clip(verdict.Session ?? "")}` is not one.",
            };
        }

        var item = verdict.Items[number - 1];
        var which = $"Item {number}";
        var named = item.Path ?? item.Gate ?? "";
        return fault.Field switch
        {
            QuestVerdictField.Requirement => $"{which} reads requirement {item.Requirement}, and requirements are numbered from 1.",
            QuestVerdictField.PathOrGate => $"{which} names exactly one of `path` or `gate`, as the requirement's evidence does.",
            QuestVerdictField.Path => $"{which}, `{Clip(named)}`, is not a repository-relative path: {fault.Why}.",
            QuestVerdictField.Gate => $"{which}, `{Clip(named)}`, is not a gate's name: {fault.Why}.",
            QuestVerdictField.Result =>
                $"{which} says `{Clip(item.Result ?? "")}` of `{Clip(named)}`, and a {(item.Path is not null ? "path" : "gate")} is read as one of "
                + string.Join(", ", (item.Path is not null ? QuestEvidenceCodes.PathResults : QuestEvidenceCodes.GateResults).Select(code => $"`{code}`"))
                + ".",
            QuestVerdictField.Object =>
                $"{which} names the object found at `{Clip(named)}`, and `{Clip(item.Object ?? "")}` is not a full object id.",
            _ => $"{which} says the commit spells `{Clip(named)}` as `{Clip(item.Spelled ?? "")}`, read `{Clip(item.Result ?? "")}`: {fault.Why}.",
        };
    }

    /// <summary>
    /// What a kept verdict means for whoever posted it (EVID1a): found, and what the done held goes on; missing, which item
    /// was not there and the person's two doors; or found beside a departure, which still waits for their yes.
    /// </summary>
    private async Task<string> EvidencedAsync(Quest quest, QuestEvidenceVerdict verdict, CancellationToken ct)
    {
        var at = $"Read the evidence of quest `#{quest.Id}` at `{Short(verdict.Commit)}`";
        if (verdict.Found)
        {
            if (quest.Hold == QuestHold.Departed)
            {
                return $"{at}: each of its {Count(verdict.Items.Count, "item")} is there. It still departs from a requirement, "
                       + $"so it waits for the person's yes: `daoris-driver quest accept {quest.Id}`.";
            }

            var waiting = await quests.WaitingOnAsync(quest.Id, ct).ConfigureAwait(false);
            return $"{at}: each of its {Count(verdict.Items.Count, "item")} is there, so what it held goes on."
                   + (waiting.Count == 0
                       ? ""
                       : $"\n\n{Listed(waiting)} {(waiting.Count == 1 ? "waits" : "wait")} on it, and "
                         + $"{(waiting.Count == 1 ? "resumes" : "resume")} at the driver's next look.");
        }

        var missing = verdict.Items.Where(item => item.Result != QuestEvidenceCodes.Found).ToList();
        return $"{at}: {missing.Count} of its {Count(verdict.Items.Count, "item")} {(missing.Count == 1 ? "is" : "are")} not "
               + $"there, so it stays held for the person:\n\n"
               + string.Join("\n", missing.Select(item => $"- requirement {item.Requirement}: `{item.Path ?? item.Gate}` {item.Result}"
                   + (item.Spelled is { } spelled ? $" (the commit holds `{spelled}`)" : "")))
               + $"\n\nThe person accepts the done as it stands with `daoris-driver quest accept {quest.Id}`, or reads it again "
               + $"at a later commit on the same history with `daoris-driver quest check {quest.Id} --commit <sha>`.";
    }

    /// <summary>A commit as a sentence names it: its first seven characters.</summary>
    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;

    /// <summary>
    /// The answers a done gives, judged against the quest's requirements (DRIFT1d, D133 §4): one for each, by its
    /// number, each <c>met</c> with how its check was met, or <c>departed</c> with the reason and the person's words it
    /// turns on, which must be theirs. The answers as kept, trimmed at their ends, in the requirements' order — or why
    /// not, naming each requirement or answer at fault.
    /// </summary>
    /// <remarks>
    /// <para><b>A quote is a fact</b> (D24, D54): a departure's words are matched as a requirement's are
    /// (<see cref="QuestRequirement.QuotedIn"/>), against the person's words on the ask where this host holds it,
    /// and always against the quest's own requirements, which were checked where the ask is and travel with the
    /// quest. A reading attributed to the person without their words is what this refuses.</para>
    ///
    /// <para><b>A quest with none</b> closes as it always did, and takes no answers.</para>
    /// </remarks>
    private async Task<(IReadOnlyList<QuestAnswer> Answers, QuestRespondRefusal? Refusal, string? Message)> JudgeAnswersAsync(
        Quest quest, IReadOnlyList<QuestAnswer> given, CancellationToken ct)
    {
        (IReadOnlyList<QuestAnswer>, QuestRespondRefusal?, string?) Bad(string why) =>
            ([], QuestRespondRefusal.BadAnswer, why + " Nothing was closed.");

        var required = quest.Requirements;
        if (required.Count == 0)
        {
            return given.Count == 0
                ? ([], null, null)
                : Bad($"Quest `#{quest.Id}` carries no requirements, so a `done` answers none: close it with a note.");
        }

        var kept = new Dictionary<int, QuestAnswer>();
        foreach (var (answer, index) in given.Select((answer, index) => (answer, index + 1)))
        {
            var which = $"Answer {index}";
            if (answer.Requirement < 1 || answer.Requirement > required.Count)
            {
                return Bad(answer.Requirement < 1
                    ? $"{which} names no requirement: answer each by its number, from 1, as the quest lists them."
                    : $"{which} names requirement {answer.Requirement}, and quest `#{quest.Id}` carries {required.Count}.");
            }

            which = $"{which} (requirement {answer.Requirement})";
            if (kept.ContainsKey(answer.Requirement))
            {
                return Bad($"Requirement {answer.Requirement} is answered twice: answer each once.");
            }

            var met = Blank(answer.Met);
            var departed = Blank(answer.Departed);
            var quote = Blank(answer.Quote);
            if (met is not null && departed is not null)
            {
                return Bad($"{which} says `met` and `departed` at once: say which.");
            }

            if (met is null && departed is null)
            {
                // A departure's reason left blank is a departure without its reason, not an answer that says nothing.
                return Bad(answer.Departed is not null
                    ? $"{which} departs without its reason: say why the work departed from it."
                    : $"{which} says neither `met` nor `departed`: say how its check was met, or why the work departed from it.");
            }

            if (met is not null && answer.Quote is not null)
            {
                return Bad($"{which} is met, and a met answer quotes nothing: its requirement already quotes the person.");
            }

            if (departed is not null && quote is null)
            {
                return Bad($"{which} departs without the person's own words it turns on (`quote`), copied exactly from what "
                    + "they said: a reading of their words is the reason, never their words.");
            }

            var tooLong = new[] { ("how", met), ("reason", departed), ("quote", quote) }
                .FirstOrDefault(half => half.Item2 is { Length: > MaxRequirementLength });
            if (tooLong.Item2 is not null)
            {
                return Bad($"{which}'s {tooLong.Item1} is longer than {MaxRequirementLength} characters: say it in a few lines.");
            }

            kept[answer.Requirement] = new QuestAnswer(answer.Requirement, met, departed, quote);
        }

        var unanswered = Enumerable.Range(1, required.Count).Where(number => !kept.ContainsKey(number)).ToList();
        if (unanswered.Count > 0)
        {
            return ([], QuestRespondRefusal.Unanswered,
                $"Quest `#{quest.Id}` carries {Count(required.Count, "requirement")}, each the person's own words, and this "
                + $"`done` leaves {(unanswered.Count == 1 ? "one" : unanswered.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))} unanswered:\n\n"
                + string.Join("\n", unanswered.Select(number =>
                    $"- requirement {number}: \"{Clip(required[number - 1].Quote)}\" — check: {Clip(required[number - 1].Check)}"))
                + "\n\nAnswer each by its number: `met`, with how its check was met, or `departed`, with the reason and the "
                + "person's own words it turns on (`quote`), copied exactly. A departure is shown to the person, and what "
                + $"follows this quest waits for their yes. Nothing was closed; the quest stays {quest.Status}.");
        }

        var ordered = kept.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
        if (await UnquotedAsync(quest, ordered, ct).ConfigureAwait(false) is { } unquoted)
        {
            return ([], QuestRespondRefusal.NotQuoted, unquoted);
        }

        return (ordered, null, null);
    }

    /// <summary>
    /// Why a departure's words are not the person's (DRIFT1d), naming each — or null when every departure quotes them:
    /// their words on the ask where this host holds it, and the quest's requirements wherever it is.
    /// </summary>
    private async Task<string?> UnquotedAsync(Quest quest, IReadOnlyList<QuestAnswer> answers, CancellationToken ct)
    {
        var departures = answers.Where(answer => answer.IsDeparture).ToList();
        if (departures.Count == 0) return null;

        var askId = AskDesk.AskOf(quest.From);
        var held = askId is not null && asks is not null ? await asks.FindAsync(askId, ct).ConfigureAwait(false) : null;
        var words = (held?.Words.Select(word => word.Text) ?? []).Concat(quest.Requirements.Select(requirement => requirement.Quote)).ToList();

        var unsaid = departures.Where(answer => !words.Any(text => QuestRequirement.QuotedIn(answer.Quote!, text))).ToList();
        if (unsaid.Count == 0) return null;

        var where = held is not null
            ? $"on ask `#{held.Id}`"
            : $"in quest `#{quest.Id}`'s requirements — this host does not hold ask `#{askId}`, so here a departure may "
              + "quote only the requirements' own words";
        return (unsaid.Count == 1 ? "A departure quotes" : $"{unsaid.Count} departures quote")
            + $" words the person never said {where}:\n\n"
            + string.Join("\n", unsaid.Select(answer => $"- requirement {answer.Requirement}: \"{Clip(answer.Quote!)}\""))
            + "\n\nA departure quotes the person's own words it turns on, verbatim (whitespace and case aside): from the ask, "
            + "what they said on it since, or the requirement itself. A reading of their words attributed to them is what "
            + "this refuses: say it in the reason. Nothing was closed.";
    }

    /// <summary>
    /// What a done said of its requirements (DRIFT1d), for whoever closed it: each met, or which departed and what that
    /// holds — the next step, a quest waiting on it, or the ask — until the person accepts it. Nothing for a quest with none.
    /// </summary>
    private async Task<string> AnsweredAsync(Quest closed, CancellationToken ct)
    {
        if (closed.Answers.Count == 0) return "";
        var departed = closed.Answers.Count(answer => answer.IsDeparture);
        var met = closed.Answers.Count == 1 ? " Its requirement is met." : $" Each of its {closed.Answers.Count} requirements is met.";
        if (!closed.Held) return met;

        // What a met answer's evidence is, for the session that can still commit it (EVID1a, D144 §2): Daoris reads the
        // work's last commit when the session ends, and a met answer without it holds the quest for the person.
        var wanted = closed.EvidenceWanted;
        var read = $"Daoris reads {string.Join(", ", wanted.Select(each => $"`{each.Item.Named}`").Distinct())} in this work's "
                   + $"last commit when this session ends, so commit {(wanted.Count == 1 ? "it" : "them")} first if you have not";
        if (departed == 0)
        {
            return $"{met} It is held until Daoris reads its evidence: {read}. Until it is found, "
                   + $"{string.Join("; and ", await HoldsAsync(closed, "it is found", ct).ConfigureAwait(false))}.";
        }

        return $" It departed from {departed} of its {Count(closed.Answers.Count, "requirement")}, so it is held for the "
               + $"person's yes: {string.Join("; and ", await HoldsAsync(closed, "they accept it", ct).ConfigureAwait(false))}. "
               + $"The departure is kept on the quest, and the person accepts it with `daoris-driver quest accept {closed.Id}`."
               + (closed.AwaitsEvidence ? $" Its evidence is read too: {read}." : "");
    }

    /// <summary>What a held done holds, each said as waiting <paramref name="until"/>: its next step, a quest waiting on it, or its ask.</summary>
    private async Task<IReadOnlyList<string>> HoldsAsync(Quest closed, string until, CancellationToken ct)
    {
        var holds = new List<string>();
        if (closed.Then.Count > 0)
        {
            var next = closed.Then[0];
            holds.Add($"the next step, \"{next.Title.Replace("{parent}", $"#{closed.Id}", StringComparison.Ordinal)}\" to "
                      + $"`{next.To}`, is published only once {until}");
        }

        var waiting = await quests.WaitingOnAsync(closed.Id, ct).ConfigureAwait(false);
        if (waiting.Count > 0)
        {
            holds.Add($"{Listed(waiting)}, which {(waiting.Count == 1 ? "waits" : "wait")} on it, "
                      + $"{(waiting.Count == 1 ? "resumes" : "resume")} only then");
        }

        if (holds.Count == 0) holds.Add($"nothing follows it, and what it was asked for stays open until {until}");
        return holds;
    }

    /// <summary>Quests by id and receiver, as an answer names them.</summary>
    private static string Listed(IReadOnlyList<Quest> quests) =>
        string.Join(", ", quests.Select(quest => $"`#{quest.Id}` (`{quest.To}`)"));

    private static string Count(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";

    private static string When(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // ——— Deleting a quest made by mistake (D95).

    /// <summary>
    /// Delete a quest nobody has started on (D95): open, no session record naming it, and no taken quest
    /// waiting on it. One that may have left this machine is tombstoned, and deleted by push when its
    /// circle's remote answers, as a take claims by push (D69); any other simply goes. The files this
    /// machine kept for it go with it.
    /// </summary>
    public async Task<QuestDeleteOutcome> DeleteAsync(string id, DateTimeOffset now, CancellationToken ct = default)
    {
        var quest = await quests.FindAsync(id.TrimStart('#'), ct).ConfigureAwait(false);
        if (quest is null)
        {
            return new(QuestDeleteRefusal.NotFound, $"No quest `#{id.TrimStart('#')}`. Ids come from `quest_list`.", Quest: null);
        }

        if (await KeptAsync(quest, ct).ConfigureAwait(false) is { } kept)
        {
            return new(QuestDeleteRefusal.Kept, $"Quest `#{quest.Id}` {kept.Stands}, so it stays. {kept.Instead}", quest);
        }

        var remote = await SharedAtAsync(quest, ct).ConfigureAwait(false);
        var deletion = await quests.DeleteAsync(quest.Id, now, travels: remote is not null, ct).ConfigureAwait(false);
        if (!deletion.Deleted)
        {
            // It moved between the look and the write: the store judged it again, inside the write.
            return deletion.Quest is { } moved && await KeptAsync(moved, ct).ConfigureAwait(false) is { } since
                ? new(QuestDeleteRefusal.Kept, $"Quest `#{quest.Id}` {since.Stands}, so it stays. {since.Instead}", moved)
                : new(QuestDeleteRefusal.NotFound, $"No quest `#{quest.Id}`. Ids come from `quest_list`.", Quest: null);
        }

        if (remote is null)
        {
            files?.Forget(quest.Id);
            return new(
                QuestDeleteRefusal.None,
                deletion.Tombstoned
                    ? $"Deleted quest `#{quest.Id}`. It had reached a remote, so the delete stays in its history, where a sync can carry it."
                    : $"Deleted quest `#{quest.Id}` — it never left this machine, so nothing else holds a copy.",
                quest);
        }

        // Deleted by push (D69's shape): the pass runs before the answer returns, so a delete that lost to
        // another machine's take is known now, not at a tick nobody watches.
        var pass = await QuestSync.RunAsync(quests, service, remote, quest.Workspace, ct).ConfigureAwait(false);
        if (await quests.FindAsync(quest.Id, ct).ConfigureAwait(false) is { } back)
        {
            return new(
                QuestDeleteRefusal.TakenElsewhere,
                $"Quest `#{quest.Id}` was taken on another machine first — its take reached the remote before this "
                + $"delete, so the quest stays, {back.Status}. Decline it instead, with the reason, if it should not be done.",
                back);
        }

        files?.Forget(quest.Id);
        var confirmed = (await quests.HistoryAsync(quest.Id, ct).ConfigureAwait(false))
            .LastOrDefault(operation => operation.Kind == QuestOperationKind.Deleted && operation.Machine == quests.Machine)
            ?.Number is not null;
        return new(
            QuestDeleteRefusal.None,
            confirmed
                ? $"Deleted quest `#{quest.Id}` — the remote confirmed it, and every machine drops it on its next sync."
                : $"Deleted quest `#{quest.Id}` here, UNCONFIRMED: "
                  + (pass.Problem ?? pass.Refused.FirstOrDefault(r => r.Quest == quest.Id)?.Reason
                      ?? "the remote has not taken the delete yet")
                  + ". It travels on the next sync — and if another machine takes the quest first, it comes back, taken.",
            quest)
        {
            Unconfirmed = !confirmed,
        };
    }

    /// <summary>
    /// Which of <paramref name="candidates"/> may be deleted (D95) — the judgement <see cref="DeleteAsync"/>
    /// runs, answered for a list so a page offers the verb only where the service would take it.
    /// </summary>
    public async Task<IReadOnlySet<string>> DeletableAsync(IEnumerable<Quest> candidates, CancellationToken ct = default)
    {
        var open = candidates.Where(quest => quest.Status == QuestStatus.Open).ToList();
        if (open.Count == 0) return new HashSet<string>();

        var started = sessions is null
            ? new HashSet<string>()
            : await sessions.QuestsNamedAsync(ct).ConfigureAwait(false);
        var awaited = await quests.AwaitedAsync(ct).ConfigureAwait(false);
        return open.Select(quest => quest.Id)
            .Where(id => !started.Contains(id) && !awaited.Contains(id))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Why a quest's record must stay (D95), in a phrase that follows its name, with what to do instead
    /// — null when nothing stands on it and it may go.
    /// </summary>
    internal async Task<(string Stands, string Instead)?> KeptAsync(Quest quest, CancellationToken ct)
    {
        const string Decline = "Decline it instead, with the reason, and the asker hears why.";
        const string AlreadyGone = "A closed quest already leaves the list, and shows again only with closed ones included.";

        switch (quest.Status)
        {
            case QuestStatus.Taken:
                return ("is Taken — someone is working it", Decline);
            case QuestStatus.Done:
                return ("is Done — it is the record of work that was answered", AlreadyGone);
            case QuestStatus.Declined:
                return ("is Declined — the decline is the trace of a decision, and its reason is the asker's to read", AlreadyGone);
        }

        if (sessions is not null && await sessions.AnyForQuestAsync(quest.Id, ct).ConfigureAwait(false) is { } session)
        {
            return ($"is open, but session `{session.Id}` was started for it and its record names the quest", Decline);
        }

        if ((await quests.WaitingOnAsync(quest.Id, ct).ConfigureAwait(false)).FirstOrDefault() is { } waiting)
        {
            return ($"is the question quest `#{waiting.Id}` waits on, and deleted, that quest would wait on nothing",
                "Decline it instead, with the reason, and its taker is resumed with it.");
        }

        return null;
    }
}
