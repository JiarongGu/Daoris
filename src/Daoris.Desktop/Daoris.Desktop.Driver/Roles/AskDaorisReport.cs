using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A range's report's states: queued while it may grow, running once a reader took it, then read, left, unread with why, or disabled
/// while the check is off.
/// </summary>
public static class AskDaorisStates
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Read = "read";
    public const string Left = "left";
    public const string Unread = "unread";
    public const string Disabled = "disabled";
}

/// <summary>
/// How a range's reader stands to its writer: another maker; the same maker, not independent; a maker not declared, not counted as
/// independent; or a writer not known, never presumed independent.
/// </summary>
public static class ReadIndependence
{
    public const string AnotherMaker = "another-maker";
    public const string NotIndependent = "not-independent";
    public const string NotCounted = "not-counted";
    public const string Unknown = "unknown";

    /// <summary>The standing of a reader the walk labelled <paramref name="label"/> to <paramref name="writer"/>.</summary>
    public static string Of(string? writer, string? label) => writer is null ? Unknown : label switch
    {
        ReviewerLabels.AnotherMaker => AnotherMaker,
        ReviewerLabels.SameAgent => NotIndependent,
        _ => NotCounted,
    };
}

/// <summary>
/// One finding of a reading, a claim and never a fact: its severity (<c>flag</c>, which the person should know before going on, or
/// <c>note</c>), its certainty, what it says, the packet's id of the call or press it rests on, and the packet's id of the person's words
/// that bear on it, or <c>none</c> where no words in the evidence cover the act.
/// </summary>
public sealed record AskDaorisFinding(string Severity, string Certainty, string Says, string Call, string Instruction)
{
    public const string Flag = "flag";
    public const string Note = "note";
    public const string NoInstruction = "none";

    public static IReadOnlyList<string> Severities { get; } = [Flag, Note];

    public static IReadOnlyList<string> Certainties { get; } = ["sure", "likely", "unsure"];
}

/// <summary>A reader's answer: its findings and how many past the most it did not write, or a leave with its bound and reasons.</summary>
public sealed record AskDaorisAnswer(
    SupervisorSubject Subject, IReadOnlyList<AskDaorisFinding> Findings, int More, string? Leave, IReadOnlyList<SupervisorReason> Reasons);

