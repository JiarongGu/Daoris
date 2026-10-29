namespace Daoris.Driver;

/// <summary>
/// What the quest's and the ask's review doors say and log (REVIEWENV1c, REVIEWENV1j; D94): the names of the environments a rule declares,
/// told as the review rule's own sentences tell them, and the person's verdict as the machine log's <c>review.verdict</c> line. Work
/// orders owns them, its two doors being their lowest readers; the review's <see cref="ReviewRules.Either"/> and
/// <see cref="ReviewLines.Verdict"/> answer through them.
/// </summary>
internal static class ReviewDoorWords
{
    /// <summary>Names in backticks, the last after <c>or</c>: <c>a</c>, <c>a or b</c>, <c>a, b or c</c>; the review choice's doors say them so too.</summary>
    internal static string Either(IReadOnlyList<string> names)
    {
        var ticked = names.Select(name => $"`{name}`").ToList();
        return ticked.Count <= 1 ? string.Concat(ticked) : $"{string.Join(", ", ticked.Take(ticked.Count - 1))} or {ticked[^1]}";
    }

    /// <summary>The person's verdict, and the door it was given at: <c>review.verdict</c>, codes and ids only (D94).</summary>
    internal static LandingLine Verdict(string quest, string said, string door) =>
        new("review.verdict", [("quest", quest), ("said", said), ("door", door)]);
}
