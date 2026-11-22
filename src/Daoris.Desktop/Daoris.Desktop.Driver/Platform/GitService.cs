using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
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

/// <summary>One part of a long answer (<see cref="GitService.PartsAsync"/>), told by its ask's id and its number from 1; the last says git's exit.</summary>
/// <param name="Exit">The last part's: git's code, 0 where its asker stopped the read, -1 where git did not start or could not be read.</param>
internal sealed record GitPart(long Ask, int Number, string Text, bool Last, int? Exit);

/// <summary>
/// The one door every git of the desktop is asked of, wrapping <see cref="GitScheduler"/>, which places it by its class (a person's, then a
/// step's, then the background's) with no change of its places. An ask gets an id, its repository, its read kind, its asker, its class and its deadline; it is answered
/// by the run's answer or failed as <see cref="GitUnplacedException"/> naming why (a place not given in time is <c>deadline</c>), and counted
/// by caller and kind with the wait for its place (<see cref="GitAskTally"/>). A census (<c>GitKeepersCensusTests</c>) holds that nothing
/// reaches the scheduler or the git library's reads but through it, and that only the library's own files name its package.
/// </summary>
/// <remarks>
/// <para><b>The deadline</b> is the ask's bound (what its run may take, as before) and an allowance for waiting for a place, by class: a
/// person's is shortest. It is each asker's own and runs only while that asker's entry is queued (decided against placement under the
/// scheduler's lock), because a git placed runs on to its end or its bound. A write or a git that waits outside Daoris has none.</para>
/// <para><b>In memory</b>: <see cref="Open"/> is the asks not yet answered; an answer is the run's own and is kept by nobody here.</para>
/// <para><b>Long answers in parts</b> (<see cref="PartsAsync"/>): a log walk or a tree's listing comes as numbered parts on its ask's id, cut at
/// a line's end once a part's size is held, on the service's own lane (a bounded channel, never the bus's frames): the read waits while
/// <see cref="PartsAhead"/> parts are unread. The machine's run still drains git's pipe into its own unbounded buffer, so git itself is not
/// held back yet. The asker stops the read by cancelling or by no longer reading, and the parts end only once the read has ended,
/// a git that outlived its kill thrown as <see cref="ProcessUnendedException"/>.</para>
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

    /// <summary>How many characters a part holds before it is cut at a line's end: past one message of the bus's.</summary>
    internal const int DefaultPartChars = 64 * 1024;

    // The ask a parted read's own git is placed under, so its id is the parts'.
    private static readonly AsyncLocal<GitServiceAsk?> s_parted = new();

    private readonly GitScheduler _scheduler;
    private readonly ConcurrentDictionary<long, GitServiceAsk> _open = new();
    private long _ids;

    internal GitService(GitScheduler scheduler) => _scheduler = scheduler;

    /// <summary>How long a read waits for a place by its class; the figures above unless a test hands its own.</summary>
    internal Func<GitAskClass, TimeSpan> Allowance { get; init; } = ClassAllowance;

    /// <summary>How many characters a part holds before it is cut; <see cref="DefaultPartChars"/> unless a test hands its own.</summary>
    internal int PartChars { get; init; } = DefaultPartChars;

    /// <summary>How many parts may wait unread before the read waits for its asker.</summary>
    internal int PartsAhead { get; init; } = 4;

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
        var deadline = allowance is { } left ? bound + left : Timeout.InfiniteTimeSpan;
        // A parted read's git is told by the parts' id, which stays open while they are read, and says where it is placed and by when.
        var parted = s_parted.Value;
        var open = parted is not null
            ? parted with { Repository = ask.Repository, Kind = ask.Kind, Deadline = deadline }
            : new GitServiceAsk(Interlocked.Increment(ref _ids), ask.Repository, ask.Kind, asker, askClass, deadline);
        _open[open.Id] = open;
        if (asker == GitAskers.Other) _scheduler.Tally.RecordSite(GitAskers.TakeSite() ??GitSites.Here(skip: 1));
        try
        {
            return await _scheduler.RunAsync(ask, run, ct, allowance, askClass).ConfigureAwait(false);
        }
        catch (GitQueueExpiredException)
        {
            throw GitUnplacedException.Deadline(open, allowance!.Value);
        }
        finally
        {
            if (parted is null) _open.TryRemove(open.Id, out _);
        }
    }

    /// <summary>
    /// The answer of <paramref name="read"/> (a streamed read handed a take, answering git's code) as numbered parts on one ask's id, each
    /// yielded as it is cut, the last marked with the code. Cancelling <paramref name="ct"/> or leaving the parts unread stops the read, and
    /// they end only once it has ended.
    /// </summary>
    internal async IAsyncEnumerable<GitPart> PartsAsync(
        string repository, string kind, Func<Func<ReadOnlyMemory<char>, bool>, CancellationToken, Task<int>> read,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var asker = GitAskers.Current;
        var open = new GitServiceAsk(
            Interlocked.Increment(ref _ids), repository, kind, asker, ClassOf(asker, GitScheduler.Whose == StartClass.Person), Timeout.InfiniteTimeSpan);
        _open[open.Id] = open;
        var lane = Channel.CreateBounded<GitPart>(new BoundedChannelOptions(Math.Max(1, PartsAhead)) { SingleReader = true, SingleWriter = true });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var cutter = new PartCutter(open.Id, Math.Max(1, PartChars));

        // Called on the read's own flow as git writes: it waits there while the lane is full, so the read goes at its asker's pace.
        bool Write(GitPart part)
        {
            try
            {
                while (!lane.Writer.TryWrite(part))
                {
                    if (!lane.Writer.WaitToWriteAsync(stop.Token).AsTask().GetAwaiter().GetResult()) return false;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        bool Take(ReadOnlyMemory<char> piece)
        {
            foreach (var part in cutter.Add(piece.Span))
            {
                if (!Write(part)) return false;
            }

            return !stop.IsCancellationRequested;
        }

        // Its own flow, never the asker's: a take that waits for room must not wait on the thread that would make it.
        ProcessUnendedException? unended = null;
        var reading = Task.Run(async () =>
        {
            try
            {
                s_parted.Value = open;
                var exit = await read(Take, stop.Token).ConfigureAwait(false);
                if (!stop.IsCancellationRequested) Write(cutter.Last(exit));
                lane.Writer.TryComplete();
            }
            catch (Exception error)
            {
                // A stop is no failure, but a git that outlived its kill is said however the read stopped.
                unended = error as ProcessUnendedException;
                lane.Writer.TryComplete(stop.IsCancellationRequested ? null : error);
            }
        }, CancellationToken.None);

        try
        {
            // A part already on the lane is not handed out once the ask is cancelled: the channel's own drain would.
            while (await lane.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (lane.Reader.TryRead(out var part))
                {
                    ct.ThrowIfCancellationRequested();
                    yield return part;
                }
            }
        }
        finally
        {
            stop.Cancel();
            await reading.ConfigureAwait(false);
            _open.TryRemove(open.Id, out _);
            if (unended is not null) throw unended;
        }
    }

    /// <summary>A read's text cut into parts at a line's end once a part's size is held; a line longer than a part is cut where it stands.</summary>
    private sealed class PartCutter(long ask, int size)
    {
        private readonly StringBuilder _held = new();
        private int _number;

        internal List<GitPart> Add(ReadOnlySpan<char> piece)
        {
            _held.Append(piece);
            List<GitPart> cut = [];
            while (_held.Length >= size)
            {
                var text = _held.ToString();
                var end = text.LastIndexOf('\n', size - 1);
                if (end < 0) end = text.IndexOf('\n', size);
                var length = end < 0 ? text.Length : end + 1;
                cut.Add(new GitPart(ask, ++_number, text[..length], Last: false, Exit: null));
                _held.Remove(0, length);
            }

            return cut;
        }

        internal GitPart Last(int exit)
        {
            var text = _held.ToString();
            _held.Clear();
            return new GitPart(ask, ++_number, text, Last: true, Exit: exit);
        }
    }
}
