using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Daoris.Knowledge.JsonFields;

namespace Daoris.Knowledge;

/// <summary>
/// Exactly what a pass read (XAGENT1c, D155 point 1; the second agent design §1): a base and a tip in one repository, and
/// the commits between, each by its full id as the driver read them from git.
/// </summary>
public sealed record OpinionCandidate(string Repository, string Base, string Tip, IReadOnlyList<string> Commits);

/// <summary>
/// Who read it (design §3.4): the adapter the walk chose, its product and maker as declared, how independent it is, and the
/// account it ran as. Daoris's own record of the choice, never the reviewer's word about itself (D24).
/// </summary>
/// <param name="Label">One of <see cref="Opinions.Labels"/>.</param>
public sealed record OpinionReviewer(string Adapter, string Label)
{
    /// <summary>The agent's product as its toolchain or plugin declares it; null where none is declared.</summary>
    public string? Product { get; init; }

    /// <summary>Its maker as declared; null where none is, which the label says.</summary>
    public string? Maker { get; init; }

    /// <summary>The account it ran as, a profile's name: this machine's, like everything an opinion keeps.</summary>
    public string? Account { get; init; }
}

/// <summary>
/// What the driver asks when it starts a pass (XAGENT1c; design §4–§6.2): the occasion, the pass, the session whose work it
/// reads, the candidate, the reviewer, what it read alike, the posture that held, the rule's minutes, and the reviewer's own
/// tree, a clone at the candidate (§5.1).
/// </summary>
/// <param name="Occasion">One of <see cref="Opinions.Occasions"/>.</param>
/// <param name="Pass">One of <see cref="Opinions.Passes"/>.</param>
/// <param name="Working">The working session: the record of this machine's whose work it reads.</param>
/// <param name="Posture">One of <see cref="Opinions.Postures"/>: what held the reviewer read-only (§5.2).</param>
/// <param name="Minutes">The rule's bound of one pass, <see cref="Opinions.FewestMinutes"/> to <see cref="Opinions.MostMinutes"/>.</param>
/// <param name="Tree">The reviewer's own tree, never the working session's.</param>
public sealed record OpinionAsk(
    string Occasion, string Pass, string Working, OpinionCandidate Candidate, OpinionReviewer Reviewer, string Posture, int Minutes,
    string Tree)
{
    /// <summary>For a recheck, the first pass it reads the commits since; null for a first pass.</summary>
    public string? Rechecks { get; init; }

    /// <summary>The families that wrote the candidate (design §3.4), as the driver read them.</summary>
    public IReadOnlyList<string> Families { get; init; } = [];

    /// <summary>The harness version the reviewer's spawn observed, for its session record (D49 §4).</summary>
    public string? HarnessVersion { get; init; }
}

/// <summary>
/// One claim a reviewer makes (design §6.1): how much it weighs, where it is, what it claims, why it matters, how to see it or
/// why it holds, and how sure the reviewer is, with a proposal where it has one. A claim, never a fact (§6.6).
/// </summary>
/// <param name="Weight">One of <see cref="Opinions.Weights"/>.</param>
/// <param name="Where">A path from the repository's root, with a line or a range, or a commit, or <c>general</c>.</param>
/// <param name="Reproduce">The steps or the command and what it showed, or, where the reviewer could not, its reasoning.</param>
/// <param name="Sure">One of <see cref="Opinions.Sureness"/>.</param>
public sealed record OpinionFinding(string Weight, string Where, string Claim, string Consequence, string Reproduce, string Sure)
{
    /// <summary>Its number in the pass, from 1, in the order said: what an answer names it by.</summary>
    public int Number { get; init; }

    /// <summary>A diagnosis, or a change proposed as text or a patch; null where none was given. The working session applies its own.</summary>
    public string? Proposal { get; init; }
}

/// <summary>A recheck's word on one of the first pass's findings (design §6.5): it <c>stands</c>, or is <c>withdrawn</c>.</summary>
/// <param name="Finding">The first pass's finding, by its number.</param>
/// <param name="Says">One of <see cref="Opinions.Rechecks"/>.</param>
public sealed record OpinionRecheck(int Finding, string Says);

