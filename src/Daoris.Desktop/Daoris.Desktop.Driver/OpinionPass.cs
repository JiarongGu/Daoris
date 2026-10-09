namespace Daoris.Driver;

/// <summary>
/// What a pass reads, and for whom (XAGENT1d, D155; the second-agent design §4): the occasion, the working session whose work it
/// is, the repository and its checkout, the candidate's base and tip as git can name them, the rule that asked for it, and the
/// quests and words the work was asked by. Nothing asks for one yet but its tests: the gate (XAGENT1f) and the person's ask do.
/// </summary>
/// <param name="Occasion">One of <c>landing</c>, <c>steps</c>, <c>failure</c> or <c>asked</c> (design §2.1).</param>
/// <param name="Working">The working session's record: the one whose work is read.</param>
/// <param name="Root">The repository's registered checkout, which the candidate's commits are read from and the copy made of.</param>
/// <param name="Base">Where the work starts: a commit or a name git reads as one.</param>
/// <param name="Tip">Where it ends, which the copy stands at.</param>
public sealed record OpinionPassAsk(
    string Occasion, string Working, string Repository, string Root, string Base, string Tip, OpinionRule Rule)
{
    /// <summary>The workspace the repository is in; the default one where none is named.</summary>
    public string? Workspace { get; init; }

    /// <summary>The quests of the work in this repository, their requirements, closes and evidence as the service answered them.</summary>
    public IReadOnlyList<QuestView> Quests { get; init; } = [];

    /// <summary>The person's words on the ask the work was asked by; null for work no ask asked.</summary>
    public AskWords? Words { get; init; }

    /// <summary>
    /// For the one recheck (XAGENT1e, design §6.5), the first pass whose findings it reads again and the working session's
    /// answers as the driver read them; null for a first pass. Its base is that pass's tip, and its tip the working tree's.
    /// </summary>
    public OpinionRecheckOf? Rechecks { get; init; }

    /// <summary>
    /// Told the opinion's id and its reviewer's session the moment the host opens them (XAGENT1f): the gate keeps the ask then, so
    /// a door reads the pass as being read while it runs, never as not asked. Null tells nobody, as a recheck's run does.
    /// </summary>
    public Action<string, string>? Opened { get; init; }
}

/// <summary>
/// What a recheck reads again (XAGENT1e, design §4, §6.5): the first pass, its findings, and the working session's answers as
/// the driver read them when its turn ended, each fix's commit checked.
/// </summary>
public sealed record OpinionRecheckOf(OpinionView First, OpinionReading Answers);

/// <summary>
/// How a pass went (XAGENT1d): its line, whether its reviewer's record opened, the opinion and the session, how the record
/// ended, the tier and the code (design §10), what held the reviewer read-only, and anything the pass could not tidy.
/// </summary>
/// <param name="Line">What a terminal and a look say, one line, opening with what became of it.</param>
/// <param name="Opened">Whether the reviewer's record opened: false is a pass that started nothing.</param>
public sealed record OpinionPassRun(string Line, bool Opened)
{
    /// <summary>The second opinion on the record, once the service opened it.</summary>
    public string? Opinion { get; init; }

    /// <summary>The reviewer's session record.</summary>
    public string? Session { get; init; }

    /// <summary>How the reviewer's record ended, in its public spelling; null where none opened.</summary>
    public string? State { get; init; }

    /// <summary>Who read it (design §10): <see cref="OpinionPass.TierAgent"/>, or <see cref="OpinionPass.TierNone"/> where none could.</summary>
    public string Tier { get; init; } = OpinionPass.TierAgent;

    /// <summary>Why no other maker's agent read it, the choice's code (§3.3); null where one did.</summary>
    public string? Code { get; init; }

    /// <summary>What held the reviewer read-only (<see cref="OpinionPosture"/>); null where no reviewer ran.</summary>
    public string? Posture { get; init; }

    /// <summary>Why the copy's folder is left, where removing it failed; null where it went, or none was made.</summary>
    public string? TreeLeft { get; init; }

    /// <summary>
    /// A pass no reviewer could read (§3.3, §10): tier <c>none</c>, with the choice's code and its sentence, and nothing started.
    /// Never <i>no issues</i>: no agent read it.
    /// </summary>
    public static OpinionPassRun Unavailable(ReviewerChoice choice) =>
        new($"unavailable  no agent could read this: {choice.Sentence}", false) { Tier = OpinionPass.TierNone, Code = choice.Code };
}

/// <summary>The pass's own words and bounds (XAGENT1d, design §5.6, §10), each said once for every door.</summary>
public static class OpinionPass
{
    /// <summary>A pass a reviewer's session read.</summary>
    public const string TierAgent = "agent";

    /// <summary>A pass no agent could read: said with its code, never as <i>no issues</i>.</summary>
    public const string TierNone = "none";

    /// <summary>
    /// The code words said to a reviewer are refused with (D155 point 5, design §5.6), the service's say door's own: a reviewer
    /// reads in one turn, and nobody talks to it while it reads.
    /// </summary>
    public const string WordsCode = "opinion";

    /// <summary>The door a reviewer's record is opened by, as the session log's <c>session.started</c> names it.</summary>
    public const string OpenedKind = "opinion";

    /// <summary>
    /// How long one pass may run (§5.6): the rule's minutes, or the machine's session timeout if that comes first. The opinion
    /// keeps the rule's own minutes; this is when its process is stopped.
    /// </summary>
    public static int Bound(OpinionRule rule, DriverConfig config) => Math.Max(1, Math.Min(rule.Bound, config.TimeoutMinutes));

    /// <summary>
    /// What a reviewer's transcript says where this machine has no connector, the install's command first, as
    /// <see cref="KnowledgeConnector.Candidates"/> ranks them: it can read, but its opinion is said only through the tool.
    /// </summary>
    public static string NoConnector =>
        $"— no {KnowledgeConnector.ExecutableName} on this machine, so this second opinion has no connector: it can read the "
        + "work, but cannot say its opinion, which is said only through `opinion_give`, and the pass fails when it ends. "
        + "`npm run publish:desktop -- --to <install> --service` lays one beside the application, and "
        + "`npm run publish:service -- --install` lands one in the home's `bin/`.";

    /// <summary>The line a reviewer's transcript opens with: what it reads, where, and what holds it read-only (§5.2).</summary>
    public static string Opening(OpinionCandidateRead candidate, string posture) =>
        $"— a second opinion on `{candidate.Repository}` at `{candidate.Tip[..Math.Min(7, candidate.Tip.Length)]}`, "
        + $"{OpinionPosture.Said(posture)}: it reads in a copy of its own, and nothing in it is taken back.";

    /// <summary>Why a person's words are refused for a reviewer while it reads, the sentence the bridge carries verbatim.</summary>
    public static string TakesNoWords(string opinion) =>
        $"the session reading second opinion `{opinion}` takes no words: a reviewer reads the work in one turn and says its "
        + "opinion through its tool, and nobody talks to it while it reads. What it says is shown with the opinion.";

    /// <summary>
    /// The families that wrote the work, as the opinion names them (§3.4): each one's owner, the agent whose accounts it runs as
    /// (AGT7), once, in the order first named.
    /// </summary>
    public static IReadOnlyList<string> Families(IEnumerable<AgentFamily> working) =>
        [.. working.Select(family => family.Owner).Distinct(StringComparer.OrdinalIgnoreCase)];
}
