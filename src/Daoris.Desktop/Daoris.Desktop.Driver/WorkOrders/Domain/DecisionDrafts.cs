using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A decision drafted from a design pass's closed question once it is answered, for the person to keep: the repository's next number,
/// the choice and what it rejected, each as the comparison worded what yes and no change. The answer is a choice, never words, so
/// nothing of the person is written as theirs; the draft is offered, and the person keeps, edits or drops it.
/// </summary>
/// <param name="Number">The decision's id, <c>D</c> and the next number.</param>
public sealed record DecisionDraft(string Number, string Question, string Chosen, string Rejected, string Text);

/// <summary>Drafts a design question's answer as a decision (<see cref="DecisionDraft"/>). Pure.</summary>
public static partial class DecisionDrafts
{
    /// <summary>The next decision number after the highest <c>D&lt;n&gt;.md</c> among <paramref name="names"/>, the repository's decisions folder's files.</summary>
    public static int Next(IEnumerable<string> names) =>
        names.Select(name => Numbered().Match(Path.GetFileName(name))).Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max() + 1;

    /// <summary>
    /// The draft for <paramref name="question"/> answered <paramref name="yes"/>: the chosen side's sentence as its title and decision, the
    /// comparison's <paramref name="route"/> as why, and the other side as what it rejected.
    /// </summary>
    public static DecisionDraft Of(DesignQuestion question, bool yes, int number, string route, DateOnly on)
    {
        var (chosen, rejected) = yes ? (question.Yes.Trim(), question.No.Trim()) : (question.No.Trim(), question.Yes.Trim());
        var id = $"D{number}";
        var text = $"## {id} — {chosen.TrimEnd('.')} ({on:yyyy-MM-dd})\n\n"
            + $"**Decision.** {chosen}\n\n"
            + $"**Why.** {route.Trim()} (the design pass's question {question.Key}: {question.Question.Trim()}; see {question.Where.Trim()})\n\n"
            + $"**Rejected:** {Lower(rejected)}\n";
        return new DecisionDraft(id, question.Key, chosen, rejected, text);
    }

    private static string Lower(string sentence) => sentence.Length > 1 && char.IsUpper(sentence[0]) && !char.IsUpper(sentence[1])
        ? char.ToLowerInvariant(sentence[0]) + sentence[1..]
        : sentence;

    [GeneratedRegex(@"^D(\d+)\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex Numbered();
}