/// <summary>What the reviewer said through its tool (design §6.1), once: its findings, what it read, and its limits.</summary>
public sealed record OpinionGiven(IReadOnlyList<OpinionFinding> Findings, string Read, DateTimeOffset At)
{
    /// <summary>What it did not read or could not tell; null where it said nothing.</summary>
    public string? Limits { get; init; }

    /// <summary>A recheck's word on each first-pass finding it named; a finding it named none for <c>stands</c>.</summary>
    public IReadOnlyList<OpinionRecheck> Rechecked { get; init; } = [];
}

/// <summary>Where the findings went (design §6.3): the working session, the word they wait in on its record, and when.</summary>
public sealed record OpinionHanded(string Session, string Word, DateTimeOffset At);

/// <summary>
/// The working session's answer to one finding (design §6.4): <c>fixed</c> with the commit it names, which the driver checks is
/// one the turn made; <c>rejected</c> with its evidence; or <c>unresolved</c> with why. The session's claim, never a fact.
/// </summary>
/// <param name="Said">One of <see cref="Opinions.Answers"/>.</param>
public sealed record OpinionAnswer(int Finding, string Said, DateTimeOffset At)
{
    /// <summary>A <c>fixed</c>'s commit, as the session named it: 7 to 64 hex characters, read from git by the driver.</summary>
    public string? Commit { get; init; }

    /// <summary>A <c>rejected</c>'s evidence: a check that shows it, or a reading.</summary>
    public string? Evidence { get; init; }

    /// <summary>An <c>unresolved</c>'s reason.</summary>
    public string? Why { get; init; }
}

/// <summary>
/// A second opinion on the record (XAGENT1c, D155 point 11; design §6.2): one pass by one reviewer over one candidate, kept by
/// the local host beside the session records and answered only on this machine (D47 §4). Never a quest operation: its
/// candidate is this machine's commits until they land, and its reviewer names an account.
/// </summary>
public sealed record Opinion(
    string Id, string Occasion, string Pass, string Working, string Session, OpinionCandidate Candidate, OpinionReviewer Reviewer,
    DateTimeOffset Asked)
{
    /// <summary>For a recheck, the first pass it reads the commits since; null for a first pass.</summary>
    public string? Rechecks { get; init; }

    /// <summary>The families that wrote the candidate.</summary>
    public IReadOnlyList<string> Families { get; init; } = [];

    /// <summary>What held the reviewer read-only; null on a record that says none.</summary>
    public string? Posture { get; init; }

    /// <summary>The rule's bound of this pass, in minutes.</summary>
    public int Minutes { get; init; } = Opinions.DefaultMinutes;

    /// <summary>Who read it: <c>agent</c> for every pass a reviewer's session reads (design §10).</summary>
    public string Tier { get; init; } = Opinions.Agent;

    /// <summary>What the reviewer said; null until it says it.</summary>
    public OpinionGiven? Given { get; init; }

    /// <summary>Where the findings went; null until they were handed.</summary>
    public OpinionHanded? Handed { get; init; }

    /// <summary>The working session's answers, one per finding it answered, the latest standing.</summary>
    public IReadOnlyList<OpinionAnswer> Answers { get; init; } = [];

    /// <summary>When the pass's minutes run out: past it, nothing it says is kept.</summary>
    public DateTimeOffset Due => Asked.AddMinutes(Minutes);
}

/// <summary>
/// An opinion and where its pass stands (design §6.1), derived when it is read from the opinion and its reviewer's session
/// record, never kept: <c>reading</c> while its session runs within its minutes, <c>given</c> once it said its opinion, and
/// <c>failed</c> when its session ended without one or its minutes ran out.
/// </summary>
/// <param name="State">One of <see cref="Opinions.Reading"/>, <see cref="Opinions.Given"/> or <see cref="Opinions.Failed"/>.</param>
/// <param name="Why">A failed pass's code, <see cref="Opinions.Ended"/> or <see cref="Opinions.OutOfTime"/>; null otherwise.</param>
public sealed record OpinionStanding(Opinion Opinion, string State, string? Why);

