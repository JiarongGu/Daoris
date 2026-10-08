using System.Text.Json;
using System.Text.RegularExpressions;
using static Daoris.Knowledge.JsonFields;

namespace Daoris.Knowledge;

/// <summary>
/// A chain's review choice (REVIEWENV1b, D154 point 3; the review environment design §1.4–§1.5): <c>off</c>, <c>on</c> (the
/// repository's default environment) or an environment's name, set at publish and inherited by each step. The person sets
/// it, with any words they give; an intake sets it only by quoting them, and the exchange checks the quote as it checks a
/// requirement's (DRIFT1c).
/// </summary>
/// <param name="Choice">One of <see cref="Reviews.Off"/>, <see cref="Reviews.On"/>, or an environment's name.</param>
/// <param name="Words">
/// The person's words it was set on: given at their own door, or quoted from what they said on the ask by the intake that
/// set it. Null where the person gave none.
/// </param>
public sealed record QuestReview(string Choice, string? Words = null);

/// <summary>
/// One set-up of a set-up step (REVIEWENV1b, D154 point 9; design §2.6, §3.5): where the work was shown, what it shows,
/// how to show it again, and the commit its tree held, which Daoris read and no session reports. Kept on the quest in a
/// <see cref="QuestOperationKind.SetUp"/> operation, which travels like every verb (D68).
/// </summary>
/// <param name="Commit">The commit the set-up holds, by its full id: the step's tree <c>HEAD</c> as the driver read it.</param>
public sealed record QuestSetUp(string Commit)
{
    /// <summary>
    /// Where to look: the address the step's tab is at, or a deployed environment's page. Null on another machine for a
    /// local set-up, whose tab is on the machine that showed it (<see cref="Local"/>), since it travels as <i>local</i>.
    /// </summary>
    public string? Look { get; init; }

    /// <summary>What it showed and what to look at, at most <see cref="Reviews.ShowsLimit"/> characters.</summary>
    public string? Shows { get; init; }

    /// <summary>How to show it again by hand: the address and the clicks, at most <see cref="Reviews.AgainLimit"/> characters.</summary>
    public string? Again { get; init; }

    /// <summary>The folder Daoris was asked to serve to the step's tab, as a path in the step's tree; null where none was.</summary>
    public string? Served { get; init; }

    /// <summary>A review run's command, quoted from the procedure; null where the work needed no process of its own.</summary>
    public string? Run { get; init; }

    /// <summary>The session that said it; null for the person's own set-up (<i>I set it up myself…</i>).</summary>
    public string? Session { get; init; }

    /// <summary>Whether it was shown in a local environment: its tab is on the machine that made it.</summary>
    public bool Local { get; init; }

    /// <summary>When it was said: the operation's time, set on the quest as the replay keeps it.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>The machine that made it: the operation's, set on the quest as the replay keeps it.</summary>
    public string? Machine { get; init; }

    /// <summary>The operation's sequence on <see cref="Machine"/>: with it, what a verdict names this set-up by.</summary>
    public long? Sequence { get; init; }

    /// <summary>The set-up's name on every machine, as a verdict names it; null on a set-up not yet kept on a quest.</summary>
    public QuestOperationRef? Ref => Machine is not null && Sequence is { } sequence ? new QuestOperationRef(Machine, sequence) : null;

    /// <summary>
    /// What it says, as one identity that crosses the wire with it (REVIEWENV1b3): a digest of everything it says, its local
    /// address included, made on the machine that said it (<see cref="Reviews.IdentityOf"/>). Null on a set-up a build from
    /// before kept, which is compared by what it says instead (<see cref="Same"/>).
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Whether it says what <paramref name="other"/> says, so a second post of one set-up is no move, whichever door or
    /// machine posts it again: the same identity where both carry one, and otherwise the same commit, words, folder, command,
    /// session and kind, with the address only where neither is local.
    /// </summary>
    /// <remarks>
    /// <b>Never a field the wire drops</b> (REVIEWENV1b3): a local set-up crosses without its address, so comparing it held a
    /// second set-up at another address here and refused it on the remote, leaving the step held on one machine and let go
    /// on the next. The identity carries the address across instead.
    /// </remarks>
    public bool Same(QuestSetUp other) =>
        Id is not null && other.Id is not null
            ? Id == other.Id
            : Commit == other.Commit && (Local || Look == other.Look) && Shows == other.Shows && Again == other.Again
              && Served == other.Served && Run == other.Run && Session == other.Session && Local == other.Local;
}

