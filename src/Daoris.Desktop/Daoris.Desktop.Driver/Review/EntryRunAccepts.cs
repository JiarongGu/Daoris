namespace Daoris.Driver;

/// <summary>
/// An entry's one Accept across several runs is not atomic: each run's word is kept as the node door keeps it, a run whose review stands on
/// another record than the one drawn is refused <c>moved</c> alone, and the others go on. The answer says which accepted, which moved and
/// which failed.
/// </summary>
public static class EntryRunAccepts
{
    /// <summary>What a part refused <c>moved</c> says: the revision its review stands on now.</summary>
    public static string MovedSaid(string session, string standing) =>
        $"Not accepted (moved): session {session}'s work now reads revision {ReviewMoved.Short(standing.Length > 0 ? standing : null)}; read it again.";

    /// <summary>The entry's sentence over its parts' outcomes: one per outcome, each naming its sessions.</summary>
    public static string Said(IReadOnlyList<EntryPartLanding> parts)
    {
        string? Named(string what, params string[] outcomes) =>
            parts.Where(part => outcomes.Contains(part.Outcome)).Select(part => part.Session).ToList() is { Count: > 0 } named
                ? $"{what}: {string.Join(", ", named)}" : null;

        var said = new[]
        {
            Named("Landed", EntryPartOutcome.Landed, EntryPartOutcome.BroughtInto),
            Named("Accepted, landing at Daoris's next pass", EntryPartOutcome.Accepted),
            Named("Moved since you read them, so not accepted", EntryPartOutcome.Moved),
            Named("Failed", EntryPartOutcome.Refused),
        }.OfType<string>().ToList();
        return said.Count == 0 ? "Nothing was landed." : string.Join("; ", said) + ".";
    }
}
