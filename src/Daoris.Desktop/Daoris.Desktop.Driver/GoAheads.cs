using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>One session's request for a go-ahead (KNOWUSE1a), as the service answers it.</summary>
public sealed record GoAheadRequestView(string Session, string? Quest, DateTimeOffset At, string Why);

/// <summary>The person's answer to a go-ahead (KNOWUSE1a): yes or no, their words where they gave any, and when.</summary>
public sealed record GoAheadAnswerView(bool Approved, string? Words, DateTimeOffset At);

/// <summary>
/// A go-ahead held on an ask (KNOWUSE1a, D135 §2), as the service answers it: the person's yes for one act outside a
/// repository, named by its kind, where it lands and what it touches, with each session's request and the person's answer.
/// </summary>
/// <param name="Number">Its number on the ask, from 1: how a session and the person name it.</param>
/// <param name="Kind">write, release, push, sign-in or run, as the service spells it.</param>
/// <param name="On">Where it lands, as the service read it.</param>
/// <param name="Act">What it touches, in the words of the session that first asked it.</param>
public sealed record GoAheadView(int Number, string Kind, string On, string Act, IReadOnlyList<GoAheadRequestView> Asked)
{
    /// <summary>The person's answer, the latest where they answered twice; null while it waits on them.</summary>
    public GoAheadAnswerView? Answer { get; init; }

    /// <summary>The go-ahead whose words this one's shared where they could not tell, when that is why it was asked again.</summary>
    public int? Near { get; init; }

    /// <summary>The act in one line, as the service names it: <c>write on production: "dashboard configuration"</c>.</summary>
    public string Named => $"{Kind} on {On}: \"{Act}\"";
}

/// <summary>
/// The terminal's door onto a go-ahead (KNOWUSE1a, D50): <c>daoris-driver ask --go-ahead &lt;id&gt; &lt;n&gt; approve|refuse ["…"]</c>,
/// read here, in the library, so its words are held by a test.
/// </summary>
public static class GoAheadCommand
{
    /// <summary>
    /// What follows <c>--go-ahead &lt;id&gt;</c>: the go-ahead's number, <c>approve</c> or <c>refuse</c>, then the person's
    /// words, if any, as one sentence. Null with what was not understood.
    /// </summary>
    public static (int Number, bool Approved, string? Words)? Read(IReadOnlyList<string> rest, out string? problem)
    {
        problem = null;
        if (rest.Count < 2 || !int.TryParse(rest[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            problem = "--go-ahead names the ask, then the go-ahead's number and approve or refuse: --go-ahead <id> <n> approve|refuse [\"…\"]";
            return null;
        }

        bool? approved = rest[1] switch { "approve" => true, "refuse" => false, _ => null };
        if (approved is null)
        {
            problem = $"a go-ahead is answered `approve` or `refuse`, not `{rest[1]}`";
            return null;
        }

        var words = string.Join(' ', rest.Skip(2)).Trim();
        return (number, approved.Value, words.Length == 0 ? null : words);
    }
}

/// <summary>
/// How the go-aheads on an ask are written into an instruction (KNOWUSE1a, D135 §2): beneath the quest and the person's
/// words, each by its number with what became of it, the person's words on an answer quoted verbatim, and bounded, saying
/// what was left out.
/// </summary>
/// <remarks>
/// <b>Why that size</b>: the same command line the person's words share (<see cref="AskWordsText"/>). The service bounds
/// what an act touches at <see cref="ActLimit"/> characters and the person's words at 2,000, so the bound keeps at least
/// three whole; past it, the rest are named by number, with the ask's page as where each is whole.
/// </remarks>
public static class GoAheadsText
{
    /// <summary>The most characters of the go-aheads an instruction carries.</summary>
    public const int Limit = 8_000;

    /// <summary>The most characters of what an act touches, a copy of the service's <c>GoAheads.ActLimit</c>: the two share no code.</summary>
    public const int ActLimit = 300;

    /// <summary>
    /// The go-aheads beneath a quest: a heading saying not to ask any again and what each state means, then each by its
    /// number. Empty where the ask holds none or the service answered none, so the instruction reads as it did.
    /// </summary>
    internal static string Beneath(AskWords? words)
    {
        if (words?.GoAheads is not { Count: > 0 } held) return "";

        var text = new StringBuilder("\n");
        text.Append($"Go-aheads the person was asked for on ask `#{words.Ask}`, one per act: do not ask for one of these again. ")
            .Append("An approved act is yours to do as approved and no further; a refused one is not to be done; one still ")
            .Append("waiting is already before the person, so name it by its number rather than listing it as a new question.\n");

        var used = 0;
        var shown = 0;
        foreach (var goAhead in held)
        {
            var item = Item(goAhead);
            if (shown > 0 && used + item.Length > Limit) break;
            used += item.Length;
            shown++;
            text.Append(item);
        }

        if (shown < held.Count)
        {
            text.Append($"\n- Go-aheads {held[shown].Number} to {held[^1].Number} are left out here to keep this instruction ")
                .Append("bounded: the ask's page shows each, and none is to be asked again.\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// The prompt an answer resumes its own conversation with (ANSWER1a, D131 §1): the person's answer, verbatim, then the
    /// answers to the go-aheads this session asked, which its conversation was not handed at its start. The answer alone
    /// where none of them is answered, or the ask's go-aheads were not read.
    /// </summary>
    public static string Resumed(string answer, AskWords? words, string session)
    {
        var answered = (words?.GoAheads ?? [])
            .Where(goAhead => goAhead.Answer is not null && goAhead.Asked.Any(request => request.Session == session))
            .ToList();
        if (answered.Count == 0) return answer;

        var text = new StringBuilder(answer).Append("\n\n");
        text.Append("The person has also answered the go-aheads you asked on this ask:\n");
        foreach (var goAhead in answered) text.Append(Item(goAhead));
        return text.ToString();
    }

    /// <summary>One go-ahead as an item: its act, then what became of it, the person's words on an answer quoted line by line.</summary>
    private static string Item(GoAheadView goAhead)
    {
        var act = goAhead.Act.Length > ActLimit ? goAhead.Act[..ActLimit] + "…" : goAhead.Act;
        var head = $"\n- Go-ahead {goAhead.Number}, {goAhead.Kind} on {goAhead.On}: \"{act.ReplaceLineEndings(" ")}\"";
        if (goAhead.Answer is not { } answer)
        {
            var first = goAhead.Asked.Count > 0 ? $", first asked {When(goAhead.Asked[0].At)}" : "";
            return $"{head} — still waiting on the person{first}.\n";
        }

        var said = answer.Approved ? "approved" : "refused";
        if (answer.Words is not { Length: > 0 } words) return $"{head} — {said} {When(answer.At)}.\n";
        var quoted = string.Join("\n", words.ReplaceLineEndings("\n").Split('\n').Select(line => line.Length == 0 ? "  >" : $"  > {line}"));
        return $"{head} — {said} {When(answer.At)}, saying:\n\n{quoted}\n";
    }

    /// <summary>A moment as the instruction says it: to the minute, in UTC, the same on every machine.</summary>
    private static string When(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
