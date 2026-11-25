namespace Daoris.Driver;

/// <summary>The ports an instance may be given on this machine: <paramref name="Count"/> numbers from <paramref name="First"/>.</summary>
public sealed record PortPool(int First, int Count);

/// <summary>
/// One running copy of an environment on this machine. The shared instance (<see cref="Scope"/> null) is the one agents who only use the
/// environment lease, at the line's version; an instance of its own is keyed by the branch of the team whose work changes the environment.
/// </summary>
/// <param name="Id">The environment's id, and for an instance of its own <c>@</c> and its branch.</param>
/// <param name="Version">The <see cref="EnvironmentVersion"/> it runs at.</param>
/// <param name="Ports">The number each of the environment's named ports has.</param>
/// <param name="Grace">How long it is kept once no lease holds it, from its declaration.</param>
/// <param name="IdleSince">When its last lease went; null while one holds it.</param>
public sealed record EnvironmentInstance(string Id, string Environment, string? Scope, string Version, IReadOnlyDictionary<string, int> Ports, TimeSpan Grace, DateTimeOffset? IdleSince)
{
    /// <summary>The instance of the line: the one every session that only uses the environment leases.</summary>
    public bool Shared => Scope is null;
}

/// <summary>A session's hold on an instance: the right to use it and to be moved with it, never a copy of it.</summary>
public sealed record EnvironmentLease(string Session, string Environment, string InstanceId);

/// <summary>What a session asks of an environment: its inputs' version at its own branch, against the version at the line.</summary>
/// <param name="Branch">The session's branch: the key of the instance a changing session leases.</param>
/// <param name="LineVersion">The environment's version at the line's tip.</param>
/// <param name="BranchVersion">The environment's version at the session's branch.</param>
public sealed record EnvironmentUse(string Session, EnvironmentSpec Spec, string Branch, string LineVersion, string BranchVersion)
{
    /// <summary>Whether the session's own work changes the environment: its inputs are not the line's.</summary>
    public bool Changes => !string.Equals(BranchVersion, LineVersion, StringComparison.Ordinal);
}

/// <summary>Why no instance could be leased.</summary>
public enum EnvironmentRefusal
{
    /// <summary>A new instance would pass what the machine's admission holds of them.</summary>
    Instances,

    /// <summary>The port pool has too few numbers left for the environment's ports.</summary>
    Ports,
}

/// <summary>A lease asked: the ledger after it, the instance leased, whether it must be started, or the refusal (the ledger then stays as it stands).</summary>
/// <param name="Starts">The instance is new or moved to a new version, so the caller runs it before any session uses it.</param>
public sealed record EnvironmentLeaseAnswer(EnvironmentLedger Ledger, EnvironmentInstance? Instance, bool Starts, EnvironmentRefusal? Refusal);

/// <summary>An instance sweep: the ledger after, and the instances whose grace ended, which the caller stops.</summary>
public sealed record EnvironmentSweep(EnvironmentLedger Ledger, IReadOnlyList<EnvironmentInstance> Stopped);

/// <summary>
/// The running instances of this machine's environments and who leases them. Pure and immutable: each act answers the ledger
/// after it, and the caller keeps it. A session whose inputs equal the line's leases the one shared instance of an environment; one whose
/// branch changes them leases the instance of that branch, which no session of another branch is moved onto, and which follows its
/// branch's version; an instance no lease holds is stopped once its grace has passed. The machine's admission counts <see cref="Count"/>,
/// the instances, and never the leases.
/// </summary>
public sealed record EnvironmentLedger(IReadOnlyList<EnvironmentInstance> Instances, IReadOnlyList<EnvironmentLease> Leases, PortPool Pool)
{
    /// <summary>A ledger with nothing running, giving ports from <paramref name="pool"/>.</summary>
    public static EnvironmentLedger Empty(PortPool pool) => new([], [], pool);

    /// <summary>How many instances run: what the admission counts.</summary>
    public int Count => Instances.Count;

    /// <summary>The sessions that lease <paramref name="instanceId"/>, in the order they took it.</summary>
    public IReadOnlyList<string> Holders(string instanceId) => Leases.Where(each => each.InstanceId == instanceId).Select(each => each.Session).ToList();

