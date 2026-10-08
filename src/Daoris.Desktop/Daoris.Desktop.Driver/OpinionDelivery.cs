using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One finding as the driver read its answer when the working session's turn ended (XAGENT1e, D155 point 7; design §6.4): what
/// the session said, and what counts. A fix counts only where its commit is one the turn made, read from git; a finding nobody
/// answered counts as <c>unresolved</c>, said as not answered.
/// </summary>
/// <param name="Counts"><c>fixed</c> (its commit checked), <c>rejected</c>, or <c>unresolved</c>.</param>
public sealed record OpinionAnswerRead(int Finding, string Weight, string Counts)
{
    /// <summary>What the session said: <c>fixed</c>, <c>rejected</c> or <c>unresolved</c>; null where it said nothing.</summary>
    public string? Said { get; init; }

    /// <summary>A fix's commit, as the session named it.</summary>
    public string? Commit { get; init; }

    /// <summary>A fix's commit by its full id, where git read it as one the turn made; null otherwise.</summary>
    public string? Fix { get; init; }

    /// <summary>Why it does not count as said, a code of <see cref="OpinionAnswerWhy"/>; null where it counts as said.</summary>
    public string? Why { get; init; }

    /// <summary>
    /// A <c>must</c> the working session did not fix (design §6.6): the person's to answer. A recheck may withdraw it
    /// (<see cref="OpinionDisputes"/>).
    /// </summary>
    public bool Disputed => Weight == OpinionViews.Must && Counts != OpinionViews.Fixed;
}

/// <summary>
/// The working session's answers as the driver read them when its turn ended (XAGENT1e; design §6.4): one row per finding, the
/// tree's tip they were read at, and when. The facts a recheck and the gate (XAGENT1f) read; the answers themselves stay the
/// local host's.
/// </summary>
/// <param name="Tip">The working tree's tip as the turn ended, by its full id; null where git could not read it.</param>
public sealed record OpinionReading(string Session, string? Tip, DateTimeOffset At, IReadOnlyList<OpinionAnswerRead> Findings)
{
    /// <summary>Whether any finding counts as fixed: what a recheck reads (design §6.5).</summary>
    public bool AnyFixed => Findings.Any(finding => finding.Counts == OpinionViews.Fixed);
}

/// <summary>Why an answer does not count as said (XAGENT1e; design §6.4), each with the line the driver says.</summary>
public static class OpinionAnswerWhy
{
    /// <summary>The working session's turn ended and it gave this finding no answer: unresolved.</summary>
    public const string NotAnswered = "not-answered";

    /// <summary>The fix named a commit its tree does not hold.</summary>
    public const string FixUnknown = "fix-unknown";

    /// <summary>The fix named a commit that is not after the work the other agent read.</summary>
    public const string FixNotAfter = "fix-not-after";

    /// <summary>The fix named a commit that is not on the working session's branch, at or before its tip.</summary>
    public const string FixOffBranch = "fix-off-branch";

    /// <summary>git could not say: its tree is gone, or git did not answer.</summary>
    public const string FixUnread = "fix-unread";

    /// <summary>The line for <paramref name="code"/>, in the driver's English.</summary>
    public static string Said(string code) => code switch
    {
        NotAnswered => "not answered",
        FixUnknown => "fixed, its commit said, but its tree holds no commit by that name",
        FixNotAfter => "fixed, its commit said, but that commit is not after the work the other agent read",
        FixOffBranch => "fixed, its commit said, but that commit is not on the session's branch",
        FixUnread => "fixed, its commit said, but git could not read its tree to check it",
        _ => code,
    };
}

/// <summary>Where an opinion stands as the driver delivers it (XAGENT1e; design §6.3–§6.7, §8.5), one of these words.</summary>
public static class OpinionDeliveryStates
{
    /// <summary>The reviewer is still reading.</summary>
    public const string Reading = "reading";

    /// <summary>The pass ended without an opinion, or ran out of time: <i>Try again</i>, with the reason. Never <i>no issues</i>.</summary>
    public const string TryAgain = "try-again";

    /// <summary>The reviewer raised nothing in what it read: said with what it read, never as <i>no issues</i>.</summary>
    public const string RaisedNothing = "raised-nothing";

    /// <summary>The working session runs or waits on the person: the findings reach it at its turn's end, never mid-step.</summary>
    public const string Waiting = "waiting";

    /// <summary>The findings were handed to the working session, which goes on with them at the driver's next look.</summary>
    public const string Handed = "handed";

    /// <summary>The working session has them: it is answering, or its turn is about to take them.</summary>
    public const string WithSession = "with-session";

