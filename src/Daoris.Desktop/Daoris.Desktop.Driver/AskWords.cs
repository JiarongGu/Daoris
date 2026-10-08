using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>One of the person's words on an ask (DRIFT1a, D133 §1), as the service answers it.</summary>
/// <param name="Kind">
/// <c>asked</c>, <c>answered</c>, <c>added</c>, <c>reopened</c>, or a review's <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>, as the
/// service spells it. A kind this build does not know is kept as spelled and said as a word they said, never dropped: what the
/// person said outranks what this build can name.
/// </param>
/// <param name="Text">Their words, verbatim.</param>
/// <param name="Session">The session it was said to; none for the ask's own sentence, and none for a review's verdict.</param>
/// <param name="Quest">
/// The quest that session worked, or the quest a review's verdict was given on; none for the ask's own sentence, an intake's,
/// and a conversation's.
/// </param>
public sealed record AskWordView(string Kind, string Text, DateTimeOffset At, string? Session = null, string? Quest = null)
{
    public const string Asked = "asked";
    public const string Answered = "answered";
    public const string Added = "added";

    /// <summary>Said to a session after it ended, kept once a session took them (MSG1a's <c>reopened</c>, D137 §2.4).</summary>
    public const string Reopened = "reopened";

    /// <summary>Their words with a <c>reviewed</c> on a set-up step (REVIEWENV1b, D154 point 8; design §3.5).</summary>
    public const string Reviewed = "reviewed";

    /// <summary>Their words with a <c>not-yet</c> on a set-up step: what is not right yet (REVIEWENV1b; design §3.4).</summary>
    public const string NotYet = "not-yet";

    /// <summary>Their words with a skip of a review (REVIEWENV1b; design §3.6).</summary>
    public const string Skipped = "skipped";
}

/// <summary>
/// The person's words on the ask a session's work is for (DRIFT1b, D133 §2), as the driver read them for its start: every
/// one, oldest first, or, where the service did not answer them, none, which the session is told.
/// </summary>
/// <param name="Ask">The ask's id.</param>
/// <param name="Said">Its words, oldest first, the ask's own sentence first; null where they could not be read.</param>
/// <param name="KeptFrom">For an ask from before its words were kept (DRIFT1a): from when they are, since nothing is back-filled.</param>
public sealed record AskWords(string Ask, IReadOnlyList<AskWordView>? Said, DateTimeOffset? KeptFrom = null)
{
    /// <summary>
    /// The go-aheads its sessions asked the person for (KNOWUSE1a, D135 §2), oldest first, read in the same answer as the
    /// words: null where the service answered none, unread or a host from before them, and the instruction says nothing of
    /// them then.
    /// </summary>
    public IReadOnlyList<GoAheadView>? GoAheads { get; init; }

    /// <summary>
    /// The sender a quest an ask asked carries, chain steps included (D65 §4). A twin of the service's
    /// <c>AskDesk.SenderOf</c>, duplicated deliberately: the driver and the service share no code.
    /// </summary>
    private const string SenderPrefix = "ask #";

    /// <summary>Words the service did not answer: unreachable, refused, unparsable, an ask it does not hold, or a host from before them.</summary>
    public static AskWords Unread(string ask) => new(ask, null);

    /// <summary>
    /// The ask a quest was asked by, read from its sender — the reverse of the service's <c>AskDesk.SenderOf</c> and a twin
    /// of its <c>AskOf</c>: null for a quest a repository asked, which is on no ask.
    /// </summary>
    public static string? AskOf(string? sender) =>
        sender is not null && sender.Length > SenderPrefix.Length && sender.StartsWith(SenderPrefix, StringComparison.Ordinal)
            ? sender[SenderPrefix.Length..]
            : null;

    /// <summary>What an ask the service answered holds: its words, or unread where a host answered none.</summary>
    public static AskWords Of(AskView ask) =>
        (ask.Words is { } said ? new AskWords(ask.Id, said, ask.WordsKeptFrom) : Unread(ask.Id)) with { GoAheads = ask.GoAheads };

