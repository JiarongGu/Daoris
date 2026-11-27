namespace Daoris.Driver;

/// <summary>
/// The doors of an ask's pins over a repository's own checkout and git's files: what the ask landed as on a producer's line (the ref the ask
/// run's <c>land</c> keeps, <see cref="TrainRefs.LandedAsync"/>), a consumer's lock as its ask branch holds it, and the pin's move onto that
/// branch (<see cref="TrainPins"/>). The reads start no more than a ref's and a file's read.
/// </summary>
public sealed class AskPinDoors : IAskPinDoors
{
    public Task<string?> LandedAsync(RepoView producer, string ask, CancellationToken ct) => TrainRefs.LandedAsync(producer.Root!, ask, ct);

    public async Task<LockText> LockAsync(RepoView consumer, string ask, CancellationToken ct) =>
        await MachinePorts.Repository.CommitOfAsync(consumer.Root!, $"refs/heads/{TrainBranch.Of(ask)}", ct).ConfigureAwait(false) is { } tip
            ? new LockText(true, await MachinePorts.Repository.TextAtAsync(consumer.Root!, tip, FamilyLock.FileName, ct).ConfigureAwait(false))
            : new LockText(false, null);

    public Task<TrainPinned> WriteAsync(RepoView consumer, string ask, string producer, string commit, CancellationToken ct) =>
        TrainPins.WriteAsync(consumer.Root!, ask, producer, commit, ct);
}
