namespace Daoris.Knowledge;

/// <summary>Permits or withholds entries by their provenance and origin.</summary>
public sealed class DisclosurePolicy(bool sharing, IReadOnlySet<string>? shareableRepositories = null) : IDisclosurePolicy
{
    /// <summary>Nothing is leaving, so nothing is withheld. The default, and the local mode.</summary>
    public static IDisclosurePolicy LocalOnly { get; } = new DisclosurePolicy(sharing: false);

    /// <summary>
    /// Shared mode. A repository shares only if it is named — <b>silence means keep it local</b>,
    /// because the cost is asymmetric: over-sharing is a disclosure and under-sharing is an
    /// inconvenience.
    /// </summary>
    public static IDisclosurePolicy Sharing(IReadOnlySet<string> repositories) =>
        new DisclosurePolicy(sharing: true, repositories);

    public bool MayLeaveMachine(KnowledgeEntry entry)
    {
        if (!sharing) return true;

        // Canonical content is public by construction — it is the canon, installed identically
        // everywhere — so it travels regardless of which repository it was read from.
        if (entry.Provenance == Provenance.Canonical) return true;

        return shareableRepositories?.Contains(entry.Repository) == true;
    }
}
