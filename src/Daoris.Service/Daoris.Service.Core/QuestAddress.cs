namespace Daoris.Knowledge;

/// <summary>
/// Whom a quest asks (D115 §2.2): a repository, or lanes inside one, spelled `repository:lane` or
/// `repository:lane+lane` at every door. Split once, by the exchange: the quest keeps `to` as the
/// repository, so everything keyed on a repository — the registry, the planner, the ledger, the scopes,
/// the sync — goes on reading one, and the lanes ride beside it.
/// </summary>
/// <param name="Repository">The repository, exactly as the address spelled it before any colon.</param>
/// <param name="Lanes">The lanes it named, trimmed, blanks dropped, in the order given.</param>
/// <param name="Named">Whether the address had a colon at all — one naming no lane after it is a mistake to say, not a quest to the whole repository.</param>
public sealed record QuestAddress(string Repository, IReadOnlyList<string> Lanes, bool Named)
{
    /// <summary>The address as a door received it.</summary>
    public static QuestAddress Parse(string? to)
    {
        var text = to ?? "";
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0) return new(text, [], Named: false);

        var lanes = text[(colon + 1)..]
            .Split('+')
            .Select(lane => lane.Trim())
            .Where(lane => lane.Length > 0)
            .ToList();
        return new(text[..colon], lanes, Named: true);
    }

    /// <summary>
    /// Whether <paramref name="id"/> is a lane's: lower-case ASCII letters, digits and dashes, starting
    /// with a letter — one word of a known alphabet, so an address splits one way only. The CLI's
    /// `lanes.ts` and the merge tool spell the same alphabet.
    /// </summary>
    public static bool IsLaneId(string? id) =>
        id is { Length: > 0 }
        && id[0] is >= 'a' and <= 'z'
        && id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    /// <summary>A quest's address as a person reads it: the repository, and its lanes after a colon.</summary>
    public static string Spell(string repository, IReadOnlyList<string> lanes) =>
        lanes.Count == 0 ? repository : $"{repository}:{string.Join('+', lanes)}";
}