/// <summary>
/// The shapes, words and bounds of a second opinion on the record (XAGENT1c), each judged here once for every door and the
/// store's own columns.
/// </summary>
public static class Opinions
{
    /// <summary>A chain's work in one repository, before its look and its landing (design §2.1).</summary>
    public const string Landing = "landing";

    /// <summary>A step's work, before the chain's next step starts.</summary>
    public const string Steps = "steps";

    /// <summary>A struck or held quest, offered and started only by the person's press.</summary>
    public const string Failure = "failure";

    /// <summary>The person's own ask, at any time.</summary>
    public const string Asked = "asked";

    /// <summary>What occasions a pass may be asked on.</summary>
    public static readonly IReadOnlyList<string> Occasions = [Landing, Steps, Failure, Asked];

    /// <summary>The occasions the rule starts by itself, and so the ones the bound counts: the person's own presses are not capped (§8.3).</summary>
    public static readonly IReadOnlyList<string> Automatic = [Landing, Steps];

    /// <summary>The pass that reads the candidate first.</summary>
    public const string First = "first";

    /// <summary>The one pass that reads the commits made in answer to a first pass's findings (§6.5).</summary>
    public const string Recheck = "recheck";

    /// <summary>What passes there are.</summary>
    public static readonly IReadOnlyList<string> Passes = [First, Recheck];

    /// <summary>Another maker's agent read it (design §3.4).</summary>
    public const string AnotherMaker = "another-maker";

    /// <summary>The working session's own agent, fresh, which the person listed by name.</summary>
    public const string SameAgent = "same-agent";

    /// <summary>An agent whose maker nothing declares, never counted as another maker's.</summary>
    public const string MakerNotDeclared = "maker-not-declared";

    /// <summary>How independent a reviewer is, in the reviewer choice's words.</summary>
    public static readonly IReadOnlyList<string> Labels = [AnotherMaker, SameAgent, MakerNotDeclared];

    /// <summary>Read-only by its agent's rules and its copy (design §5.2).</summary>
    public const string RulesAndCopy = "rules-and-copy";

    /// <summary>Read-only by its copy alone.</summary>
    public const string CopyAlone = "copy-alone";

    /// <summary>What may have held a reviewer read-only.</summary>
    public static readonly IReadOnlyList<string> Postures = [RulesAndCopy, CopyAlone];

    /// <summary>Wrong to land as it is (design §6.1).</summary>
    public const string Must = "must";

    /// <summary>How much a finding weighs.</summary>
    public static readonly IReadOnlyList<string> Weights = [Must, "should", "note"];

    /// <summary>How sure a reviewer is of a finding.</summary>
    public static readonly IReadOnlyList<string> Sureness = ["sure", "likely", "unsure"];

    /// <summary>A first-pass finding the recheck still holds.</summary>
    public const string Stands = "stands";

    /// <summary>A first-pass finding the recheck withdraws after the session's answer and commits.</summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>What a recheck says of a first-pass finding.</summary>
    public static readonly IReadOnlyList<string> Rechecks = [Stands, Withdrawn];

    /// <summary>The working session fixed it, with a commit.</summary>
    public const string Fixed = "fixed";

    /// <summary>The working session rejects it, with evidence.</summary>
    public const string Rejected = "rejected";

    /// <summary>The working session cannot tell, and says why; also what an unanswered finding reads as.</summary>
    public const string Unresolved = "unresolved";

    /// <summary>How the working session may answer a finding.</summary>
    public static readonly IReadOnlyList<string> Answers = [Fixed, Rejected, Unresolved];

    /// <summary>The tier of a pass a reviewer's session reads (design §10).</summary>
    public const string Agent = "agent";

    /// <summary>A pass still being read.</summary>
    public const string Reading = "reading";