    /// <summary>Its turn ended, and its answers were read.</summary>
    public const string Answered = "answered";

    /// <summary>The findings went to the person: the working session could not take them, or they are a recheck's.</summary>
    public const string ToPerson = "to-person";

    /// <summary>The service holds no such opinion.</summary>
    public const string Unknown = "unknown";
}

/// <summary>
/// Why findings went to the person instead of the working session (XAGENT1e; design §6.7), beside <see cref="ContinueWhy"/>'s
/// codes for a session that could not go on.
/// </summary>
public static class OpinionToPerson
{
    /// <summary>No record of this machine's by the working session's id.</summary>
    public const string NotFound = "not-found";

    /// <summary>A recheck's findings go to the person, never back to the working session by themselves (design §6.5).</summary>
    public const string Recheck = "recheck";

    /// <summary>
    /// The work was a conversation's (design §12.2): a chat lands nothing by the driver's gate, and its runner would take another
    /// agent's claims as the person's words, so they go to the person.
    /// </summary>
    public const string Conversation = "conversation";

    /// <summary>
    /// The line for <paramref name="code"/>, this list's or <see cref="ContinueWhy"/>'s for a session that could not go on, in
    /// the driver's English.
    /// </summary>
    public static string Said(string code) => code switch
    {
        NotFound => "there is no session of this machine's by its id",
        Recheck => "a recheck's findings go to the person, never back to the working session by themselves",
        Conversation => "the work was a conversation's, which another agent's findings do not reach by themselves",
        // A reason whose line names an adapter is said by its code here: the session's own note says it whole.
        ContinueWhy.Adapter or ContinueWhy.Unable => $"its session could not go on ({code})",
        _ => Fixed(code),
    };

    private static string Fixed(string code)
    {
        try
        {
            return ContinueWhy.Of(code).Sentence;
        }
        catch (ArgumentOutOfRangeException)
        {
            return code;
        }
    }
}

/// <summary>What the driver did with one opinion, and what it learned (XAGENT1e): kept beside the opinion's packet.</summary>
/// <param name="Person">Why its findings went to the person, a code; null where they did not.</param>
public sealed record OpinionDelivered(string? Person, DateTimeOffset? PersonAt, OpinionReading? Answered)
{
    /// <summary>The recheck the driver asked of it, by its opinion's id; null where none was asked.</summary>
    public string? Recheck { get; init; }

    /// <summary>Why no recheck was asked, a code of <see cref="OpinionRechecks"/> or the choice's; null where one was, or none judged yet.</summary>
    public string? RecheckWhy { get; init; }

    /// <summary>Nothing done yet.</summary>
    public static OpinionDelivered None { get; } = new(null, null, null);
}

/// <summary>
/// What the driver keeps of each opinion it delivers (XAGENT1e): <c>&lt;home&gt;/opinions/&lt;id&gt;/delivered.json</c>, beside the
/// pass's diff (§4): whether its findings went to the person and why, the working session's answers as read when its turn
/// ended, and the recheck asked or why none was. This machine's own, as the opinion is (D47 §4).
/// </summary>
/// <remarks>A file that does not read is nothing done: the next look reads the host and the record again, never the words lost.</remarks>
public sealed class OpinionDeliveries(string home)
{
    public const string FileName = "delivered.json";

    /// <summary>Where an opinion's delivery is kept; null for an id that is no plain name.</summary>
    public string? PathOf(string opinion)
    {
        try
        {
            return Path.Combine(OpinionPackets.Folder(home, opinion), FileName);
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>What was kept for <paramref name="opinion"/>; <see cref="OpinionDelivered.None"/> where nothing reads.</summary>
    public OpinionDelivered Read(string opinion)
    {
        if (PathOf(opinion) is not { } path || !File.Exists(path)) return OpinionDelivered.None;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return OpinionDelivered.None;
            return new OpinionDelivered(Text(root, "person"), Moment(root, "personAt"), Reading(root))
            {
                Recheck = Text(root, "recheck"),
                RecheckWhy = Text(root, "recheckWhy"),
            };
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return OpinionDelivered.None;
        }
    }

    /// <summary>Its findings went to the person, and why: kept once, the first reason standing.</summary>
    public OpinionDelivered ToPerson(string opinion, string why, DateTimeOffset at) =>
        Keep(opinion, kept => kept.Person is not null ? kept : kept with { Person = why, PersonAt = at });

    /// <summary>The answers as read when the working session's turn ended: the latest reading stands.</summary>
    public OpinionDelivered Answered(string opinion, OpinionReading reading) => Keep(opinion, kept => kept with { Answered = reading });

    /// <summary>The recheck asked, by its opinion's id, or why none was: kept once.</summary>
    public OpinionDelivered Rechecked(string opinion, string? recheck, string? why) =>
        Keep(opinion, kept => kept.Recheck is not null || kept.RecheckWhy is not null ? kept : kept with { Recheck = recheck, RecheckWhy = why });

    private OpinionDelivered Keep(string opinion, Func<OpinionDelivered, OpinionDelivered> change)
    {
        var next = change(Read(opinion));
        if (PathOf(opinion) is not { } path) return next;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // LF on every platform, as every file under the home is written.
            AtomicFile.WriteText(path, Json(next).Replace("\r\n", "\n") + "\n");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Lost: the next look reads the host and the record again, and keeps it then.
        }

        return next;
    }