/// <summary>
/// The person's verdict on a set-up step (REVIEWENV1b, D154 points 8–9; design §3.3–§3.6): <c>reviewed</c> or
/// <c>not-yet</c> on its newest set-up, or <c>skipped</c> for this work. Kept on the quest in a
/// <see cref="QuestOperationKind.Verdict"/> operation; only a local host's door makes one, since the verdict is the person's.
/// </summary>
/// <param name="Said">One of <see cref="Reviews.Verdicts"/>.</param>
public sealed record QuestReviewVerdict(string Said)
{
    /// <summary>The set-up it answers, by its machine and sequence: a <c>reviewed</c>'s and a <c>not-yet</c>'s; null for a skip.</summary>
    public QuestOperationRef? SetUp { get; init; }

    /// <summary>The commit of the set-up it answers, as that set-up holds it: what the gate asks git about.</summary>
    public string? Commit { get; init; }

    /// <summary>The person's words: a <c>not-yet</c>'s always, what the step's session acts on; null where they gave none.</summary>
    public string? Words { get; init; }

    /// <summary>When it was given: the operation's time, set on the quest as the replay keeps it.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>The machine it was given on: the operation's, set on the quest as the replay keeps it.</summary>
    public string? Machine { get; init; }
}

/// <summary>
/// Where a set-up step's review stands (REVIEWENV1b, D154 points 7–9; design §3.2, §3.5), read from its set-ups and verdicts:
/// one reading for the replay, the hold and every door.
/// </summary>
public static class QuestReviewing
{
    /// <summary>The newest set-up said on the quest, the one a verdict answers; null while none was said.</summary>
    public static QuestSetUp? Newest(Quest quest) => quest.SetUps.Count == 0 ? null : quest.SetUps[^1];

    /// <summary>Whether the person skipped the review of this work.</summary>
    public static bool Skipped(Quest quest) => quest.Verdicts.Any(verdict => verdict.Said == Reviews.Skipped);

    /// <summary>
    /// Whether the person said <c>reviewed</c> of the newest set-up. A <c>reviewed</c> of an older one does not stand for a
    /// newer: that work is work they have not seen run.
    /// </summary>
    public static bool Reviewed(Quest quest) =>
        Newest(quest)?.Ref is { } newest
        && quest.Verdicts.Any(verdict => verdict.Said == Reviews.Reviewed && verdict.SetUp == newest);

    /// <summary>Whether a set-up step's done waits for the person's review: closed done, neither reviewed nor skipped.</summary>
    public static bool Waits(Quest quest) =>
        quest is { SetUpIn: not null, Status: QuestStatus.Done } && !Skipped(quest) && !Reviewed(quest);

    /// <summary>
    /// Whether <paramref name="verdict"/> moves the review of <paramref name="quest"/>: a skip once, and on a set-up step only
    /// before its review; a <c>reviewed</c> or a <c>not-yet</c> only of the newest set-up, at its commit, and only before a
    /// <c>reviewed</c> or a skip let it go. So two machines' <c>reviewed</c> are one, and a verdict on a set-up a newer one
    /// stands beside is no move.
    /// </summary>
    public static bool Takes(Quest quest, QuestReviewVerdict verdict) => verdict.Said switch
    {
        Reviews.Skipped => !Skipped(quest) && !(quest.SetUpIn is not null && Reviewed(quest)),
        Reviews.Reviewed or Reviews.NotYet =>
            quest.SetUpIn is not null && !Skipped(quest) && !Reviewed(quest)
            && Newest(quest) is { } newest && newest.Ref == verdict.SetUp && newest.Commit == verdict.Commit,
        _ => false,
    };
}

/// <summary>
/// The shapes and bounds of a review on the record (REVIEWENV1b): a choice, an environment's name, a set-up and a verdict,
/// each judged here once for every door, the wire and the store's own column.
/// </summary>
public static class Reviews
{
    /// <summary>No review for this work.</summary>
    public const string Off = "off";

    /// <summary>Review in the repository's default environment, the first its rule names.</summary>
    public const string On = "on";

    /// <summary>The person looked at the set-up and says it is right.</summary>
    public const string Reviewed = "reviewed";

    /// <summary>The person looked and it is not right yet: their words go back to the step's session.</summary>
    public const string NotYet = "not-yet";

