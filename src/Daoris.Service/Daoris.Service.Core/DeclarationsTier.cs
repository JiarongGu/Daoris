namespace Daoris.Knowledge;

/// <summary>A repository an ask's words point at, and the words that point there.</summary>
/// <param name="Repository">The repository proposed.</param>
/// <param name="Score">How strongly — a word the repository says it OWNS counts double.</param>
/// <param name="Matched">The evidence: the ask's words its declaration shares, in the ask's order.</param>
public sealed record DeclarationMatch(string Repository, int Score, IReadOnlyList<string> Matched);

/// <summary>
/// The intake's floor (D65 §1b): with no harness to read the ask, its words are still read against
/// what each repository DECLARED — its summary, what it owns, what it accepts — and the best overlaps
/// are proposed, with the words that matched as the evidence.
/// </summary>
/// <remarks>
/// <para><b>It proposes; it never publishes.</b> Word overlap cannot tell "the media config" from a
/// sentence that merely mentions media, and a quest published on a guess lands in a repository that
/// did not ask for it. A person, or an intake session, turns a proposal into a quest.</para>
///
/// <para><b>It is what `model-decoupling` requires, not a stopgap.</b> A model-backed feature must
/// still do its useful part with no model at all, and report which tier answered — the ask's record
/// says <i>by declarations only</i> whenever this is what ran.</para>
///
/// <para>Only what can be ASKED is a candidate: an adopted repository in the ask's own circle. Anything
/// else would be a proposal the exchange then refuses (D48 §4).</para>
/// </remarks>
public static class DeclarationsTier
{
    /// <summary>How many candidates a proposal names — enough to choose between, few enough to read.</summary>
    public const int Proposed = 3;

    /// <summary>
    /// A sentence's glue. Words that match everything match nothing, and a request is full of them —
    /// "please check whether this should use …" says nothing about whose problem it is.
    /// </summary>
    private static readonly HashSet<string> Common = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "are", "but", "not", "you", "all", "any", "can", "had", "her", "was", "one",
        "our", "out", "has", "him", "his", "how", "its", "may", "new", "now", "see", "two", "way", "who",
        "did", "get", "let", "put", "say", "she", "too", "use", "via", "per", "this", "that", "with",
        "have", "from", "they", "will", "would", "there", "their", "what", "about", "which", "when",
        "make", "like", "just", "know", "take", "into", "your", "some", "could", "them", "than", "then",
        "other", "only", "over", "also", "after", "should", "these", "those", "want", "need", "instead",
        "please", "check", "thing", "able", "being", "been", "were", "here", "where", "while", "does",
        "done", "each", "very", "such", "same", "more", "most", "much", "many", "well", "still", "even",
        "back", "again", "because", "before", "between", "whether", "without", "within", "using", "used",
    };

    /// <summary>
    /// Rank the circle's askable repositories by how much of <paramref name="sentence"/> their
    /// declarations share. Empty when nothing is shared — a proposal of nobody, said as such.
    /// </summary>
    public static IReadOnlyList<DeclarationMatch> Rank(
        string sentence, IEnumerable<Registration> registered, string? workspace)
    {
        var asked = Words(sentence);
        if (asked.Count == 0) return [];

        var ranked = new List<DeclarationMatch>();
        foreach (var registration in registered)
        {
            if (!registration.Adopted || !Workspaces.Same(registration.InWorkspace, workspace)) continue;

            var owned = Words(string.Join(" ", registration.Owns)).ToHashSet(StringComparer.Ordinal);
            var declared = Words(string.Join(
                " ", [registration.Summary ?? "", .. registration.Owns, .. registration.Accepts]))
                .ToHashSet(StringComparer.Ordinal);

            var matched = asked.Where(declared.Contains).ToList();
            if (matched.Count == 0) continue;

            ranked.Add(new DeclarationMatch(
                registration.Repository, matched.Sum(word => owned.Contains(word) ? 2 : 1), matched));
        }

        return ranked
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Matched.Count)
            .ThenBy(match => match.Repository, StringComparer.Ordinal)
            .Take(Proposed)
            .ToList();
    }

    /// <summary>The words worth matching on, each once, in the order they first appear.</summary>
    private static List<string> Words(string text) =>
        Text.Tokenize(text)
            .Where(word => !Common.Contains(word))
            .Select(Singular)
            .Where(word => !Common.Contains(word))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// A plural and its singular are one word to a person reading a declaration: "imports" is about
    /// "import". A trailing `s` only, on a word long enough to be sure — never "ss", which is not a
    /// plural, and never a stem cut deeper, which is where a stemmer starts to invent words.
    /// </summary>
    private static string Singular(string word) =>
        word.Length > 4 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1] : word;
}
