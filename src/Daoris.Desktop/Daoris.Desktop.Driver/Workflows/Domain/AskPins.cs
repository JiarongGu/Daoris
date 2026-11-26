namespace Daoris.Driver;

/// <summary>The ask run of a repository an ask's plan gives a part to: its ask and its repository.</summary>
public sealed record AskRunOf(string Ask, string Repository);

/// <summary>Where a consumer's pin of a producer stands for one ask.</summary>
public enum AskPinStates
{
    /// <summary>The producer's part has not landed on its line, so there is no commit to pin.</summary>
    Waiting,

    /// <summary>The producer's part landed, and the consumer's ask branch pins another commit or none.</summary>
    Owed,

    /// <summary>The consumer's ask branch pins the commit the producer's part landed.</summary>
    Pinned,

    /// <summary>The consumer's lock on its ask branch is no lock, or the branch is not read: it is left as it is.</summary>
    Unreadable,
}

/// <summary>
/// One edge of a family as one ask's consumer part owes it: <paramref name="Landed"/> is the commit the producer's part landed as on its line,
/// <paramref name="Pinned"/> what the consumer's ask branch pins of the producer now, and <paramref name="Readable"/> false where its lock is no lock.
/// </summary>
public sealed record AskPin(PinEdge Edge, string? Landed, string? Pinned, bool Readable)
{
    public AskPinStates State =>
        !Readable ? AskPinStates.Unreadable
        : Landed is null ? AskPinStates.Waiting
        : string.Equals(Landed, Pinned, StringComparison.OrdinalIgnoreCase) ? AskPinStates.Pinned
        : AskPinStates.Owed;

    /// <summary>Whether the consumer's part still waits on this pin: it lands with the pin moved, so any state but pinned holds it.</summary>
    public bool Holds => State != AskPinStates.Pinned;
}

/// <summary>What the pins of an ask read and write: the commit a repository's part landed as, a consumer's lock on its ask branch, and the pin's move.</summary>
public interface IAskPinDoors
{
    /// <summary>The commit the ask's branch landed as on <paramref name="producer"/>'s line; null where it has not landed.</summary>
    Task<string?> LandedAsync(RepoView producer, string ask, CancellationToken ct);

    /// <summary>The consumer's lock as its ask branch holds it: not read where the branch is not there.</summary>
    Task<LockText> LockAsync(RepoView consumer, string ask, CancellationToken ct);

    /// <summary>Moves the consumer's pin of <paramref name="producer"/> to <paramref name="commit"/> on its ask branch, as a change of its own.</summary>
    Task<TrainPinned> WriteAsync(RepoView consumer, string ask, string producer, string commit, CancellationToken ct);
}

/// <summary>
/// The pins an ask across repositories owes one of its consumers: for each producer of the consumer's family, one the ask's plan
/// gives a part, the commit that part landed as on the producer's line, against what the consumer's ask branch pins. A family is a
/// <c>daoris.family.json</c> in the folder the members' checkouts share; a producer is a member the consumer's own <c>uses</c> names, of a lower layer,
/// and not denied. Nothing here names a kind of repository.
/// </summary>
public static class AskPins
{
    /// <param name="partRepositories">The repositories the ask's plan gives a part to.</param>
    public static async Task<IReadOnlyList<AskPin>> ReadAsync(
        IReadOnlyList<RepoView> registry, string consumer, string ask, IEnumerable<string> partRepositories, IAskPinDoors doors, CancellationToken ct)
    {
        var row = registry.FirstOrDefault(each => each.Root is { Length: > 0 } && FamilyFanout.Same(each.Repository, consumer));
        if (row is null || FamilyFanout.Read(Path.Combine(FamilyFanout.ParentOf(row.Root!), FamilyDeclaration.FileName)) is not { } family) return [];
        var share = FamilyFanout.ParentOf(row.Root!);
        var members = registry.Where(each => each.Root is { Length: > 0 } && FamilyFanout.ParentOf(each.Root!).Equals(share, StringComparison.OrdinalIgnoreCase)
            && family.Members.Any(name => FamilyFanout.Same(name, each.Repository))).ToList();
        var pins = new List<AskPin>();
        foreach (var name in partRepositories.Where(each => !FamilyFanout.Same(each, consumer)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (members.FirstOrDefault(each => FamilyFanout.Same(each.Repository, name)) is not { } producer) continue;
            var edge = family.ConsumersOf(producer.Repository, each => FamilyFanout.UsesOf(members.FirstOrDefault(member => FamilyFanout.Same(member.Repository, each))))
                .FirstOrDefault(each => FamilyFanout.Same(each.Consumer, consumer));
            if (edge is null) continue;
            var landed = await doors.LandedAsync(producer, ask, ct).ConfigureAwait(false);
            var (readable, pinned) = FamilyFanout.PinIn(await doors.LockAsync(row, ask, ct).ConfigureAwait(false), producer.Repository);
            pins.Add(new AskPin(edge, landed, pinned, readable));
        }

        return pins;
    }
}