/// <summary>
/// One range of a conversation's turns, as the second check keeps it: its turns, its state (<see cref="AskDaorisStates"/>), the limits of
/// what a reading of it is shown, and once read its reader, how that reader stands to the range's writer, and its findings or its leave.
/// Every state the window shows derives from it.
/// </summary>
public sealed record AskDaorisReport(
    string Range, string Session, string State, DateTimeOffset Opened, IReadOnlyList<ReadTurn> Turns, IReadOnlyList<ReadLimitAt> Limits)
{
    /// <summary>The most findings one reading writes; it counts the rest in <c>more</c>.</summary>
    public const int MostFindings = 8;

    public DateTimeOffset? Closed { get; init; }

    /// <summary>Why it closed unread; null for every other state.</summary>
    public string? Why { get; init; }

    /// <summary>While it stays queued, the walk's sentence for why no reader can take it now.</summary>
    public string? Waiting { get; init; }

    /// <summary>The agent that wrote the range, as its session's record opened; null where none is known.</summary>
    public string? Writer { get; init; }

    public SupervisorAgentHolder? Reader { get; init; }

    /// <summary>How its reader stands to its writer (<see cref="ReadIndependence"/>), and the walk's sentence saying so.</summary>
    public string? Independence { get; init; }

    public string? Said { get; init; }

    public IReadOnlyList<AskDaorisFinding> Findings { get; init; } = [];

    public int More { get; init; }

    public string? Leave { get; init; }

    public IReadOnlyList<SupervisorReason> Reasons { get; init; } = [];

    /// <summary>About how many characters its packet takes.</summary>
    public int Size => Turns.Sum(turn => turn.Size);

    /// <summary>
    /// The reading <paramref name="lastMessage"/>'s one block holds, or why none reads: the fence's own refusals, a kind or subject not
    /// a reading's, an act that is not <c>report</c>, a finding out of shape or naming an id its packet does not hold, more than
    /// <see cref="MostFindings"/>; a leave read as every leave is, by one to five reasons.
    /// </summary>
    /// <param name="calls">The packet's ids of its calls and presses, which a finding's <c>call</c> names; null takes any.</param>
    /// <param name="words">The packet's ids of the person's words, which a finding's <c>instruction</c> names; null takes any.</param>
    public static (AskDaorisAnswer? Answer, string? Why) Read(string? lastMessage, IReadOnlySet<string>? calls = null, IReadOnlySet<string>? words = null)
    {
        var (body, why) = SupervisorBlock.Body(lastMessage);
        if (body is null) return (null, why);
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Json.Text(root, "kind") != SupervisorKinds.AskDaoris) return (null, "its block names no reading of Ask Daoris's acts");
            if (!root.TryGetProperty("subject", out var wire) || SupervisorSubject.Read(SupervisorKinds.AskDaoris, wire) is not { } subject) return (null, "its block names no range");
            var (act, leave) = (Json.Text(root, "do"), Json.Text(root, "leave"));
            if ((act is null) == (leave is null)) return (null, "its block names an act or a bound, one of them");
            if (leave is not null)
            {
                var (left, leftWhy) = SupervisorBlock.Read(lastMessage);
                return left is null ? (null, leftWhy) : (new AskDaorisAnswer(subject, [], 0, left.Leave, left.Reasons), null);
            }

            if (act != SupervisorKinds.Report) return (null, $"`{act}` is no act of a reading: {SupervisorKinds.Report}");
            var findings = new List<AskDaorisFinding>();
            foreach (var each in Json.Items(root, "findings"))
            {
                var finding = new AskDaorisFinding(
                    Json.Text(each, "severity") ?? "", Json.Text(each, "certainty") ?? "", Json.Text(each, "says") ?? "", Json.Text(each, "call") ?? "",
                    Json.Text(each, "instruction") ?? "");
                if (Refused(finding, calls, words) is { } refused) return (null, refused);
                findings.Add(finding);
            }

            if (findings.Count > MostFindings) return (null, $"its block holds more than {MostFindings} findings: the rest are counted in `more`");
            var more = root.TryGetProperty("more", out var counted) && counted.ValueKind == JsonValueKind.Number && counted.TryGetInt32(out var n) ? n : 0;
            return more < 0 ? (null, "its `more` is below zero") : (new AskDaorisAnswer(subject, findings, more, null, []), null);
        }
        catch (JsonException)
        {
            return (null, "its block is not one form");
        }
    }

    private static string? Refused(AskDaorisFinding finding, IReadOnlySet<string>? calls, IReadOnlySet<string>? words)
    {
        if (!AskDaorisFinding.Severities.Contains(finding.Severity)) return $"a finding's severity is one of {string.Join(", ", AskDaorisFinding.Severities)}";
        if (!AskDaorisFinding.Certainties.Contains(finding.Certainty)) return $"a finding's certainty is one of {string.Join(", ", AskDaorisFinding.Certainties)}";
        if (finding.Says.Trim().Length == 0 || finding.Says.Length > QuestionSupervisorDecisions.MaxSays) return $"a finding says something in at most {QuestionSupervisorDecisions.MaxSays} characters";
        if (finding.Call.Length == 0 || (calls is not null && !calls.Contains(finding.Call))) return $"a finding names call `{finding.Call}`, which its packet does not hold";
        return finding.Instruction == AskDaorisFinding.NoInstruction || (finding.Instruction.Length > 0 && (words is null || words.Contains(finding.Instruction)))
            ? null
            : $"a finding names the person's words `{finding.Instruction}`, which its packet does not hold: a packet id, or `none`";
    }
}
