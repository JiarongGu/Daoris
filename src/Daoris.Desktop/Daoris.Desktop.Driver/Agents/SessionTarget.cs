using System.Diagnostics;

namespace Daoris.Driver;

/// <param name="QuestId">The quest the session exists to serve.</param>
/// <param name="Title">One line: what is wanted.</param>
/// <param name="Body">Why, and the evidence — the asker's words, verbatim.</param>
/// <param name="Asker">Who asked.</param>
/// <param name="Repository">Whose agent the session is.</param>
/// <param name="Root">The working tree it runs in.</param>
/// <param name="ServiceUrl">Where the service is, so the session can claim and close its own quest.</param>
public sealed record SessionTarget(
    string QuestId,
    string Title,
    string Body,
    string Asker,
    string Repository,
    string Root,
    string ServiceUrl)
{
    /// <summary>The selected adapter's commit attribution instruction, composed for this start.</summary>
    public string? CommitTrailer { get; init; }

    /// <summary>
    /// For a carry-on, the part of its line before it as that part's record says it ran, so a start of another maker
    /// is told that work left uncommitted in the tree carries the earlier maker's trailer too. Null for every other start.
    /// </summary>
    public EarlierMaker? LeftBy { get; init; }

    /// <summary>What this start's actor asked for, carried by every native prepare.</summary>
    public ActorAsked? Asked { get; init; }

    /// <summary>The entry of its actor's order a new start took, its record's <c>choice</c>; null keeps the record's.</summary>
    public string? Choice { get; init; }
    /// <summary>Addresses the quest carries, handed over as the asker gave them.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Files the quest carries, with where this machine keeps each — or null where it does not.</summary>
    public IReadOnlyList<QuestFileView> Attachments { get; init; } = [];

    /// <summary>What the service publishes when this closes done — told, so the session knows.</summary>
    public IReadOnlyList<QuestStepView> Then { get; init; } = [];

    /// <summary>
    /// What the person requires of the quest, handed verbatim beneath it with how its done answers each.
    /// Empty for a quest that names none, and the instruction reads as it did.
    public IReadOnlyList<QuestRequirementView> Requirements { get; init; } = [];

    /// <summary>The quest this one follows, when it is a step of a chain.</summary>
    public string? Parent { get; init; }

    /// <summary>
    /// The ask this session answers — an INTAKE, which serves no quest: its spawn carries the
    /// ask and its own session instead, and no quest variable at all. Null for a quest's session.
    /// </summary>
    public string? Ask { get; init; }

    /// <summary>
    /// The second opinion this session gives: a REVIEWER, which serves no quest and no ask. Its spawn
    /// carries its own session and its instruction, and no quest variable at all. Null for every other session.
    /// </summary>
    public string? Opinion { get; init; }

    /// <summary>
    /// The ask this session answers as a helper: a read-only session on another agent that serves
    /// the session which asked, and no quest, no ask and no opinion. Its spawn carries its own session and its instruction, as a
    /// reviewer's does. Null for every other session.
    /// </summary>
    public string? Helper { get; init; }

    /// <summary>
    /// The piece this session writes as a partner: a one-turn session on another agent that serves its lead,
    /// and no quest, no ask and no opinion. Its spawn carries its own session and its instruction, as a helper's does. Null otherwise.
    /// </summary>
    public string? Piece { get; init; }

    /// <summary>
    /// The home the start's tools are read from: the one the driver's environment names, as the
    /// driver hands it. Null reads this process's <c>DAORIS_HOME</c>.
    /// </summary>
    public string? Home { get; init; }

    /// <summary>
    /// The permission prompts a driven session of a quest runs under: the posture its protocol door asks, or
    /// the permission mode its native door names, as its repository's setup resolved at this start or resume. Null keeps the adapter's
    /// own, as an intake, a reviewer, a helper and a brainstorm have.
    /// </summary>
    public IAgentAccess? Access { get; init; }

    /// <summary>
    /// The brainstorm this turn is a pass of: one turn in a room holding the packet and its instruction,
    /// on no quest, ask, opinion or repository. Its spawn carries the brainstorm, the turn and where its result goes. Null for every
    /// other session.
    /// </summary>
    public string? Brainstorm { get; init; }

    /// <summary>The brainstorm's turn this session is (<c>g1</c>, <c>s1</c>, <c>g1r</c>…); null for every other session.</summary>
    public string? BrainstormTurn { get; init; }

    /// <summary>
    /// The file a brainstorm turn's result goes to, which its result tool writes; outside its room, and named to the
    /// spawn only, never in its instruction. Null for every other session.
    /// </summary>
    public string? BrainstormAnswer { get; init; }

    /// <summary>
    /// Whether the session is one turn framed as one prompt, taking no person's line and never resumed: an intake, a
    /// reviewer, which reads in one turn and starts a fresh conversation every pass, a helper, and a brainstorm's pass.
    /// </summary>
    public bool OneTurn => Ask is not null || Opinion is not null || Helper is not null || Brainstorm is not null;

    /// <summary>
    /// The session's own record — handed to every session, whose connector names it when it publishes
    /// (an intake's, or a quest's).
    /// </summary>
    public string? Session { get; init; }

    /// <summary>
    /// The instruction, when it is not a quest's — an intake's job (<see cref="IntakePrompt"/>). Null
    /// composes the claiming instruction, as every target always has.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// What the session's standing block is composed from (<see cref="StandingBlock"/>), set by every start that
    /// composes an instruction and bound to its harness at the spawn (<see cref="TargetPrompt.ForStart"/>): appended to the instruction, or
    /// named there where it goes through its file. Null hands none, and the instruction reads as it did.
    /// </summary>
    public StandingFacts? Block { get; init; }

    /// <summary>
    /// For a session that RESUMES a waiting quest: the question an earlier session asked another
    /// repository, now closed, with its answer. Null composes the first start's claiming instruction.
    /// </summary>
    public QuestView? Answered { get; init; }

    /// <summary>
    /// For a session CARRYING ON a quest a cut-off left taken: what the cut-off session's record
    /// said about its end. Null for every other session.
    /// </summary>
    public string? CutOff { get; init; }

    /// <summary>
    /// For a session carrying on an OPEN quest in the tree of an earlier session that committed work and failed before it took it:
    /// what that record said about its end. The quest is still to take, and the work is to finish, never to build
    /// again. Null for every other session.
    /// </summary>
    public string? Retried { get; init; }

    /// <summary>What the tree holds uncommitted when a session carries on — git's short lines.</summary>
    public IReadOnlyList<string> InFlight { get; init; } = [];

    /// <summary>
    /// For a session going on in a tree that stands — a resume, a carry-on, a further part gone into its session's tree:
    /// what its start did against the line (<see cref="TreeStartWords"/>), said before how to go
    /// on. Null where there is nothing to tell it, and the instruction reads as it did.
    /// </summary>
    public string? InSync { get; init; }

    /// <summary>
    /// Its repository's held remotes at its start, in the driver's words, so it does not try what
    /// Daoris holds; null where none is held.
    /// </summary>
    public string? Git { get; init; }

    /// <summary>
    /// For a session in a tree whose files stage kept, placed or installed its dependencies: which, and to skip the install that
    /// would delete them (<see cref="TreeFilesOutcome.Told"/>), said before how to take the quest. Null where none stands ready, and
    /// the instruction reads as it did.
    /// </summary>
    public string? Dependencies { get; init; }

    /// <summary>
    /// For a session in a repository that marks heavy gates, with <c>daoris</c> on its PATH: to run focused checks while it works
    /// and those gates once, at its end, through <c>run-heavy</c>
    /// (<see cref="HeavyTold.For"/>), said before how to take the quest. Null where neither holds, and the instruction reads as it did.
    /// </summary>
    public string? Heavy { get; init; }

    /// <summary>
    /// What the person answered, when the session before parked to ask them — the carry-on
    /// is handed it in their words. Null for a carry-on after a cut-off.
    /// </summary>
    public string? PersonSaid { get; init; }

    /// <summary>
    /// For a resume: the words kept on the session that asked while its quest waited, which it never took, in the
    /// order said, each with the id that record gave it: the person's, and Daoris's own. Quoted after what came back, and shown in the
    /// conversation under their ids after the target. Empty for every other start, and the instruction reads as it did.
    /// </summary>
    public IReadOnlyList<SaidWordView> Handed { get; init; } = [];

    /// <summary>
    /// The person's words on the ask this quest is asked by, read for this start: every one, newest
    /// last, beneath the quest, on a first start, a resume and a carry-on alike, so what the person said outlives the hops
    /// a quest makes across cut-offs and accounts. Null for a quest no ask asked, and the instruction reads as it did.
    /// </summary>
    public IAgentAskWords? Words { get; init; }

    /// <summary>
    /// What waited on the ask's channel for this part's line of work, read for this start: the person's
    /// messages and the ask's other sessions', since the entry last read it, beneath the quest. Null for a quest on no ask, a
    /// channel that could not be read, or nothing waiting, and the instruction reads as it did.
    /// </summary>
    public ChannelHeld? Channel { get; init; }

    /// <summary>
    /// The language the work asks its sessions to write to the person in: its repository's, else its
    /// workspace's, read from <c>driver.json</c> for this start and named in one line after the close instruction. Null where
    /// neither is set, and the instruction reads as it did.
    /// </summary>
    public SessionLanguage? Language { get; init; }

    /// <summary>
    /// Whether its repository's access setup asks no go-ahead (<c>goAheads</c> <c>never</c>, full access
    /// among them), read for this start: the instruction then asks one only where the quest or the person's words demand it.
    /// False is the default, <c>named</c>, and the instruction reads as it did.
    /// </summary>
    public bool NoGoAheads { get; init; }

    /// <summary>
    /// Whether the quest closes through its closure graph: bound at its
    /// take, or bound already. Its session closes its work with `ready`, never `done`, which the service refuses on such a quest, on a
    /// first start, a resume and a carry-on alike. False for every other quest, and the instruction reads as it did.
    /// </summary>
    public bool Ready { get; init; }

    /// <summary>
    /// Whether the session before is one the person stopped, and has since released the quest from that stop with *Try
    /// again*: the carry-on is told so, rather than that the session before is cut off.
    /// </summary>
    public bool Released { get; init; }

    /// <summary>
    /// The agent the session before ran on, where the quest is handed over from it to this one: the
    /// carry-on is told of the hand-over and carries on from that session's plan and words, never that the person released it.
    /// Null for every other start, and the instruction reads as it did.
    /// </summary>
    public string? HandedOver { get; init; }

    /// <summary>
    /// The note the session before's agent left where it asked to hand over: told after its plan and last
    /// words as that agent's own claim. Null for every other start, and where it left none.
    /// </summary>
    public string? HandedNote { get; init; }

    /// <summary>
    /// Whether the session before stood down for a lost take that the person has since brought forward: the carry-on
    /// is told the press, not that the session is cut off. Its <see cref="CutOff"/> is the words of that bring-forward.
    /// </summary>
    public bool BroughtForward { get; init; }

    /// <summary>
    /// For a carry-on: Daoris's brief of the line it continues (<see cref="ContextBrief"/>), in place of
    /// the cut-off session's last plan and last words and the tree's uncommitted list, which the brief reads itself. A
    /// section of the instruction, counted by its own sections within its account. Null where none
    /// is composed, and the instruction reads as it did.
    public ComposedBrief? Brief { get; init; }

    /// <summary>
    /// For a start that holds no brief: its line's course as Daoris counts it,
    /// the brief's item and its account, where any count of its line is not zero. Null where none is, and the instruction reads as before.
    /// </summary>
    public ComposedBrief? Course { get; init; }

    /// <summary>
    /// For a carry-on after an account's limit: the cut-off session ran on another account, which is
    /// now cooling, so nothing of its own conversation carries over. Names no account: the instruction is the agent's.
    /// </summary>
    public bool AccountChanged { get; init; }

    /// <summary>
    /// The branch this session's tree grew from when it is a chain's next step in its parent's
    /// repository — the parent's unmerged work is in the tree. Null for the canonical line.
    /// </summary>
    public string? GrewFrom { get; init; }

    /// <summary>
    /// For a first start that is a further part of a session: the parts before it here, and
    /// how its tree stands to theirs. Null for every other start, and the instruction reads as it did.
    /// </summary>
    public SessionParts? Parts { get; init; }

    /// <summary>
    /// How this session's work will land, when it runs in a tree of its own — so a session
    /// whose work goes through review knows not to merge or push it. Null says nothing of it; a merge says
    /// where it goes only beside <see cref="Commits"/>.
    /// </summary>
    public IStartLanding? LandsOn { get; init; }

    /// <summary>
    /// The commit rule standing for this session's repository and where it is set, set
    /// beside <see cref="LandsOn"/> only for a session in a tree of its own: <see cref="ProcessWords"/> says it, with the landing,
    /// in the instruction's landing section on every form. Null says nothing of it, and the instruction reads as it did.
    /// </summary>
    public IStartCommits? Commits { get; init; }

    /// <summary>
    /// The other checkouts this session may read, the declared write targets among them. Empty — as
    /// when reading across is off everywhere — and the instruction reads as it always has.
    /// </summary>
    public IReadOnlyList<AcrossCheckout> ReadsAcross { get; init; } = [];

    /// <summary>The checkouts the person declared this repository may also write into. Empty is none.</summary>
    public IReadOnlyList<AcrossCheckout> WritesAcross { get; init; } = [];

    /// <summary>
    /// For a set-up step: the environment it shows its chain's
    /// work in, as the review rule here declares it at the start, so its instruction says what to show, by which procedure,
    /// where, and how to say it. Null for every other quest, and the instruction reads as it did.
    /// </summary>
    public ReviewEnvironment? SetUp { get; init; }

    /// <summary>
    /// Where the review rule naming <see cref="SetUp"/> is set (<see cref="ReviewSource"/>), so the instruction's account says
    /// its set-up section came from the rule on this machine for its repository or its workspace. Null reads as
    /// the repository's.
    /// </summary>
    public string? SetUpFrom { get; init; }

    /// <summary>
    /// What this session's agent is told of the browser it is handed: the one line
    /// (<see cref="InAppBrowserServers.Told"/>), set after the hand-over and only where a server that drives Daoris's browser is
    /// </summary>
    public string? Browser { get; init; }

    /// <summary>
    /// The target a quest's session is handed: the quest as the service answered it, run in
    /// <paramref name="workTree"/> — the repository's own tree where it opted in, its root
    /// otherwise — its standing block naming what that tree keeps: its code map and its indexes.
    /// </summary>
    public static SessionTarget ForQuest(QuestView quest, string workTree, string serviceUrl) =>
        new(quest.Id, quest.Title, quest.Body, quest.From, quest.To, workTree, serviceUrl)
        {
            Links = quest.Links,
            Attachments = quest.Attachments,
            Then = quest.Then,
            Parent = quest.Parent,
            Requirements = quest.Requirements,
            Block = new StandingFacts { Repository = quest.To, CodeMap = CodeMapFile.Find(workTree), Indexes = RepositoryIndexes.Find(workTree) },
        };

    /// <summary>
    /// The directory the session is handed as <c>DAORIS_QUEST_ATTACHMENTS</c> — the one the service
    /// keeps this quest's files in — or null when none of them is on this machine.
    /// </summary>
    public string? AttachmentsDirectory => Attachments
        .Select(file => file.Path)
        .OfType<string>()
        .Select(System.IO.Path.GetDirectoryName)
        .FirstOrDefault(directory => !string.IsNullOrEmpty(directory));
}