    /// <summary>The person lets the work land without a review, for this work alone.</summary>
    public const string Skipped = "skipped";

    /// <summary>What a verdict may say.</summary>
    public static readonly IReadOnlyList<string> Verdicts = [Reviewed, NotYet, Skipped];

    /// <summary>How a set-up was shown: in a local environment, or in a deployed one.</summary>
    public static readonly IReadOnlyList<string> Kinds = ["local", "deployed"];

    /// <summary>The longest an environment's name may be, as the review rule's twins hold it (REVIEWENV1a).</summary>
    public const int NameLimit = 32;

    /// <summary>The longest what a set-up shows may be.</summary>
    public const int ShowsLimit = 300;

    /// <summary>The longest how to show a set-up again may be.</summary>
    public const int AgainLimit = 600;

    /// <summary>The longest an intake's reason for a proposal may be.</summary>
    public const int ReasonLimit = 300;

    /// <summary>The longest an address may be, a link's bound (D65 §2).</summary>
    public const int LookLimit = 2048;

    /// <summary>The longest a review run's command may be.</summary>
    public const int RunLimit = 2000;

    /// <summary>The longest the person's words on a choice or a verdict may be, as an answer's are (DRIFT1a, KNOWUSE1a).</summary>
    public const int WordsLimit = 2000;

