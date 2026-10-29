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
/// Where admitted requests are kept (INTEGRATE1e): one open request per repository and branch, so no landing is requested twice. Part
/// e2's store family implements it on disk; until then <see cref="InMemoryIntegrationRequests"/> does.
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

/// <summary>The request store in memory (INTEGRATE1e part e1): what the door is judged over until part e2 keeps requests on disk.</summary>
public sealed class InMemoryIntegrationRequests : IIntegrationRequestStore
{
    private readonly object _gate = new();
    private readonly List<IntegrationRequest> _open = [];

    /// <summary>Every open request, in the order admitted.</summary>
    public IReadOnlyList<IntegrationRequest> Open()
    {
        lock (_gate) return [.. _open];
    }

    public IntegrationRequest? OpenFor(string repository, string branch)
    {
        lock (_gate) return _open.FirstOrDefault(each => SessionBranches.SameBranch(each.Repository, each.Branch, repository, branch));
    }

    public IntegrationRequest? Add(IntegrationRequest request)
    {
        lock (_gate)
        {
            if (_open.FirstOrDefault(each => SessionBranches.SameBranch(each.Repository, each.Branch, request.Repository, request.Branch)) is { } open)
            {
                return open;
            }

            _open.Add(request);
            return null;
        }
    }

    /// <summary>The request of that id is no longer open (promoted, handed back or dropped, in the parts that do those); whether it was.</summary>
    public bool Close(string id)
    {
        lock (_gate) return _open.RemoveAll(each => string.Equals(each.Id, id, StringComparison.Ordinal)) > 0;
    }
}
