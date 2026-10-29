namespace Daoris.Driver;

/// <summary>
/// What the judge remembers between candidates: the known reds it inherits from, and the sightings it files (the integration design §4.5,
/// §5.3, §5.6: <c>integration_sightings</c> and the findings moved from <c>local/ci-findings.json</c>, once FABRIC-PIPELINE1e runs the
/// background check). The store waits, so this is a seam of its own, unwired: it remembers nothing, no failure is a known red and a sighting
/// is told to no one. A sighting is never a pass either way.
/// </summary>
internal interface IIntegrationMemory
{
    /// <summary>The open known red a failure's signature matches in the repository, with whether its 72 hours are past and the commit it was last seen failing on; null where none is open.</summary>
    KnownRed? KnownRedFor(string repository, FailureSignature signature);

    /// <summary>A failure that did not fail again on its gate's confirm run, filed on its unit with the candidate's tip and the confirm run's evidence.</summary>
    void Sighted(string repository, string tip, JudgedFailure failure, ConfirmReading? confirm);
}

/// <summary>The memory the judge reads and writes: none until the sightings' store is built and installs its own.</summary>
internal static class IntegrationMemory
{
    private sealed class Forgets : IIntegrationMemory
    {
        public KnownRed? KnownRedFor(string repository, FailureSignature signature) => null;

        public void Sighted(string repository, string tip, JudgedFailure failure, ConfirmReading? confirm)
        {
        }
    }

    internal static IIntegrationMemory Current { get; set; } = new Forgets();
}