    private static readonly Regex NameShape = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);

    private static readonly Regex IdShape = new(@"^[0-9a-f]{32}\z", RegexOptions.CultureInvariant);

    /// <summary>
    /// A set-up's identity (REVIEWENV1b3): the first 32 hex characters of the SHA-256 of what it says, in its payload's own
    /// shape, its local address included and when, where and which operation left out. The same set-up posted again has the
    /// same identity on every door and machine; one said at another address has another.
    /// </summary>
    public static string IdentityOf(QuestSetUp setUp)
    {
        var said = Written(writer => Write(writer, setUp with { Id = null, At = null, Machine = null, Sequence = null }));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(said)))[..32]
            .ToLowerInvariant();
    }

    /// <summary>
    /// Why <paramref name="name"/> is not an environment's name, or null when it is (design §1.1, §1.3): lower-case letters,
    /// digits and dashes, at most <see cref="NameLimit"/>, never <c>none</c>, <c>off</c> or <c>on</c>, and never one that
    /// reads as production by the place words a go-ahead is read by (<see cref="GoAheadAct.ReadsAsProduction"/>).
    /// </summary>
    public static string? JudgeEnvironment(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "an environment is named: lower-case letters, digits and dashes, such as `local` or `dev`";
        if (!NameShape.IsMatch(name) || name.Length > NameLimit)
        {
            return $"`{Clip(name)}` is not an environment's name: lower-case letters, digits and dashes, at most {NameLimit} characters, "
                   + "such as `local` or `dev`";
        }

        if (name is "none" or Off or On) return $"`{name}` names no environment: it is a choice's word, never an environment's name";
        if (GoAheadAct.ReadsAsProduction(name))
        {
            return $"`{name}` reads as production, and production is never a review environment: work is looked at before it lands "
                   + "there, such as in `local` or `dev`";
        }

        return null;
    }

    /// <summary>Why <paramref name="choice"/> is not a review choice, or null when it is: <c>off</c>, <c>on</c>, or an environment's name.</summary>
    public static string? JudgeChoice(string? choice) =>
        choice is Off or On ? null
        : JudgeEnvironment(choice) is { } unfit ? $"a review choice is `off`, `on` or an environment's name, and {unfit}"
        : null;

    /// <summary>Whether <paramref name="address"/> is an absolute <c>http</c> or <c>https</c> address of at most <see cref="LookLimit"/>.</summary>
    public static bool IsAddress(string? address) =>
        address is { Length: > 0 and <= LookLimit }
        && Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Why <paramref name="setUp"/> is not one in shape, or null when it is: a full commit id, an address to look at unless it
    /// is a local set-up seen from another machine, words within their bounds, a folder that is a path in the tree, one
    /// command on one line, and a session's id. The exchange calls it before it keeps one, and the wire before it reads one.
    /// </summary>
    public static string? JudgeSetUp(QuestSetUp setUp)
    {
        if (!QuestEvidenceCodes.IsObjectId(setUp.Commit)) return $"a set-up names its commit by its full id (40 or 64 hex characters), and `{Clip(setUp.Commit ?? "")}` is not one";
        if (setUp.Look is null && !setUp.Local) return "a set-up names where to look: the address its tab is at, or the environment's page";
        if (setUp.Look is { } look && !IsAddress(look)) return $"`{Clip(look)}` is not where to look: an absolute http or https address of at most {LookLimit} characters";
        if (setUp.Shows is { Length: > ShowsLimit } shows) return $"what a set-up shows is at most {ShowsLimit} characters, and this is {shows.Length}";
        if (setUp.Again is { Length: > AgainLimit } again) return $"how to show a set-up again is at most {AgainLimit} characters, and this is {again.Length}";
        if (setUp.Served is { } served && QuestEvidence.JudgePath(served) is { } unfit) return $"the folder served, `{Clip(served)}`, is not a path in the tree: {unfit}";
        if (setUp.Run is { } run && (run.Trim().Length == 0 || run.Length > RunLimit || run.Contains('\r') || run.Contains('\n')))
        {
            return $"a review run's command is one command on one line, of at most {RunLimit} characters";
        }

        if (setUp.Session is { } session && !QuestEvidenceCodes.IsSessionId(session)) return $"`{Clip(session)}` is not a session's id";
        if (setUp.Id is { } id && !IdShape.IsMatch(id)) return $"`{Clip(id)}` is not a set-up's identity: 32 lower-case hex characters";
        return null;
    }

    /// <summary>
    /// Why <paramref name="verdict"/> is not one in shape, or null when it is: one of <see cref="Verdicts"/>; a <c>reviewed</c>
    /// and a <c>not-yet</c> name the set-up they answer and its commit, a skip names none; a <c>not-yet</c> carries the
    /// person's words; and words are within their bound.
    /// </summary>
    public static string? JudgeVerdict(QuestReviewVerdict verdict)
    {
        if (!Verdicts.Contains(verdict.Said ?? "")) return $"a verdict says `{Reviewed}`, `{NotYet}` or `{Skipped}`, and `{Clip(verdict.Said ?? "")}` is none of them";
        if (verdict.Said == Skipped)
        {
            if (verdict.SetUp is not null || verdict.Commit is not null) return "a skip answers no set-up: it lets the work land without one";
        }
        else if (verdict.SetUp is not { Machine.Length: > 0 } || !QuestEvidenceCodes.IsObjectId(verdict.Commit))
        {
            return $"a `{verdict.Said}` names the set-up it answers and that set-up's commit";
        }

        if (verdict.Said == NotYet && string.IsNullOrWhiteSpace(verdict.Words)) return "a `not-yet` says what is not right yet, in the person's words";
        if (verdict.Words is { Length: > WordsLimit } words) return $"the person's words are at most {WordsLimit} characters, and these are {words.Length}";
        return null;
    }

    /// <summary>A choice's one shape in the log's payload, the quest's column and on the wire.</summary>
    internal static void Write(Utf8JsonWriter writer, QuestReview review)
    {
        writer.WriteStartObject();
        writer.WriteString("choice", review.Choice);
        if (review.Words is not null) writer.WriteString("words", review.Words);
        writer.WriteEndObject();
    }

    /// <summary>A choice read from JSON, judged as the wire judges what it reads: null when it is not one.</summary>
    internal static QuestReview? JudgedReview(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || Text(element, "choice") is not { } choice || JudgeChoice(choice) is not null) return null;
        var words = Text(element, "words");
        if (element.TryGetProperty("words", out _) && words is null) return null;
        return words is { Length: > WordsLimit } ? null : new QuestReview(choice, words);
    }

    /// <summary>
    /// A set-up's one shape in the log's payload and on the wire, and, <paramref name="standing"/>, in the quest's column,
    /// which alone keeps when, where and which operation, those being the operation's own. On the wire
    /// (<paramref name="crossing"/>) a local set-up leaves its address behind: its tab is on this machine (design §3.5).
    /// </summary>
    internal static void Write(Utf8JsonWriter writer, QuestSetUp setUp, bool standing = false, bool crossing = false)
    {
        writer.WriteStartObject();
        writer.WriteString("commit", setUp.Commit);
        // Its identity crosses whole, the local address it was made from included (REVIEWENV1b3); absent on one from before.
        if (setUp.Id is not null) writer.WriteString("id", setUp.Id);
        if (setUp.Look is not null && !(crossing && setUp.Local)) writer.WriteString("look", setUp.Look);
        if (setUp.Shows is not null) writer.WriteString("shows", setUp.Shows);
        if (setUp.Again is not null) writer.WriteString("again", setUp.Again);
        if (setUp.Served is not null) writer.WriteString("served", setUp.Served);
        if (setUp.Run is not null) writer.WriteString("run", setUp.Run);
        if (setUp.Session is not null) writer.WriteString("session", setUp.Session);
        if (setUp.Local) writer.WriteBoolean("local", true);
        if (standing && setUp.At is { } at) writer.WriteString("at", at.ToString("O"));
        if (standing && setUp.Machine is not null) writer.WriteString("machine", setUp.Machine);
        if (standing && setUp.Sequence is { } sequence) writer.WriteNumber("sequence", sequence);
        writer.WriteEndObject();
    }

    /// <summary>
    /// A set-up read from JSON, judged by <see cref="JudgeSetUp"/>: null when it is not whole. A field of the wrong kind is
    /// not one, never read as absent, since a set-up half-read would be shown as something nobody said.
    /// </summary>
    internal static QuestSetUp? JudgedSetUp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || Text(element, "commit") is not { } commit) return null;
        foreach (var name in new[] { "id", "look", "shows", "again", "served", "run", "session", "machine", "at" })
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.String) return null;
        }

        var local = false;
        if (element.TryGetProperty("local", out var flag))
        {
            if (flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
            local = flag.ValueKind == JsonValueKind.True;
        }

        var setUp = new QuestSetUp(commit)
        {
            Id = Text(element, "id"),
            Look = Text(element, "look"),
            Shows = Text(element, "shows"),
            Again = Text(element, "again"),
            Served = Text(element, "served"),
            Run = Text(element, "run"),
            Session = Text(element, "session"),
            Local = local,
            At = Moment(element, "at"),
            Machine = Text(element, "machine"),
            Sequence = Number(element, "sequence"),
        };
        return JudgeSetUp(setUp) is null ? setUp with { Commit = commit.ToLowerInvariant() } : null;
    }

    /// <summary>
    /// A verdict's one shape in the log's payload and on the wire, and, <paramref name="standing"/>, in the quest's column,
    /// which alone keeps when and where it was given.
    /// </summary>
    internal static void Write(Utf8JsonWriter writer, QuestReviewVerdict verdict, bool standing = false)
    {
        writer.WriteStartObject();
        writer.WriteString("said", verdict.Said);
        if (verdict.SetUp is { } setUp)
        {
            writer.WriteStartObject("setUp");
            writer.WriteString("machine", setUp.Machine);
            writer.WriteNumber("sequence", setUp.Sequence);
            writer.WriteEndObject();
        }

        if (verdict.Commit is not null) writer.WriteString("commit", verdict.Commit);
        if (verdict.Words is not null) writer.WriteString("words", verdict.Words);
        if (standing && verdict.At is { } at) writer.WriteString("at", at.ToString("O"));
        if (standing && verdict.Machine is not null) writer.WriteString("machine", verdict.Machine);
        writer.WriteEndObject();
    }

    /// <summary>A verdict read from JSON, judged by <see cref="JudgeVerdict"/>: null when it is not whole.</summary>
    internal static QuestReviewVerdict? JudgedVerdict(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || Text(element, "said") is not { } said) return null;
        foreach (var name in new[] { "commit", "words", "machine", "at" })
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.String) return null;
        }

        QuestOperationRef? setUp = null;
        if (element.TryGetProperty("setUp", out var named))
        {
            if (named.ValueKind != JsonValueKind.Object || Text(named, "machine") is not { Length: > 0 } machine
                || Number(named, "sequence") is not { } sequence)
            {
                return null;
            }

            setUp = new QuestOperationRef(machine, sequence);
        }

        var verdict = new QuestReviewVerdict(said)
        {
            SetUp = setUp,
            Commit = Text(element, "commit")?.ToLowerInvariant(),
            Words = Text(element, "words"),
            At = Moment(element, "at"),
            Machine = Text(element, "machine"),
        };
        return JudgeVerdict(verdict) is null ? verdict : null;
    }

    private static DateTimeOffset? Moment(JsonElement element, string name) =>
        Text(element, name) is { } when
        && DateTimeOffset.TryParse(when, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;

    private static string Clip(string text) => text.Length <= 120 ? text : text[..120] + "…";
}
