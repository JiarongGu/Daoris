namespace Daoris.Driver;

/// <summary>
/// The person's words a press puts on an ask (D277): requirements' quotes and through words. Where Ask Daoris presses, proved by the
/// delegate key that names its conversation, each must stand verbatim (whitespace and case aside) in one of the person's own messages
/// there; the person's own press, in the window, on a card or at their terminal, is their word; a working session's own press puts none.
/// No model reads them: a quote the person never said is refused naming it.
/// </summary>
public static class AskQuotes
{
    /// <summary>The origin a conversation's events give the person's own messages.</summary>
    public const string PersonOrigin = "person";

    /// <summary>The person's own messages in a conversation's events, oldest first.</summary>
    public static IReadOnlyList<string> PersonSaid(IEnumerable<SessionEvent> events) =>
        [.. events.Where(e => e.Kind == SessionEventKind.User && e.Origin == PersonOrigin && !string.IsNullOrWhiteSpace(e.Text)).Select(e => e.Text!)];

    /// <summary>
    /// Whether <paramref name="quote"/> stands in <paramref name="words"/>, every run of white space one space and case aside: the
    /// service's quote judge's own rule (<c>QuestRequirement.QuotedIn</c>), so what passes here passes there.
    /// </summary>
    public static bool QuotedIn(string quote, string words)
    {
        var folded = Folded(quote);
        return folded.Length > 0 && Folded(words).Contains(folded, StringComparison.Ordinal);
    }

    /// <summary>
    /// Why <paramref name="carried"/> cannot go on an ask as pressed by <paramref name="by"/> (<see cref="TerminalTierBy"/>), or null;
    /// <paramref name="said"/> reads the person's messages in the proving conversation, only for Ask Daoris's press.
    /// </summary>
    public static string? Refused(AskCarried carried, string by, Func<IReadOnlyList<string>> said)
    {
        var quotes = carried.Requirements.Select(requirement => requirement.Quote ?? "")
            .Concat(carried.Through is { } through ? [through] : []).ToList();
        if (quotes.Count == 0) return null;
        if (by == TerminalTierBy.Session)
        {
            return "A working session's own press puts no words of the person's on an ask: requirements and through words are the person's, "
                + "quoted from their conversation with Ask Daoris or given at their own press. Nothing was asked.";
        }

        if (by != TerminalTierBy.AskDaoris) return null;
        var messages = said();
        var unsaid = quotes.Where(quote => !messages.Any(message => QuotedIn(quote, message))).Distinct(StringComparer.Ordinal).ToList();
        return unsaid.Count == 0
            ? null
            : $"{(unsaid.Count == 1 ? "This quotes words" : $"{unsaid.Count} of these quote words")} the person never said in this conversation:\n\n"
              + string.Join("\n", unsaid.Select(quote => $"- \"{quote}\""))
              + "\n\nRequirements and through words are the person's own messages, copied verbatim (whitespace and case aside); your reading "
              + "of them belongs in the ask's words. Nothing was asked.";
    }

    private static string Folded(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
