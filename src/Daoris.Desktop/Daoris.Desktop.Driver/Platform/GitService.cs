using System.Collections.Concurrent;
using Daoris.Ports.Processes;

namespace Daoris.Driver;

/// <summary>Whose a git ask is by how soon it must go: a person's, a step's (the run-step consumer, the opinion gate, a check), or the background's.</summary>
internal enum GitAskClass
{
    Person,
    Step,
    Background,
}

/// <summary>One ask of <see cref="GitService"/>, kept in memory while it is open: never written, never an entry of the bus's log.</summary>
/// <param name="Id">What it is answered by.</param>
/// <param name="Repository">The repository's common directory, or the folder where it has none.</param>
/// <param name="Kind">Its read kind (<see cref="GitReadKinds"/>).</param>
/// <param name="Asker">The caller its flow names (<see cref="GitAskers"/>).</param>
/// <param name="Deadline">How long the whole ask may take: its place's wait and then its run's bound.</param>
internal sealed record GitServiceAsk(long Id, string Repository, string Kind, string Asker, GitAskClass Class, TimeSpan Deadline);

/// <summary>
/// The one door every git of the desktop is asked of, wrapping <see cref="GitScheduler"/> with no
/// change of its order or places. An ask gets an id, its repository, its read kind, its asker, its class and its deadline; it is answered
/// by the run's answer or failed as <see cref="GitUnplacedException"/> naming why (a place not given in time is <c>deadline</c>), and counted
/// by caller and kind with the wait for its place (<see cref="GitAskTally"/>). A census (<c>GitKeepersCensusTests</c>) holds that nothing
/// reaches the scheduler or the git library's reads but through it, and that only the library's own files name its package.
/// </summary>
/// <remarks>
/// <para><b>The deadline</b> is the ask's bound (what its run may take, as before) and an allowance for waiting for a place, by class: a
/// person's is shortest. It is each asker's own and runs only while that asker's entry is queued (decided against placement under the
/// scheduler's lock), because a git placed runs on to its end or its bound. A write or a git that waits outside Daoris has none.</para>
/// <para><b>In memory</b>: <see cref="Open"/> is the asks not yet answered; an answer is the run's own and is kept by nobody here.</para>
/// </remarks>
internal sealed class GitService
{
    /// <summary>The wait for a place a person's ask is allowed beyond its bound.</summary>
    internal static readonly TimeSpan PersonAllowance = TimeSpan.FromMinutes(5);

    /// <summary>The same for a step's ask: the run-step consumer, the opinion gate, a check.</summary>
    internal static readonly TimeSpan StepAllowance = TimeSpan.FromMinutes(15);

    /// <summary>The same for the background's ask: a look's sweeps run for minutes at a start, so this is the longest of the reads'.</summary>
    internal static readonly TimeSpan BackgroundAllowance = TimeSpan.FromMinutes(30);

    /// <summary>The one service of the process, over the one scheduler.</summary>
    internal static GitService Shared { get; } = new(GitScheduler.Shared);

    private readonly GitScheduler _scheduler;
    private readonly ConcurrentDictionary<long, GitServiceAsk> _open = new();
    private long _ids;

    internal GitService(GitScheduler scheduler) => _scheduler = scheduler;

    /// <summary>How long a read waits for a place by its class; the figures above unless a test hands its own.</summary>
    internal Func<GitAskClass, TimeSpan> Allowance { get; init; } = ClassAllowance;

    /// <summary>The asks not yet answered, oldest first.</summary>
    internal IReadOnlyList<GitServiceAsk> Open => [.. _open.Values.OrderBy(ask => ask.Id)];

    /// <summary>The class of an ask: a person's where its flow is a person's, else a step's by its asker, else the background's.</summary>
    internal static GitAskClass ClassOf(string asker, bool person) =>
        person ? GitAskClass.Person : asker is GitAskers.Steps or GitAskers.Gate or GitAskers.Check ? GitAskClass.Step : GitAskClass.Background;

    internal static TimeSpan ClassAllowance(GitAskClass askClass) => askClass switch
    {
        GitAskClass.Person => PersonAllowance,
        GitAskClass.Step => StepAllowance,
        _ => BackgroundAllowance,
    };

    /// <summary>
    /// Run <paramref name="run"/> for <paramref name="ask"/> in the scheduler's place, <paramref name="bound"/> being what the run itself may
    /// take. Failed as <see cref="GitUnplacedException"/> where the scheduler refuses it or its place does not come within its allowance.
    /// </summary>
    internal async Task<T> PlaceAsync<T>(GitRunAsk ask, TimeSpan bound, Func<CancellationToken, Task<T>> run, CancellationToken ct)
    {
        var asker = GitAskers.Current;
        var askClass = ClassOf(asker, GitScheduler.Whose == StartClass.Person);
        // An act has no queue deadline: a step dropped mid-act would leave it half done.
        TimeSpan? allowance = ask.Writes || ask.Wait == GitWait.Outside ? null : Allowance(askClass);
        var open = new GitServiceAsk(Interlocked.Increment(ref _ids), ask.Repository, ask.Kind, asker, askClass, allowance is { } left ? bound + left : Timeout.InfiniteTimeSpan);
        _open[open.Id] = open;
        if (asker == GitAskers.Other) _scheduler.Tally.RecordSite(GitAskers.TakeSite() ??GitSites.Here(skip: 1));
        try
        {
            return await _scheduler.RunAsync(ask, run, ct, allowance).ConfigureAwait(false);
        }
        catch (GitQueueExpiredException)
        {
            throw GitUnplacedException.Deadline(open, allowance!.Value);
        }
        finally
        {
            _open.TryRemove(open.Id, out _);
        }
    }
}