    /// <summary>A pass whose reviewer said its opinion.</summary>
    public const string Given = "given";

    /// <summary>A pass that ended without an opinion, never an empty one (design §6.1).</summary>
    public const string Failed = "failed";

    /// <summary>Its session ended without saying one.</summary>
    public const string Ended = "ended";

    /// <summary>Its minutes ran out first.</summary>
    public const string OutOfTime = "out-of-time";

    /// <summary>The rule's fewest minutes for one pass, as its twins hold it (XAGENT1a).</summary>
    public const int FewestMinutes = 5;

    /// <summary>The rule's most minutes for one pass.</summary>
    public const int MostMinutes = 120;

    /// <summary>The rule's bound where it names none.</summary>
    public const int DefaultMinutes = 20;

    /// <summary>The most findings one pass gives.</summary>
    public const int MostFindings = 20;

    /// <summary>The longest a claim may be, and a consequence.</summary>
    public const int ClaimLimit = 300;

    /// <summary>The longest a reproduction or its reasoning may be, and what was read, its limits and a rejection's evidence.</summary>
    public const int ReproduceLimit = 600;

    /// <summary>The longest a proposal may be.</summary>
    public const int ProposalLimit = 2000;

    /// <summary>The longest an unresolved answer's reason may be.</summary>
    public const int WhyLimit = 300;

    /// <summary>The longest a <c>where</c> may be.</summary>
    public const int WhereLimit = 300;

    /// <summary>The most commits one candidate names.</summary>
    public const int MostCommits = 1000;

    /// <summary>The most families a candidate names.</summary>
    public const int MostFamilies = 16;

    /// <summary>The longest an adapter's, a product's, a maker's, an account's or a family's name may be.</summary>
    public const int NameLimit = 128;

    /// <summary>
    /// Why a shared host refuses every door to an opinion (design §6.2): it is kept on the machine whose commits it read.
    /// </summary>
    public const string SharedSentence =
        "A second opinion is kept on the machine whose work it read, beside its session records, and answered only there. "
        + "This host is shared, so it keeps none, and nothing was kept.";

    private static readonly Regex Hex = new(@"^[0-9a-fA-F]{7,64}\z", RegexOptions.CultureInvariant);

    private static readonly Regex Lines = new(@"^(?<path>.+):(?<first>[1-9][0-9]{0,8})(?:-(?<last>[1-9][0-9]{0,8}))?\z", RegexOptions.CultureInvariant);

    /// <summary>Whether <paramref name="commit"/> reads as a commit an agent named: 7 to 64 hex characters.</summary>
    public static bool IsCommit(string? commit) => commit is not null && Hex.IsMatch(commit);

    /// <summary>
    /// Why <paramref name="where"/> is not a finding's place, or null when it is: <c>general</c>; a commit; or a path from the
    /// repository's root, judged as an evidence path is, with <c>:line</c> or <c>:first-last</c> where it is one place.
    /// </summary>
    public static string? JudgeWhere(string where)
    {
        if (where.Length == 0) return "`where` names the place: a path from the repository's root, a commit, or `general`";
        if (where.Length > WhereLimit) return $"`where` is at most {WhereLimit} characters, and this is {where.Length}";
        if (where == "general" || IsCommit(where)) return null;

        var path = where;
        if (Lines.Match(where) is { Success: true } lines)
        {
            path = lines.Groups["path"].Value;
            if (lines.Groups["last"].Success
                && long.Parse(lines.Groups["last"].Value, CultureInfo.InvariantCulture) < long.Parse(lines.Groups["first"].Value, CultureInfo.InvariantCulture))
            {
                return $"`{Clip(where)}` names a range that ends before it starts";
            }
        }

        return QuestEvidence.JudgePath(path) is { } unfit
            ? $"`{Clip(where)}` is not a place in the repository: {unfit}. Name a path from its root, a commit, or `general`"
            : null;
    }

