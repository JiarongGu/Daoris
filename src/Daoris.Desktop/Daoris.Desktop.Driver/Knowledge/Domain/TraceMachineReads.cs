using System.Globalization;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>This machine's side of a session in the chain: its events and the rules file it was handed.</summary>
internal static partial class TraceMachineReads
{
    /// <summary>The states a session's process runs in, whose rules file stands while it does.</summary>
    private static readonly HashSet<string> Running = new(StringComparer.Ordinal) { "queued", "starting", "working" };

    /// <summary>
    /// What a landing keeps in the session's record (D100), read from the writer itself so a reworded note is read the same: the
    /// person's acceptance, a merge into the line's included, an acceptance at the quest's done (LAND2b), and a hand-off after it.
    /// </summary>
    private static readonly IReadOnlyList<string> Acceptances = KnowledgeWorkplace.Current.AcceptanceNotes;

    /// <summary>
    /// This machine's record of it (D76): the driver's notes before its instruction, which name the account a start chose and
    /// why; each instruction it was handed, by its event and size; and each acceptance of its work a landing's press kept.
    /// </summary>
    internal static TraceEvents Events(TracedSession session, TraceHere here)
    {
        if (!SessionEventText.IsId(session.Id)) return new TraceEvents { Missing = TraceEventGaps.NotAnId };

        var read = here.ReadEvents(session.Id);
        if (read.All is not { } all) return new TraceEvents { Missing = read.Missing, Problem = read.Problem };

        return new TraceEvents
        {
            Starts =
            [
                .. all.TakeWhile(e => e.Kind != SessionEventKind.User)
                    .Where(e => e.Kind == SessionEventKind.Note && e.Text is { Length: > 0 })
                    .Select(note => note.Text!),
            ],
            Instructions = [.. all.Where(e => e.Kind == SessionEventKind.User && e.Origin == "target").Select(Instruction)],
            Accepted =
            [
                .. all.Where(e => e.Kind == SessionEventKind.Note && e.Text is { } said
                                  && Acceptances.Any(start => said.StartsWith(start, StringComparison.Ordinal)))
                    .Select(note => new TraceAcceptance(note.At, note.Text!)),
            ],
        };
    }

    /// <summary>One instruction by its event and its size: the length written, or, where the record cut it, the length it said.</summary>
    private static TraceInstruction Instruction(SessionEvent instruction)
    {
        var said = instruction.Text ?? "";
        if (CutSaid().Match(said) is { Success: true } cut && cut.Groups["kept"].Length == SessionEventText.TextLimit
            && int.TryParse(cut.Groups["chars"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var whole))
        {
            return new TraceInstruction(instruction.Seq, whole) { Kept = SessionEventText.TextLimit, Account = instruction.Account };
        }

        return new TraceInstruction(instruction.Seq, said.Length) { Account = instruction.Account };
    }

    /// <summary>
    /// The rules it was handed (PERM1, D72), from the file the driver wrote under the home for it, while that file stands: it is
    /// the session's own and goes when its run ends.
    /// </summary>
    internal static TraceRules Rules(TracedSession session, TraceHere here)
    {
        var read = here.ReadRules(session.Id);
        if (read.Problem is { } problem) return new TraceRules { Missing = TraceRuleGaps.Unread, Problem = problem };
        if (read.Text is not { } text)
        {
            return new TraceRules { Missing = Running.Contains(session.State) ? TraceRuleGaps.NoneWhileRunning : TraceRuleGaps.GoneWithRun };
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(text);
            var root = document.RootElement;
            var permissions = root.TryGetProperty("permissions", out var lists) ? lists : default;
            int Listed(System.Text.Json.JsonElement element, string list) =>
                element.ValueKind == System.Text.Json.JsonValueKind.Object && element.TryGetProperty(list, out var items)
                && items.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? items.GetArrayLength()
                    : 0;
            var hard = root.TryGetProperty("autoMode", out var auto) && auto.TryGetProperty("hard_deny", out var denials)
                       && denials.ValueKind == System.Text.Json.JsonValueKind.Array
                ? denials.EnumerateArray().Count(entry => entry.ValueKind == System.Text.Json.JsonValueKind.String && entry.GetString() != TraceSpawnFiles.HarnessDefaults)
                : 0;
            return new TraceRules
            {
                Allowed = Listed(permissions, "allow"),
                Asked = Listed(permissions, "ask"),
                Denied = Listed(permissions, "deny"),
                Hard = hard,
                Guarded = TraceRules.HandsTreeGuard(root),
            };
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or InvalidOperationException)
        {
            return new TraceRules { Missing = TraceRuleGaps.Unread, Problem = error.Message };
        }
    }

    /// <summary>A field the record cut (<see cref="SessionEventText.Cut"/>): what it kept, then how long the original was.</summary>
    [GeneratedRegex(@"^(?<kept>.*)… \((?<chars>\d+) chars\)$", RegexOptions.Singleline)]
    private static partial Regex CutSaid();
}
