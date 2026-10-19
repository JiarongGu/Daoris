using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What each part of the loop's looks took (LOOKWAIT1, D181 point 6): every stage of a look, and every piece of the work that runs
/// beside it (the remote sync, a start's preparation that outlived its look, a plugin's word on an ending, a packet frozen), timed
/// as a span with its subject, so a slow look names its slow part in the machine log rather than only its length.
/// </summary>
/// <remarks>
/// <para><b>Two lines.</b> A span still running past <see cref="SlowAfter"/> is written at once as <c>look.slow</c>, its stage, its
/// subject and whether it runs beside the look, so a hung one is named while it hangs, once. Every span that has ended is folded
/// into the next <c>watch.looked</c>'s <c>stages</c> (<see cref="Take"/>), one line per look however much ran.</para>
///
/// <para><b>Compact.</b> One entry per stage in the order it first began, its milliseconds summed, <c>xN</c> where it ran for
/// several subjects and <c>@subject</c> for the slowest; a gate's queue apart from its hold, as <c>stage.queue</c>; a span beside the
/// look marked <c>~</c>; a stage under <see cref="Floor"/> left out. A subject is a name, a quest's <c>#id</c>, a session or a
/// repository, never a path.</para>
///
/// <para>Shared across the looks of one watch, as its running sessions are, since the work beside a look ends during a later one.</para>
/// </remarks>
public sealed class LookStages
{
    /// <summary>How long a stage runs before it is named as slow while it runs.</summary>
    public static readonly TimeSpan SlowAfter = TimeSpan.FromSeconds(5);

    /// <summary>The milliseconds under which a stage is left out of the compact line: what a look costs is in its larger parts.</summary>
    public const long Floor = 10;

    private readonly MachineLog? _log;
    private readonly TimeSpan _slowAfter;
    private readonly Func<long> _clock;
    private readonly object _gate = new();
    private readonly List<Ended> _ended = [];

    /// <param name="log">Where a slow stage is named; null names none, and the spans are still kept for the line.</param>
    /// <param name="slowAfter">How long before a running stage is named as slow; <see cref="SlowAfter"/> unless a test shortens it.</param>
    /// <param name="clock">The milliseconds a span reads, the machine's monotonic clock unless a test moves it by hand.</param>
    public LookStages(MachineLog? log = null, TimeSpan? slowAfter = null, Func<long>? clock = null)
    {
        _log = log;
        _slowAfter = slowAfter ?? SlowAfter;
        _clock = clock ?? (() => (long)Stopwatch.GetElapsedTime(Origin).TotalMilliseconds);
    }

    private static readonly long Origin = Stopwatch.GetTimestamp();

    /// <summary>Begin a span of <paramref name="stage"/>, for <paramref name="subject"/> where it has one; it ends when disposed.</summary>
    /// <param name="beside">Whether it runs beside the look rather than in it: marked <c>~</c> on the line.</param>
    public StageSpan Begin(string stage, string? subject = null, bool beside = false) => new(this, stage, subject, beside, _clock());

    /// <summary>Run <paramref name="work"/> as a span of <paramref name="stage"/>, ended however the work ends.</summary>
    public async Task<T> TimeAsync<T>(string stage, Func<Task<T>> work, string? subject = null, bool beside = false)
    {
        using var span = Begin(stage, subject, beside);
        return await work().ConfigureAwait(false);
    }

    /// <summary>Run <paramref name="work"/> as a span of <paramref name="stage"/>, ended however the work ends.</summary>
    public async Task TimeAsync(string stage, Func<Task> work, string? subject = null, bool beside = false)
    {
        using var span = Begin(stage, subject, beside);
        await work().ConfigureAwait(false);
    }

    /// <summary>
    /// Every span that ended since the last take, as the compact line <c>watch.looked</c> carries, and let go; null where none
    /// reached <see cref="Floor"/>. A span still running is in a later line, once it ends.
    /// </summary>
    public string? Take()
    {
        List<Ended> ended;
        lock (_gate)
        {
            ended = [.. _ended];
            _ended.Clear();
        }

        var line = new StringBuilder();
        foreach (var stage in ended.OrderBy(span => span.Began).GroupBy(span => (span.Stage, span.Beside)))
        {
            var spans = stage.ToList();
            var held = spans.Sum(span => span.Held);
            var queued = spans.Sum(span => span.Queued);
            var name = (stage.Key.Beside ? "~" : "") + stage.Key.Stage;
            if (held >= Floor)
            {
                var slowest = spans.Where(span => span.Subject is not null).MaxBy(span => span.Held);
                Append(line, $"{name}={held}{(spans.Count > 1 ? $"x{spans.Count}" : "")}{(slowest is null ? "" : $"@{slowest.Subject}")}");
            }

            if (queued >= Floor) Append(line, $"{name}.queue={queued}");
        }

        return line.Length == 0 ? null : line.ToString();

        static void Append(StringBuilder line, string entry)
        {
            if (line.Length > 0) line.Append(' ');
            line.Append(entry);
        }
    }

    private void Keep(Ended span)
    {
        lock (_gate) _ended.Add(span);
    }

    private void Slow(StageSpan span) =>
        _log?.Warn("look.slow",
            ("stage", span.Stage),
            ("subject", span.Subject),
            ("beside", span.Beside),
            ("ms", _clock() - span.Began));

    private sealed record Ended(string Stage, string? Subject, bool Beside, long Began, long Held, long Queued);

    /// <summary>One stage's span (<see cref="Begin"/>): ended once, when disposed.</summary>
    public sealed class StageSpan : IDisposable
    {
        private readonly LookStages _stages;
        private readonly Timer? _slow;
        private long _queued;
        private int _ended;
        private volatile bool _beside;

        internal StageSpan(LookStages stages, string stage, string? subject, bool beside, long began)
        {
            _stages = stages;
            Stage = stage;
            Subject = subject;
            _beside = beside;
            Began = began;
            // Named once if it outlives the threshold; a log nobody keeps names nothing, so no timer is spent on it.
            if (stages._log is { Writing: true }) _slow = new Timer(_ => Slowed(), null, stages._slowAfter, Timeout.InfiniteTimeSpan);
        }

        internal string Stage { get; }

        internal string? Subject { get; }

        internal bool Beside => _beside;

        internal long Began { get; }

        /// <summary>How long of it was spent waiting for a gate (a queue for a place, a semaphore), said apart from its hold.</summary>
        public void Queued(TimeSpan queued) => Interlocked.Add(ref _queued, (long)queued.TotalMilliseconds);

        /// <summary>
        /// It runs on beside the look that began it, which let go of it (PARALLEL1b: a start not yet open), so it is marked as work
        /// beside the look in the line it ends in, never read as a later look's own.
        /// </summary>
        public void Aside() => _beside = true;

        private void Slowed()
        {
            if (Volatile.Read(ref _ended) == 0) _stages.Slow(this);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _ended, 1) == 1) return;
            _slow?.Dispose();
            var queued = Interlocked.Read(ref _queued);
            var took = _stages._clock() - Began;
            _stages.Keep(new Ended(Stage, Subject, Beside, Began, Math.Max(0, took - queued), queued));
        }
    }
}