    /// <summary>
    /// Leases the instance <paramref name="use"/> is owed: the shared one at the line's version where its inputs are the line's, else the
    /// one of its branch, started at the branch's version. A session holds one lease for an environment, the last it asked. The shared
    /// instance is leased as it stands; moving it to a new line version is the landing's.
    /// </summary>
    /// <param name="capacity">How many instances the admission holds; a new one past it is refused, a lease of a running one never is.</param>
    public EnvironmentLeaseAnswer Lease(EnvironmentUse use, DateTimeOffset now, int capacity)
    {
        var spec = use.Spec;
        var scope = use.Changes ? use.Branch : null;
        var version = use.Changes ? use.BranchVersion : use.LineVersion;
        var released = Without(each => each.Session == use.Session && each.Environment == spec.Id, now);
        var found = released.Instances.FirstOrDefault(each => each.Environment == spec.Id && each.Scope == scope);

        if (found is not null)
        {
            var moved = scope is not null && found.Version != version;
            var kept = moved || found.IdleSince is not null ? found with { Version = version, IdleSince = null } : found;
            return released.Taken(use, kept, moved);
        }

        if (released.Count >= capacity) return new(this, null, false, EnvironmentRefusal.Instances);
        if (released.Ports(spec.Ports) is not { } ports) return new(this, null, false, EnvironmentRefusal.Ports);
        var fresh = new EnvironmentInstance(scope is null ? spec.Id : $"{spec.Id}@{scope}", spec.Id, scope, version, ports, spec.Grace, null);
        return released.Taken(use, fresh, true);
    }

    /// <summary>Lets go every lease <paramref name="session"/> holds; an instance it is last to hold begins its grace at <paramref name="now"/>.</summary>
    public EnvironmentLedger Release(string session, DateTimeOffset now) => Without(each => each.Session == session, now);

    /// <summary>Takes out each instance no lease holds whose grace ended by <paramref name="now"/>, and gives its ports back.</summary>
    public EnvironmentSweep Sweep(DateTimeOffset now)
    {
        var stopped = Instances.Where(each => each.IdleSince is { } idle && idle + each.Grace <= now && !Leases.Any(lease => lease.InstanceId == each.Id)).ToList();
        return stopped.Count == 0 ? new(this, []) : new(this with { Instances = Instances.Except(stopped).ToList() }, stopped);
    }

    private EnvironmentLeaseAnswer Taken(EnvironmentUse use, EnvironmentInstance instance, bool starts)
    {
        var instances = Instances.Any(each => each.Id == instance.Id)
            ? Instances.Select(each => each.Id == instance.Id ? instance : each).ToList()
            : Instances.Append(instance).ToList();
        var leases = Leases.Append(new EnvironmentLease(use.Session, use.Spec.Id, instance.Id)).ToList();
        return new(this with { Instances = instances, Leases = leases }, instance, starts, null);
    }

    private EnvironmentLedger Without(Func<EnvironmentLease, bool> drop, DateTimeOffset now)
    {
        var dropped = Leases.Where(drop).ToList();
        if (dropped.Count == 0) return this;
        var leases = Leases.Except(dropped).ToList();
        var emptied = dropped.Select(each => each.InstanceId).Distinct().Where(id => !leases.Any(each => each.InstanceId == id)).ToHashSet();
        var instances = Instances.Select(each => emptied.Contains(each.Id) ? each with { IdleSince = now } : each).ToList();
        return this with { Instances = instances, Leases = leases };
    }

    /// <summary>The lowest free numbers of the pool for <paramref name="names"/>, in order; null where the pool has too few.</summary>
    private IReadOnlyDictionary<string, int>? Ports(IReadOnlyList<string> names)
    {
        var used = Instances.SelectMany(each => each.Ports.Values).ToHashSet();
        var free = Enumerable.Range(Pool.First, Pool.Count).Where(each => !used.Contains(each)).Take(names.Count).ToList();
        return free.Count < names.Count ? null : names.Zip(free).ToDictionary(each => each.First, each => each.Second);
    }
}