    /// <summary>
    /// Why <paramref name="finding"/> is not one in shape, or null when it is (design §6.1): its weight, place, claim,
    /// consequence, reproduction and sureness each said and within bounds, and a proposal within its own.
    /// </summary>
    public static string? JudgeFinding(OpinionFinding finding)
    {
        if (!Weights.Contains(finding.Weight)) return $"`weight` is `must`, `should` or `note`, and `{Clip(finding.Weight)}` is none of them";
        if (JudgeWhere(finding.Where) is { } where) return where;
        if (Said(finding.Claim, "claim", ClaimLimit) is { } claim) return claim;
        if (Said(finding.Consequence, "consequence", ClaimLimit) is { } consequence) return consequence;
        if (Said(finding.Reproduce, "reproduce", ReproduceLimit) is { } reproduce) return reproduce;
        if (!Sureness.Contains(finding.Sure)) return $"`sure` is `sure`, `likely` or `unsure`, and `{Clip(finding.Sure)}` is none of them";
        if (finding.Proposal is { Length: > ProposalLimit } proposal) return $"`proposal` is at most {ProposalLimit} characters, and this is {proposal.Length}";
        return null;
    }

    /// <summary>
    /// Why <paramref name="answer"/> is not one in shape, or null when it is (design §6.4): <c>fixed</c> names its commit,
    /// <c>rejected</c> its evidence and <c>unresolved</c> why, each within its bound, and none carries another's part.
    /// </summary>
    public static string? JudgeAnswer(OpinionAnswer answer) => answer.Said switch
    {
        Fixed when !IsCommit(answer.Commit) =>
            $"a `fixed` names the commit that fixes it, 7 to 64 hex characters, and `{Clip(answer.Commit ?? "")}` is not one",
        Rejected when Said(answer.Evidence ?? "", "evidence", ReproduceLimit) is { } evidence => $"a `rejected` says its evidence: {evidence}",
        Unresolved when Said(answer.Why ?? "", "why", WhyLimit) is { } why => $"an `unresolved` says why: {why}",
        Fixed or Rejected or Unresolved when (answer.Said != Fixed && answer.Commit is not null)
                                          || (answer.Said != Rejected && answer.Evidence is not null)
                                          || (answer.Said != Unresolved && answer.Why is not null) =>
            $"a `{answer.Said}` carries only its own part: `commit` for fixed, `evidence` for rejected, `why` for unresolved",
        Fixed or Rejected or Unresolved => null,
        _ => $"an answer is `fixed`, `rejected` or `unresolved`, and `{Clip(answer.Said)}` is none of them",
    };

