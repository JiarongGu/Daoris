namespace Daoris.Driver;

/// <summary>
/// The records the integration door reads (INTEGRATE1e part e1): the machine's landings and its session branches, standing and traces.
/// A seam, so the door's rules are judged over records a test holds; <see cref="HomeIntegrationRecords"/> reads a home's.
/// </summary>
public interface IIntegrationRecords
{
    /// <summary>Every landing entry, standing and traces, in the order they landed (<see cref="LandedBranches.Entries"/>).</summary>
    IReadOnlyList<LandedBranch> Landings();

    /// <summary>Every session branch entry, standing and traces (<see cref="SessionBranches.Entries"/>).</summary>
    IReadOnlyList<GrownBranch> SessionBranches();
}

/// <summary>A home's own records, read afresh at each ask.</summary>
public sealed class HomeIntegrationRecords(string home) : IIntegrationRecords
{
    public IReadOnlyList<LandedBranch> Landings() => new LandedBranches(home).Entries();

    public IReadOnlyList<GrownBranch> SessionBranches() => new SessionBranches(home).Entries();
}

/// <summary>
/// Where admitted requests are kept (INTEGRATE1e): one open request per repository and branch, so no landing is requested twice.
/// <see cref="IntegrationStore"/> keeps them in the desktop store (part e2), judged by the store's own index in the write, so two doors at
/// once, in one process or two, keep one.
/// </summary>
public interface IIntegrationRequestStore
{
    /// <summary>The request open for that branch in that repository, at whatever tip; null where none is.</summary>
    IntegrationRequest? OpenFor(string repository, string branch);

    /// <summary>
    /// Keep <paramref name="request"/> where no request is open for its repository and branch, judged with the write; else keep nothing and
    /// return the one open.
    /// </summary>
    IntegrationRequest? Add(IntegrationRequest request);
}
