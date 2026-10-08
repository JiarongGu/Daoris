namespace Daoris.Knowledge;

/// <summary>A repository an ask's words point at, and the words that point there.</summary>
/// <param name="Repository">The repository proposed.</param>
/// <param name="Score">
/// How strongly — a word the repository says it OWNS counts double. A repository the sentence NAMES
/// scores above every overlap in the same proposal, the first named highest (ASKNAME1).
/// </param>
/// <param name="Matched">
/// The evidence: the ask's words its declaration shares, in the ask's order — or, for a repository the
/// sentence names, that name alone.
/// </param>
public sealed record DeclarationMatch(string Repository, int Score, IReadOnlyList<string> Matched);

/// <summary>
/// The intake's floor (D65 §1b): with no harness to read the ask, its words are still read against
/// what each repository DECLARED — its summary, what it owns, what it accepts — and the best overlaps
/// are proposed, with the words that matched as the evidence. A repository the sentence names comes
/// before them all.
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
/// <para>Only a DECLARATION is matched on words: an adopted repository in the ask's own circle.
/// Another circle would be a proposal the exchange refuses (D48 §4). A declaration is the manifest's
/// (D34).</para>
///
/// <para><b>A repository the sentence NAMES is the exception</b> (ASKNAME1): one of the ask's own
/// circle that can be asked (<see cref="Registration.Addressable"/>), adopted or not, whose exact name
/// the sentence holds whole is proposed first, in the order the sentence names them, with its name as
/// the evidence. An ask that opens <i>In release-infra: …</i> has said whose it is. Since D70 a
/// repository registered here with a root and no manifest can be asked, but it declares nothing, so
/// words alone could never reach it: on the owner's install a different, adopted repository was
/// proposed alone on words, and the one word that settles the receiver counted for nothing. A name is
/// still a proposal, never a publish: a sentence can name a repository it only mentions. Another
/// circle's name is never proposed (D48 §4), nor one nobody here can ask, since the exchange would
/// refuse both.</para>
///
/// <para>Whole means a longer name or word that contains it is not it: <c>portal</c> is not named by
/// <c>portal-ui</c>, nor <c>orders-db</c> by <c>orders-db-v2</c>. Beside an ideograph there is no edge
/// to find, since those scripts put no space between words, so there a name is named as written and
/// <c>媒体服务</c> inside <c>媒体服务器</c> counts — a gap this states rather than closes.</para>
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
    /// Rank the circle's askable repositories: those <paramref name="sentence"/> names first, in its
    /// order, then by how much of it their declarations share. Empty when nothing is named or shared —
    /// a proposal of nobody, said as such.
    /// </summary>
    public static IReadOnlyList<DeclarationMatch> Rank(
        string sentence, IEnumerable<Registration> registered, string? workspace)
    {
        var circle = registered.Where(registration => Workspaces.Same(registration.InWorkspace, workspace)).ToList();
        var named = Named(sentence, circle);
        var asked = Words(sentence);

        var overlaps = new List<DeclarationMatch>();
        foreach (var registration in circle)
        {
            if (!registration.Adopted || named.Contains(registration.Repository)) continue;

            var owned = Words(string.Join(" ", registration.Owns)).ToHashSet(StringComparer.Ordinal);
            var declared = Words(string.Join(
                " ", [registration.Summary ?? "", .. registration.Owns, .. registration.Accepts]))
                .ToHashSet(StringComparer.Ordinal);

            var matched = asked.Where(declared.Contains).ToList();
            if (matched.Count == 0) continue;

            overlaps.Add(new DeclarationMatch(
                registration.Repository, matched.Sum(word => owned.Contains(word) ? 2 : 1), matched));
        }

        // A named repository scores above every overlap, the first named highest, so the stored list
        // reads in the same order by its scores as by its place — no new field on a stored shape.
        var above = overlaps.Count == 0 ? 0 : overlaps.Max(match => match.Score);
        var first = named.Select((name, at) => new DeclarationMatch(name, above + named.Count - at, [name]));

        return first
            .Concat(overlaps
                .OrderByDescending(match => match.Score)
                .ThenByDescending(match => match.Matched.Count)
                .ThenBy(match => match.Repository, StringComparer.Ordinal))
            .Take(Proposed)
            .ToList();
    }

    /// <summary>
    /// The circle's askable repositories <paramref name="sentence"/> names whole, in the order it first
    /// names each; two named at one place, the longer first, as the more particular.
    /// </summary>
    private static List<string> Named(string sentence, IEnumerable<Registration> circle) =>
        circle
            .Where(registration => registration.Addressable)
            .Select(registration => (Name: registration.Repository, At: NamedAt(sentence, registration.Repository)))
            .Where(named => named.At >= 0)
            .OrderBy(named => named.At)
            .ThenByDescending(named => named.Name.Length)
            .ThenBy(named => named.Name, StringComparer.Ordinal)
            .Select(named => named.Name)
            .ToList();

    /// <summary>Where <paramref name="sentence"/> first holds <paramref name="name"/> whole, in any case; -1 when nowhere.</summary>
    private static int NamedAt(string sentence, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return -1;

        for (var at = sentence.IndexOf(name, StringComparison.OrdinalIgnoreCase);
             at >= 0;
             at = sentence.IndexOf(name, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (!Continues(sentence, at - 1, -1, name[0]) && !Continues(sentence, at + name.Length, 1, name[^1])) return at;
        }

        return -1;
    }

    /// <summary>
    /// Whether the character at <paramref name="at"/>, beside a name's <paramref name="edge"/>, makes the
    /// name part of a longer word or name: a letter or digit does, and so does a joiner a name is spelled
    /// with (<c>-</c>, <c>_</c>, <c>.</c>) when a letter or digit follows it outward — <c>portal-ui</c>,
    /// <c>v2-storefront</c>. A joiner with nothing after it ends a sentence or a clause, not a name.
    /// Beside an ideograph nothing does: those scripts put no space between words.
    /// </summary>
    private static bool Continues(string text, int at, int outward, char edge)
    {
        if (at < 0 || at >= text.Length) return false;

        var beside = text[at];
        if (char.IsLetterOrDigit(beside)) return !Text.IsIdeograph(beside) && !Text.IsIdeograph(edge);
        if (beside is not ('-' or '_' or '.')) return false;

        var beyond = at + outward;
        return beyond >= 0 && beyond < text.Length && char.IsLetterOrDigit(text[beyond]);
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