    /// <summary>
    /// Why what a pass asks is not one in shape, or null when it is: its occasion, pass, candidate, reviewer, families, posture,
    /// minutes and tree, each judged; a recheck names its first pass and a first pass names none. The bounds that read the
    /// record (one pass, one recheck) are the desk's.
    /// </summary>
    public static string? JudgeAsk(OpinionAsk ask)
    {
        if (!Occasions.Contains(ask.Occasion ?? "")) return $"`occasion` is `landing`, `steps`, `failure` or `asked`, and `{Clip(ask.Occasion ?? "")}` is none of them";
        if (!Passes.Contains(ask.Pass ?? "")) return $"`pass` is `first` or `recheck`, and `{Clip(ask.Pass ?? "")}` is neither";
        if (ask.Pass == Recheck && string.IsNullOrWhiteSpace(ask.Rechecks)) return "a recheck names the first pass whose findings it reads again (`rechecks`)";
        if (ask.Pass == First && ask.Rechecks is not null) return "a first pass rechecks nothing: `rechecks` is a recheck's";
        if (Name(ask.Working) is not null) return "`working` names the session whose work is read, by its id";
        if (ask.Candidate is not { } candidate || string.IsNullOrWhiteSpace(candidate.Repository)) return "the candidate names its repository";
        if (!QuestEvidenceCodes.IsObjectId(candidate.Base) || !QuestEvidenceCodes.IsObjectId(candidate.Tip))
        {
            return "the candidate names its `base` and its `tip` by their full ids (40 or 64 hex characters)";
        }

        if (candidate.Commits is null || candidate.Commits.Count is 0 or > MostCommits || !candidate.Commits.All(QuestEvidenceCodes.IsObjectId))
        {
            return $"the candidate names the commits it holds, 1 to {MostCommits}, each by its full id";
        }

        if (string.Equals(candidate.Base, candidate.Tip, StringComparison.OrdinalIgnoreCase)) return "the candidate's tip is its base: there is no work between them to read";
        if (ask.Reviewer is not { } reviewer || Name(reviewer.Adapter) is not null) return "the reviewer names its adapter";
        if (!Labels.Contains(reviewer.Label ?? "")) return $"the reviewer's `label` is `another-maker`, `same-agent` or `maker-not-declared`, and `{Clip(reviewer.Label ?? "")}` is none of them";
        foreach (var (named, field) in new[] { (reviewer.Product, "product"), (reviewer.Maker, "maker"), (reviewer.Account, "account") })
        {
            if (named is not null && Name(named) is { } unfit) return $"the reviewer's `{field}` {unfit}";
        }

        if (ask.Families is null || ask.Families.Count > MostFamilies || ask.Families.Any(family => Name(family) is not null))
        {
            return $"`families` names at most {MostFamilies} families that wrote the candidate, each a name";
        }

        if (!Postures.Contains(ask.Posture ?? "")) return $"`posture` is `rules-and-copy` or `copy-alone`, and `{Clip(ask.Posture ?? "")}` is neither";
        if (ask.Minutes is < FewestMinutes or > MostMinutes) return $"`minutes` is the rule's bound of one pass, a whole number from {FewestMinutes} to {MostMinutes}";
        if (string.IsNullOrWhiteSpace(ask.Tree)) return "`tree` names the reviewer's own tree: a clone at the candidate, never the working session's";
        return null;
    }

    /// <summary>The answer standing for <paramref name="finding"/>: the latest said, or null where none was.</summary>
    public static OpinionAnswer? AnswerTo(Opinion opinion, int finding) =>
        opinion.Answers.LastOrDefault(answer => answer.Finding == finding);

    /// <summary>
    /// The turn Daoris says to the working session with the findings (design §6.3): its fixed words, then each finding whole,
    /// then what the reviewer read and its limits. Another agent's claims, marked as such, never the person's words.
    /// </summary>
    public static string Words(Opinion opinion)
    {
        var reviewer = opinion.Reviewer;
        var who = (reviewer.Product ?? reviewer.Adapter) + (reviewer.Maker is { } maker ? $" by {maker}" : ", whose maker is not declared");
        var text = new StringBuilder()
            .Append($"Another agent, {who}, read your work at `{opinion.Candidate.Tip}` and claims what follows. These are its claims, ")
            .Append("not the person's words and not facts. Check each one against the code, and reproduce it where it says how. If it ")
            .Append("holds, fix it here and commit. If it does not, reject it with your evidence. If you cannot tell, say so. Answer every ")
            .Append($"finding with `opinion_answer` (opinion `{opinion.Id}`) before you end. The other agent has ended; you cannot ask it ")
            .Append("anything. The person sees your answers beside its claims.");

        foreach (var finding in opinion.Given?.Findings ?? [])
        {
            text.Append($"\n\nFinding {finding.Number} ({finding.Weight}, {finding.Sure}), at `{finding.Where}`: {finding.Claim}")
                .Append($"\nWhy it matters: {finding.Consequence}")
                .Append($"\nHow to see it: {finding.Reproduce}");
            if (finding.Proposal is { } proposal) text.Append($"\nIt proposes: {proposal}");
        }

        if (opinion.Given is { } given)
        {
            text.Append($"\n\nWhat it read: {given.Read}");
            if (given.Limits is { } limits) text.Append($"\nWhat it did not read or could not tell: {limits}");
        }

        return text.ToString();
    }