    /// <summary>
    /// The person's words on the ask a quest was asked by, read from the ask for a session's start (DRIFT1b): null for a
    /// quest no ask asked, and never a failure. A read that does not answer is <see cref="Unread"/>, and the start goes on
    /// with its instruction saying so: the words are what a session is handed, never what holds it.
    /// </summary>
    /// <exception cref="OperationCanceledException">The driver is closing: a start it ends is not one to compose.</exception>
    public static async Task<AskWords?> ReadAsync(ServiceClient service, string? sender, CancellationToken ct)
    {
        if (AskOf(sender) is not { } ask) return null;
        try
        {
            return await service.FindAskAsync(ask, ct).ConfigureAwait(false) is { } held ? Of(held) : Unread(ask);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                          or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // A client's own time-out is a cancellation the driver did not ask for: the service did not answer.
            return Unread(ask);
        }
    }
}

/// <summary>
/// How the person's words are written into an instruction (DRIFT1b, D133 §2): verbatim and quoted, each saying how it was
/// given, when, and on which quest, oldest first and newest last — and bounded, saying what was left out.
/// </summary>
/// <remarks>
/// <para><b>What bounds it.</b> The ask's own sentence is kept whole, as the quest's body and the intake's instruction
/// already carry it. Every word after it is cut at <see cref="WordLimit"/> characters, and of those the newest that fit in
/// <see cref="WordsLimit"/> are kept: a correction is newer than what it corrects. The older are left out as one line,
/// between the sentence and the newest, saying how many and when they were said, so the order still reads oldest first.</para>
///
/// <para><b>Why that size.</b> The native door hands its instruction as one argument, and Windows caps a whole command line
/// at 32,767 characters, which the instruction already shares with a carry-on's plan, its last words and the quest's
/// body. The words the drift lost were a few hundred characters each, so the bound keeps dozens of them.</para>
/// </remarks>
public static class AskWordsText
{
    /// <summary>The most characters of the person's words after the ask's own sentence an instruction carries.</summary>
    public const int WordsLimit = 8_000;

    /// <summary>The most characters of one word after the ask's sentence: a pasted log is not a sentence to quote whole.</summary>
    public const int WordLimit = 2_000;

    /// <summary>The line an instruction carries where the words could not be read, and today's instruction stands.</summary>
    public static string UnreadSentence(string ask) =>
        $"The person's words on ask `#{ask}` could not be read just now, so any they gave after the ask itself are not here.";

    /// <summary>The intake's line for the same: the ask's sentence is above it, and nothing after it could be read.</summary>
    internal const string IntakeUnread = "Whether they said more on it since the ask could not be read just now.";

    /// <summary>
    /// The words beneath a quest (DRIFT1b): a heading, then each word, the older left out past the bound said as one line;
    /// or the unread line. Empty for a session on no ask, so its instruction reads exactly as it did.
    /// </summary>
    internal static string Beneath(AskWords? words, string questId)
    {
        if (words is null) return "";
        if (words.Said is not { } said) return "\n" + UnreadSentence(words.Ask) + "\n";

        var text = new StringBuilder("\n");
        text.Append($"The person's own words on ask `#{words.Ask}`, oldest first and newest last. These are their words verbatim: ")
            .Append("the quest above, a plan or an earlier session's note is someone else's reading of them.");
        if (words.KeptFrom is { } from)
        {
            text.Append($" Daoris has kept their words on this ask since {When(from)}: anything they said before then is not among them.");
        }

        text.Append('\n');
        text.Append(Listed(said, questId, skipAsked: false));
        return text.ToString();
    }

    /// <summary>
    /// For an intake (DRIFT1b): what the person said on its ask after the ask itself, which its instruction quotes above;
    /// nothing where they said nothing more, and the intake's unread line where it could not be read.
    /// </summary>
    internal static string Since(AskView ask)
    {
        if (ask.Words is not { } said) return IntakeUnread + "\n\n";
        if (!said.Any(word => word.Kind != AskWordView.Asked)) return "";
        return "What they said on it since, oldest first and newest last:\n" + Listed(said, questId: null, skipAsked: true) + "\n";
    }