    private static string Json(OpinionDelivered delivered)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            if (delivered.Person is { } person)
            {
                writer.WriteString("person", person);
                if (delivered.PersonAt is { } at) writer.WriteString("personAt", OpinionViews.Moment(at));
            }

            if (delivered.Answered is { } reading)
            {
                writer.WriteStartObject("answered");
                writer.WriteString("session", reading.Session);
                if (reading.Tip is not null) writer.WriteString("tip", reading.Tip);
                writer.WriteString("at", OpinionViews.Moment(reading.At));
                writer.WriteStartArray("findings");
                foreach (var finding in reading.Findings)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("finding", finding.Finding);
                    writer.WriteString("weight", finding.Weight);
                    writer.WriteString("counts", finding.Counts);
                    if (finding.Said is not null) writer.WriteString("said", finding.Said);
                    if (finding.Commit is not null) writer.WriteString("commit", finding.Commit);
                    if (finding.Fix is not null) writer.WriteString("fix", finding.Fix);
                    if (finding.Why is not null) writer.WriteString("why", finding.Why);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            if (delivered.Recheck is not null) writer.WriteString("recheck", delivered.Recheck);
            if (delivered.RecheckWhy is not null) writer.WriteString("recheckWhy", delivered.RecheckWhy);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static OpinionReading? Reading(JsonElement root)
    {
        if (!root.TryGetProperty("answered", out var answered) || answered.ValueKind != JsonValueKind.Object
            || Text(answered, "session") is not { } session || Moment(answered, "at") is not { } at)
        {
            return null;
        }

        var findings = new List<OpinionAnswerRead>();
        if (answered.TryGetProperty("findings", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("finding", out var number) || number.ValueKind != JsonValueKind.Number
                    || !number.TryGetInt32(out var finding) || Text(row, "weight") is not { } weight || Text(row, "counts") is not { } counts)
                {
                    continue;
                }

                findings.Add(new OpinionAnswerRead(finding, weight, counts)
                {
                    Said = Text(row, "said"), Commit = Text(row, "commit"), Fix = Text(row, "fix"), Why = Text(row, "why"),
                });
            }
        }

        return new OpinionReading(session, Text(answered, "tip"), at, findings);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Moment(JsonElement element, string name) =>
        Text(element, name) is { } when
        && DateTimeOffset.TryParse(when, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;
}

/// <summary>
/// The working session's answers, read when its turn ends (XAGENT1e, D155 point 7; design §6.4): each finding's latest answer,
/// a fix's commit checked against the tree's history, and a finding with none read as <c>unresolved</c>, not answered. What a
/// fix fixes is the session's claim; that its commit is one the turn made is a fact git answers.
/// </summary>
public static class OpinionAnswers
{
    /// <summary>
    /// The answers to <paramref name="opinion"/>'s findings as they stand, read in <paramref name="tree"/>: a fix counts where
    /// git reads its commit after the candidate's tip and at or before the tree's tip. A tree that is gone checks no fix.
    /// </summary>
    internal static async Task<OpinionReading> ReadAsync(
        OpinionView opinion, string session, string? tree, WorkingTree.GitRead git, DateTimeOffset at, CancellationToken ct)
    {
        var readable = tree is { Length: > 0 } && Directory.Exists(tree);
        var head = readable ? await CommitAsync(tree!, "HEAD", git, ct).ConfigureAwait(false) : null;
        var tip = readable && head is not null ? await CommitAsync(tree!, opinion.Tip, git, ct).ConfigureAwait(false) : null;

        var rows = new List<OpinionAnswerRead>();
        foreach (var finding in opinion.Findings ?? [])
        {
            var answer = opinion.AnswerTo(finding.Number);
            if (answer is null)
            {
                rows.Add(new OpinionAnswerRead(finding.Number, finding.Weight, OpinionViews.Unresolved) { Why = OpinionAnswerWhy.NotAnswered });
                continue;
            }

            if (answer.Said != OpinionViews.Fixed)
            {
                var counts = answer.Said == OpinionViews.Rejected ? OpinionViews.Rejected : OpinionViews.Unresolved;
                rows.Add(new OpinionAnswerRead(finding.Number, finding.Weight, counts) { Said = answer.Said });
                continue;
            }

            var (fix, why) = head is null || tip is null
                ? (null, OpinionAnswerWhy.FixUnread)
                : await CheckFixAsync(tree!, tip, head, answer.Commit, git, ct).ConfigureAwait(false);
            rows.Add(new OpinionAnswerRead(finding.Number, finding.Weight, fix is null ? OpinionViews.Unresolved : OpinionViews.Fixed)
            {
                Said = answer.Said, Commit = answer.Commit, Fix = fix, Why = why,
            });
        }

        return new OpinionReading(session, head, at, rows);
    }

    /// <summary>
    /// Whether <paramref name="commit"/> is one the turn made (design §6.4): a commit the tree holds, after
    /// <paramref name="tip"/>, at or before <paramref name="head"/>. Its full id where it is; else why not, by code.
    /// </summary>
    internal static async Task<(string? Fix, string? Why)> CheckFixAsync(
        string tree, string tip, string head, string? commit, WorkingTree.GitRead git, CancellationToken ct)
    {
        // A name git would read as an option names no commit, and neither does anything that is no commit's id.
        if (commit is not { Length: > 0 } named || !WorkingTree.IsCommitId(named)) return (null, OpinionAnswerWhy.FixUnknown);
        var full = await CommitAsync(tree, named, git, ct).ConfigureAwait(false);
        if (full is null) return (null, OpinionAnswerWhy.FixUnknown);
        if (string.Equals(full, tip, StringComparison.OrdinalIgnoreCase)) return (null, OpinionAnswerWhy.FixNotAfter);

        // `--is-ancestor` answers 0 for yes and 1 for no; anything else is git failing to answer.
        var after = await RunAsync(tree, ["merge-base", "--is-ancestor", tip, full], git, ct).ConfigureAwait(false);
        if (after.Code == 1) return (null, OpinionAnswerWhy.FixNotAfter);
        if (after.Code != 0) return (null, OpinionAnswerWhy.FixUnread);

        var on = await RunAsync(tree, ["merge-base", "--is-ancestor", full, head], git, ct).ConfigureAwait(false);
        return on.Code switch
        {
            0 => (full, null),
            1 => (null, OpinionAnswerWhy.FixOffBranch),
            _ => (null, OpinionAnswerWhy.FixUnread),
        };
    }

    /// <summary>
    /// The driver's line in the working session's conversation once its answers are read: whose findings they were, and how
    /// each counts. The person's view of the answers beside the claims is the page's (XAGENT1g); this is the record's.
    /// </summary>
    public static string Line(OpinionView opinion, OpinionReading reading)
    {
        var parts = new List<string>();
        int Count(Func<OpinionAnswerRead, bool> which) => reading.Findings.Count(which);
        var fixedCount = Count(row => row.Counts == OpinionViews.Fixed);
        var rejected = Count(row => row.Counts == OpinionViews.Rejected);
        var unresolved = Count(row => row.Counts == OpinionViews.Unresolved && row.Why is null);
        var unanswered = Count(row => row.Why == OpinionAnswerWhy.NotAnswered);
        var notMade = Count(row => row.Said == OpinionViews.Fixed && row.Fix is null);
        if (fixedCount > 0) parts.Add($"{fixedCount} fixed, each commit read from git as one this turn made");
        if (notMade > 0) parts.Add($"{notMade} said fixed whose commit git did not read as one this turn made, read as unresolved");
        if (rejected > 0) parts.Add($"{rejected} rejected with its evidence");
        if (unresolved > 0) parts.Add($"{unresolved} unresolved");
        if (unanswered > 0) parts.Add($"{unanswered} not answered, read as unresolved");
        var disputed = reading.Findings.Count(row => row.Disputed);
        return $"— the answers to {opinion.Who}'s {Plural(reading.Findings.Count, "finding")}, as its turn ended: {string.Join("; ", parts)}."
               + (disputed > 0 ? $" {Plural(disputed, "`must` finding")} not fixed {(disputed == 1 ? "waits" : "wait")} for the person." : "");
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    /// <summary>A commit's full id as <paramref name="tree"/>'s git names it, or null.</summary>
    private static async Task<string?> CommitAsync(string tree, string name, WorkingTree.GitRead git, CancellationToken ct)
    {
        if (name.StartsWith('-')) return null;
        var (code, output) = await RunAsync(tree, ["rev-parse", "--verify", "--quiet", $"{name}^{{commit}}"], git, ct).ConfigureAwait(false);
        var id = output.Trim().ToLowerInvariant();
        return code == 0 && EvidenceCodes.IsObjectId(id) ? id : null;
    }

    private static async Task<(int Code, string Output)> RunAsync(
        string tree, IReadOnlyList<string> arguments, WorkingTree.GitRead git, CancellationToken ct)
    {
        var output = new StringBuilder();
        var code = await git(tree, arguments, piece =>
        {
            output.Append(piece.Span);
            return true;
        }, ct).ConfigureAwait(false);
        return (code, output.ToString());
    }
}

/// <summary>
/// The one recheck (XAGENT1e, D155 point 7; design §6.5): whether it is due, and what it is handed. One pass reads the commits
/// since the first pass's tip, once, and only where the working session answered a finding fixed with a commit its turn made;
/// its findings go to the person, never back to the working session by themselves.
/// </summary>
public static class OpinionRechecks
{
    /// <summary>The rule turns the recheck off.</summary>
    public const string Off = "off";

    /// <summary>A recheck is never rechecked.</summary>
    public const string NotFirst = "not-first";

    /// <summary>The working session's turn has not ended, so nothing is read yet.</summary>
    public const string NotAnswered = "not-answered";

    /// <summary>No finding was answered fixed with a commit its turn made: nothing new to read for it.</summary>
    public const string NoFix = "no-fix";

    /// <summary>The pass that would have read it again held or was refused before it opened, for the reason its line said.</summary>
    public const string Refused = "refused";

    /// <summary>
    /// Null where a recheck is due (design §6.5): a first pass whose working session's answers were read, one of them a fix
    /// whose commit the turn made, under a rule that keeps the recheck. Else why not, by code.
    /// </summary>
    public static string? WhyNot(OpinionView first, OpinionReading? reading, OpinionRule rule) =>
        !first.First ? NotFirst
        : !rule.Recheck ? Off
        : reading is null ? NotAnswered
        : !reading.AnyFixed || reading.Tip is null ? NoFix
        : null;

    /// <summary>The line for <paramref name="code"/>, in the driver's English.</summary>
    public static string Said(string code) => code switch
    {
        Off => "the rule turns the recheck off",
        NotFirst => "a recheck is never rechecked",
        NotAnswered => "the working session has not answered yet",
        NoFix => "no finding was answered fixed with a commit its turn made, so there is nothing new to read",
        Refused => "the pass that would have read it again did not open",
        // The reviewer choice's codes (§3.3): nobody could read it again, said by the code the choice gave.
        _ => $"no reviewer could read it again ({code})",
    };
}

/// <summary>
/// What waits for the person once the opinion and its recheck are read (design §6.6, §8.2): a first-pass <c>must</c> the
/// working session did not fix and the recheck did not withdraw, and every <c>must</c> the recheck raised. A <c>should</c> or a
/// <c>note</c> is shown and waits for nothing; two agents agreeing establish nothing.
/// </summary>
/// <param name="First">The first pass's disputed findings, by number.</param>
/// <param name="Raised">The recheck's own <c>must</c> findings, by number.</param>
public sealed record OpinionDisputes(IReadOnlyList<int> First, IReadOnlyList<int> Raised)
{
    public int Count => First.Count + Raised.Count;

    /// <summary>The disputes of <paramref name="reading"/>, with <paramref name="recheck"/>'s word where one was given.</summary>
    public static OpinionDisputes Of(OpinionReading reading, OpinionView? recheck)
    {
        var said = recheck is { Given: true } given ? given : null;
        return new OpinionDisputes(
            [.. reading.Findings.Where(row => row.Disputed && said?.Rechecked.GetValueOrDefault(row.Finding) != OpinionViews.Withdrawn).Select(row => row.Finding)],
            [.. (said?.Findings ?? []).Where(finding => finding.Weight == OpinionViews.Must).Select(finding => finding.Number)]);
    }

    /// <summary>
    /// The disputes of findings that never reached the working session (design §6.7): each stands unanswered, and each
    /// <c>must</c> is disputed.
    /// </summary>
    public static OpinionDisputes Unanswered(OpinionView opinion) =>
        new([.. (opinion.Findings ?? []).Where(finding => finding.Weight == OpinionViews.Must).Select(finding => finding.Number)], []);
}