    /// <summary>What an opinion asked, in the store's column: everything but its identity, its pass and when, which are columns.</summary>
    internal static string AskedJson(Opinion opinion) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartObject("candidate");
        writer.WriteString("repository", opinion.Candidate.Repository);
        writer.WriteString("base", opinion.Candidate.Base);
        writer.WriteString("tip", opinion.Candidate.Tip);
        writer.WriteStartArray("commits");
        foreach (var commit in opinion.Candidate.Commits) writer.WriteStringValue(commit);
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteStartObject("reviewer");
        writer.WriteString("adapter", opinion.Reviewer.Adapter);
        writer.WriteString("label", opinion.Reviewer.Label);
        if (opinion.Reviewer.Product is not null) writer.WriteString("product", opinion.Reviewer.Product);
        if (opinion.Reviewer.Maker is not null) writer.WriteString("maker", opinion.Reviewer.Maker);
        if (opinion.Reviewer.Account is not null) writer.WriteString("account", opinion.Reviewer.Account);
        writer.WriteEndObject();
        writer.WriteStartArray("families");
        foreach (var family in opinion.Families) writer.WriteStringValue(family);
        writer.WriteEndArray();
        if (opinion.Posture is not null) writer.WriteString("posture", opinion.Posture);
        writer.WriteNumber("minutes", opinion.Minutes);
        writer.WriteString("tier", opinion.Tier);
        writer.WriteEndObject();
    });

    /// <summary>
    /// An opinion as stored, from its columns and its asked JSON; null when what it asked cannot be read, since an opinion
    /// with no candidate or no reviewer says nothing anyone can act on. A field a later build added is ignored.
    /// </summary>
    internal static Opinion? Read(
        string id, string occasion, string pass, string working, string session, string? rechecks, DateTimeOffset asked, string askedJson,
        string? givenJson, OpinionHanded? handed, string? answersJson)
    {
        if (ParseObject(askedJson) is not { } root
            || !root.TryGetProperty("candidate", out var candidate) || candidate.ValueKind != JsonValueKind.Object
            || Text(candidate, "repository") is not { } repository || Text(candidate, "base") is not { } baseCommit
            || Text(candidate, "tip") is not { } tip
            || !root.TryGetProperty("reviewer", out var reviewer) || reviewer.ValueKind != JsonValueKind.Object
            || Text(reviewer, "adapter") is not { } adapter || Text(reviewer, "label") is not { } label)
        {
            return null;
        }

        return new Opinion(
            id, occasion, pass, working, session,
            new OpinionCandidate(repository, baseCommit, tip, Strings(candidate, "commits")),
            new OpinionReviewer(adapter, label)
            {
                Product = Text(reviewer, "product"), Maker = Text(reviewer, "maker"), Account = Text(reviewer, "account"),
            },
            asked)
        {
            Rechecks = rechecks,
            Families = Strings(root, "families"),
            Posture = Text(root, "posture"),
            Minutes = Number(root, "minutes") is { } minutes and >= 0 and <= int.MaxValue ? (int)minutes : DefaultMinutes,
            Tier = Text(root, "tier") ?? Agent,
            Given = givenJson is null ? null : GivenOf(givenJson),
            Handed = handed,
            Answers = answersJson is null ? [] : AnswersOf(answersJson),
        };
    }

    /// <summary>What the reviewer said, in the store's column.</summary>
    internal static string GivenJson(OpinionGiven given) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("findings");
        foreach (var finding in given.Findings)
        {
            writer.WriteStartObject();
            writer.WriteNumber("number", finding.Number);
            writer.WriteString("weight", finding.Weight);
            writer.WriteString("where", finding.Where);
            writer.WriteString("claim", finding.Claim);
            writer.WriteString("consequence", finding.Consequence);
            writer.WriteString("reproduce", finding.Reproduce);
            writer.WriteString("sure", finding.Sure);
            if (finding.Proposal is not null) writer.WriteString("proposal", finding.Proposal);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteString("read", given.Read);
        if (given.Limits is not null) writer.WriteString("limits", given.Limits);
        if (given.Rechecked.Count > 0)
        {
            writer.WriteStartArray("rechecked");
            foreach (var recheck in given.Rechecked)
            {
                writer.WriteStartObject();
                writer.WriteNumber("finding", recheck.Finding);
                writer.WriteString("says", recheck.Says);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteString("at", given.At.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteEndObject();
    });

    /// <summary>The answers, in the store's column, in the order kept.</summary>
    internal static string AnswersJson(IEnumerable<OpinionAnswer> answers) => Written(writer =>
    {
        writer.WriteStartArray();
        foreach (var answer in answers)
        {
            writer.WriteStartObject();
            writer.WriteNumber("finding", answer.Finding);
            writer.WriteString("said", answer.Said);
            if (answer.Commit is not null) writer.WriteString("commit", answer.Commit);
            if (answer.Evidence is not null) writer.WriteString("evidence", answer.Evidence);
            if (answer.Why is not null) writer.WriteString("why", answer.Why);
            writer.WriteString("at", answer.At.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    /// <summary>
    /// What the reviewer said, as stored: a finding missing a part, or one this build cannot read, is passed over, never a
    /// failed read of the opinion. Null when the column holds no opinion at all.
    /// </summary>
    private static OpinionGiven? GivenOf(string json)
    {
        if (ParseObject(json) is not { } root || Text(root, "read") is not { } read || Moment(root, "at") is not { } at) return null;

        var findings = new List<OpinionFinding>();
        foreach (var each in Items(root, "findings"))
        {
            if (Number(each, "number") is not { } number || number is < 1 or > MostFindings
                || Text(each, "weight") is not { } weight || Text(each, "where") is not { } where || Text(each, "claim") is not { } claim
                || Text(each, "consequence") is not { } consequence || Text(each, "reproduce") is not { } reproduce
                || Text(each, "sure") is not { } sure)
            {
                continue;
            }

            findings.Add(new OpinionFinding(weight, where, claim, consequence, reproduce, sure)
            {
                Number = (int)number, Proposal = Text(each, "proposal"),
            });
        }

        var rechecked = Items(root, "rechecked")
            .Where(each => Number(each, "finding") is > 0 and <= MostFindings && Text(each, "says") is { } says && Rechecks.Contains(says))
            .Select(each => new OpinionRecheck((int)Number(each, "finding")!.Value, Text(each, "says")!))
            .ToList();
        return new OpinionGiven(findings, read, at) { Limits = Text(root, "limits"), Rechecked = rechecked };
    }

    private static IReadOnlyList<OpinionAnswer> AnswersOf(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
            return document.RootElement.EnumerateArray()
                .Where(each => each.ValueKind == JsonValueKind.Object && Number(each, "finding") is > 0 and <= MostFindings
                               && Text(each, "said") is { } said && Answers.Contains(said) && Moment(each, "at") is not null)
                .Select(each => new OpinionAnswer((int)Number(each, "finding")!.Value, Text(each, "said")!, Moment(each, "at")!.Value)
                {
                    Commit = Text(each, "commit"), Evidence = Text(each, "evidence"), Why = Text(each, "why"),
                })
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        Items(element, name).Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToList();

    private static DateTimeOffset? Moment(JsonElement element, string name) =>
        Text(element, name) is { } when
        && DateTimeOffset.TryParse(when, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;

    /// <summary>Why <paramref name="words"/> is not said within <paramref name="limit"/>, or null when it is.</summary>
    private static string? Said(string words, string field, int limit) =>
        words.Trim().Length == 0 ? $"`{field}` is said, and it is empty"
        : words.Length > limit ? $"`{field}` is at most {limit} characters, and this is {words.Length}"
        : null;

    /// <summary>Why <paramref name="name"/> is not a name, or null when it is: said, at most <see cref="NameLimit"/>, one line.</summary>
    private static string? Name(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "is empty"
        : name.Length > NameLimit ? $"is longer than {NameLimit} characters"
        : name.Any(char.IsControl) ? "holds a control character"
        : null;

    internal static string Clip(string text) => text.Length <= 120 ? text : text[..120] + "…";
}