    /// <summary>
    /// Whether a word with these exact words is among those an instruction quotes, after the bound — so a carry-on's
    /// answer is quoted once, and pointed to only where it is there to point to.
    /// </summary>
    internal static bool Quotes(AskWords? words, string text) =>
        words?.Said is { } said && Bounded(said).Shown.Any(word => word.Kind == AskWordView.Answered && word.Text == text.Trim());

    /// <summary>
    /// The words an instruction shows, by the bound (see the remarks), and those it leaves out: the ask's own sentence
    /// always, then the newest of the rest that fit.
    /// </summary>
    internal static (IReadOnlyList<AskWordView> Shown, IReadOnlyList<AskWordView> Left) Bounded(IReadOnlyList<AskWordView> said)
    {
        var asked = said.Count > 0 && said[0].Kind == AskWordView.Asked ? said[0] : null;
        var rest = asked is null ? said : said.Skip(1).ToList();

        var kept = 0;
        var used = 0;
        for (var i = rest.Count - 1; i >= 0; i--)
        {
            var cost = Math.Min(rest[i].Text.Length, WordLimit);
            if (used + cost > WordsLimit) break;
            used += cost;
            kept++;
        }

        var left = rest.Take(rest.Count - kept).ToList();
        IReadOnlyList<AskWordView> shown = [.. asked is null ? [] : new[] { asked }, .. rest.Skip(rest.Count - kept)];
        return (shown, left);
    }

    private static string Listed(IReadOnlyList<AskWordView> said, string? questId, bool skipAsked)
    {
        var (shown, left) = Bounded(said);
        var text = new StringBuilder();
        var leftSaid = left.Count == 0;
        foreach (var word in shown)
        {
            if (!leftSaid && word.Kind != AskWordView.Asked)
            {
                text.Append('\n')
                    .Append($"- … {left.Count} of their words, said from {When(left[0].At)} to {When(left[^1].At)}, are left out ")
                    .Append("here to keep this instruction bounded; the ask's record on this machine keeps every one.\n");
                leftSaid = true;
            }

            if (skipAsked && word.Kind == AskWordView.Asked) continue;
            text.Append('\n').Append($"- {Given(word, questId)}, {When(word.At)}:\n\n").Append(Quoted(word.Text, word.Kind == AskWordView.Asked));
        }

        return text.ToString();
    }

    /// <summary>How a word was given, and on which quest: this one, another by its id, or none named.</summary>
    private static string Given(AskWordView word, string? questId)
    {
        var on = word.Quest is not { Length: > 0 } quest ? ""
            : string.Equals(quest, questId, StringComparison.OrdinalIgnoreCase) ? " on this quest"
            : $" on quest `#{quest}`";
        return word.Kind switch
        {
            AskWordView.Asked => "They asked",
            AskWordView.Answered => $"They answered a session{on}",
            AskWordView.Added => $"They added, while a session{on} ran",
            AskWordView.Reopened => $"They added, after a session{on} ended",
            // A review's verdict (REVIEWENV1b2): said as the verdict it came with, of the set-up or the work on its quest.
            AskWordView.Reviewed => $"They reviewed the set-up shown{on}",
            AskWordView.NotYet => $"They said not yet to the set-up shown{on}",
            AskWordView.Skipped => $"They skipped the review of the work{on}",
            _ => on.Length == 0 ? "They said" : $"They said,{on}",
        };
    }

    /// <summary>
    /// A word quoted line by line beneath its item, cut at <see cref="WordLimit"/> and said to be — except the ask's own
    /// sentence, which the quest already carries whole.
    /// </summary>
    private static string Quoted(string text, bool whole)
    {
        var cut = !whole && text.Length > WordLimit;
        var quoted = cut ? text[..WordLimit] : text;
        var lines = quoted.ReplaceLineEndings("\n").Split('\n').Select(line => line.Length == 0 ? "  >" : $"  > {line}");
        var result = string.Join("\n", lines) + "\n";
        return cut
            ? result + $"  (… and {text.Length - WordLimit} more characters of this one, left out here to keep this instruction bounded.)\n"
            : result;
    }

    /// <summary>A moment as the instruction says it: to the minute, in UTC, the same on every machine.</summary>
    private static string When(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
