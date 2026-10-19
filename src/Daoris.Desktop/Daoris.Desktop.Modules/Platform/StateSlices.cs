using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>One partition as a judgement made it (PUSH1a, D182 point 1): its name, and its projection, an allow-listed DTO.</summary>
/// <param name="Partition">The partition's name within its slice, in the page's words.</param>
/// <param name="Data">What the window holds of it: never a credential, an arbitrary payload, or a machine path outside a desktop-only slice.</param>
public sealed record SliceJudged(string Partition, object Data);

/// <summary>What wakes a slice's judge on the kit's bus (D182 point 2): a Daoris event's type, and which of its payloads do.</summary>
/// <param name="Type">The event's type under <see cref="StateSlice.Module"/>.</param>
/// <param name="When">Which payloads wake it; null is every one.</param>
public sealed record SliceWake(string Type, Func<object?, bool>? When = null);

/// <summary>
/// <c>STATE_PUSH</c> on the wire (PUSH1a, D182 point 3): one partition of a slice as it now stands, numbered within the slice's
/// epoch. A whole replacement carries no <see cref="BaseSeq"/> and may skip numbers; a delta names the number it applies to and
/// the keys it takes away, and a window holding another number reads the partition again.
/// </summary>
/// <remarks>A wire shape: a field is added, never changed in place (D169 point 3).</remarks>
/// <param name="Slice">The slice, as its owner names it.</param>
/// <param name="Epoch">The owner's life: a number from another epoch is no number of this one.</param>
/// <param name="Partition">The partition, as the owner names it.</param>
/// <param name="Seq">This partition's number: higher is newer within the epoch.</param>
/// <param name="Status"><see cref="StateSlice.Ready"/>, or <see cref="StateSlice.Gone"/> where the partition is no longer judged.</param>
/// <param name="Data">The partition's projection; none where it is gone.</param>
/// <param name="BaseSeq">The number a delta applies to; absent on a whole replacement.</param>
/// <param name="Removed">The keys a delta takes away; absent on a whole replacement.</param>
public sealed record SlicePush(
    string Slice, string Epoch, string Partition, long Seq, string Status, object? Data,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? BaseSeq = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Removed = null);

/// <summary>One partition in a window's first snapshot: as <see cref="SlicePush"/> carries it, whole.</summary>
public sealed record SlicePartition(string Partition, long Seq, string Status, object? Data);

/// <summary>
/// A slice's snapshot (PUSH1a, D182 point 3), read from memory by the route a window asks after it subscribes: every partition as
/// last judged, or <see cref="StateSlice.NotJudged"/> with none before the first judgement, never a judgement made for the asking.
/// </summary>
public sealed record SliceSnapshot(string Slice, string Epoch, string Status, IReadOnlyList<SlicePartition> Partitions);

/// <summary>A partition's number in a manifest.</summary>
public sealed record SliceVersion(string Partition, long Seq);

/// <summary>
/// <c>STATE_MANIFEST</c> on the wire (PUSH1a, D182 point 3): each partition's number, told at the slice's period through the same
/// bus and after the pushes it covers, so a window that missed a push (a batch dropped, its final update among them) reads that
/// partition again once.
/// </summary>
public sealed record SliceManifest(string Slice, string Epoch, IReadOnlyList<SliceVersion> Partitions);

/// <summary>
/// One slice of the machine's state, judged once and pushed with its data to every window (PUSH1a, D182 points 1 to 4).
/// </summary>
/// <remarks>
/// <para><b>One owner, one judgement at a time.</b> A wake only asks (<see cref="Wake"/>): the bus's subscribers and the loop's look
/// write one waiting wake and return, and the slice's worker judges. Judgements never overlap, so a slow one's completion can never
/// land over a newer one's, and each partition is compared with what was last pushed: a changed one is numbered and pushed whole, an
/// unchanged one, a reconciliation's included, is pushed nothing.</para>
///
/// <para><b>One ordered outbox.</b> Every push and manifest of a slice is told by the judgement that numbered it, one at a time, so
/// the pump receives them in the order they were numbered; the kit's pump may coalesce a whole replacement by its partition and a
/// manifest by its slice, and never a delta (<see cref="EventMessage.CoalesceKey"/>). The snapshot is swapped under a short lock
/// that no judgement or bus call is made under.</para>
///
/// <para><b>Shell-only</b> (D47, D169 point 4): the bus is this machine's, forwarded to its windows and to nothing else, so a
/// desktop slice may carry what only this machine's windows may read.</para>
/// </remarks>
public sealed class StateSlice
{
    /// <summary>The module every push and manifest is raised under, as every Daoris event is.</summary>
    public const string Module = "DAORIS";

    /// <summary>A partition pushed.</summary>
    public const string PushType = "STATE_PUSH";

    /// <summary>The slice's numbers, told at its period.</summary>
    public const string ManifestType = "STATE_MANIFEST";

    /// <summary>A partition as judged; a slice that has judged.</summary>
    public const string Ready = "ready";

    /// <summary>A slice not judged yet: its snapshot holds no partition, and the window keeps its pull until a push.</summary>
    public const string NotJudged = "notJudged";

    /// <summary>A partition no longer judged.</summary>
    public const string Gone = "gone";

    private readonly IEventBus _bus;
    private readonly Func<CancellationToken, Task<IReadOnlyList<SliceJudged>?>> _judge;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Held> _held = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _telling = new(1, 1);
    private readonly Channel<bool> _wakes = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private bool _judged;
    private long _judgements;

    /// <summary>Whether the last judgement failed; read and written only within a judgement's turn.</summary>
    private bool _failing;

    private sealed record Held(long Seq, string Status, object? Data, string Json);

    /// <param name="name">The slice's name, in the page's words.</param>
    /// <param name="bus">Where its pushes and manifests are raised, the kit's bus each window's pump forwards.</param>
    /// <param name="judge">
    /// One judgement of the slice's partitions, from the truth (files, git, the database, the machine's facts); null where what it is
    /// judged from has not landed yet (PUSH1b1), which judges nothing: a slice never judged stays <see cref="NotJudged"/>, so a window
    /// never installs an empty slice as the truth, and one judged keeps what it holds.
    /// </param>
    /// <param name="epoch">This owner's life; a fresh one where none is named.</param>
    public StateSlice(string name, IEventBus bus, Func<CancellationToken, Task<IReadOnlyList<SliceJudged>?>> judge, string? epoch = null)
    {
        Name = name;
        _bus = bus;
        _judge = judge;
        Epoch = epoch ?? Guid.NewGuid().ToString("N")[..12];
    }

    /// <summary>The slice's name, in the page's words.</summary>
    public string Name { get; }

    /// <summary>This owner's life: every number it gives is within it.</summary>
    public string Epoch { get; }

    /// <summary>How often the worker reconciles and tells the manifest: a dropped push is repaired within it.</summary>
    public TimeSpan ManifestEvery { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>What on the kit's bus wakes the judge while the worker runs.</summary>
    public IReadOnlyList<SliceWake> WakesOn { get; init; } = [];

    /// <summary>
    /// Where the slice hears of a move no bus event tells (PUSH1b1: the machine's facts landing), while the worker runs: each is
    /// handed the slice's <see cref="Wake"/> and answers what stops it hearing, disposed when the worker stops.
    /// </summary>
    public IReadOnlyList<Func<Action, IDisposable>> Hears { get; init; } = [];

    /// <summary>
    /// Where a judgement's failure is said, once until one succeeds again; the last judged state stands, and the next wake judges
    /// again.
    /// </summary>
    public Action<Exception>? Failed { get; init; }

    /// <summary>How many judgements were made: a window's snapshot makes none.</summary>
    public long Judgements => Interlocked.Read(ref _judgements);

    /// <summary>Ask for a judgement: one waits at most, and the worker makes it. Never judges here, so a bus subscriber returns at once.</summary>
    public void Wake() => _wakes.Writer.TryWrite(true);

    /// <summary>The slice as last judged, from memory: what a window installs as its baseline.</summary>
    public SliceSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new SliceSnapshot(Name, Epoch, _judged ? Ready : NotJudged, [
                .. _held.Where(each => each.Value.Status != Gone)
                    .OrderBy(each => each.Key, StringComparer.Ordinal)
                    .Select(each => new SlicePartition(each.Key, each.Value.Seq, each.Value.Status, each.Value.Data)),
            ]);
        }
    }

    /// <summary>Each partition's number, a gone one's among them, as the manifest tells it.</summary>
    public SliceManifest Manifest()
    {
        lock (_gate)
        {
            return new SliceManifest(Name, Epoch, [
                .. _held.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => new SliceVersion(each.Key, each.Value.Seq)),
            ]);
        }
    }

    /// <summary>
    /// One judgement now: each partition compared with what was last pushed, a changed one numbered and pushed whole, one no longer
    /// judged pushed gone. A failure keeps what was judged and pushes nothing.
    /// </summary>
    public async Task JudgeAsync(CancellationToken ct = default)
    {
        await _telling.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            IReadOnlyList<SliceJudged>? judged;
            try
            {
                judged = await _judge(ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // Said once until a judgement succeeds again: a file that stays unreadable is not a line at every look.
                if (!_failing) Failed?.Invoke(error);
                _failing = true;
                return;
            }
            finally
            {
                Interlocked.Increment(ref _judgements);
            }

            _failing = false;
            // Nothing to judge from yet: what is held stands, and a slice never judged stays not judged.
            if (judged is null) return;
            var pushes = new List<SlicePush>();
            lock (_gate)
            {
                _judged = true;
                var named = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (partition, data) in judged)
                {
                    named.Add(partition);
                    var json = JsonSerializer.Serialize(data, IpcJson.Options);
                    var held = _held.GetValueOrDefault(partition);
                    if (held is { Status: Ready } && held.Json == json) continue;
                    var seq = (held?.Seq ?? 0) + 1;
                    _held[partition] = new Held(seq, Ready, data, json);
                    pushes.Add(new SlicePush(Name, Epoch, partition, seq, Ready, data));
                }

                foreach (var (partition, held) in _held.Where(each => each.Value.Status != Gone && !named.Contains(each.Key)).ToList())
                {
                    _held[partition] = new Held(held.Seq + 1, Gone, null, "");
                    pushes.Add(new SlicePush(Name, Epoch, partition, held.Seq + 1, Gone, null));
                }
            }

            // Told outside the lock and inside the judgement's turn: no other judgement's pushes come between.
            foreach (var push in pushes) Tell(PushType, push, $"{Name}/{push.Partition}");
        }
        finally
        {
            _telling.Release();
        }
    }

    /// <summary>Tell the manifest now, after every push already numbered.</summary>
    public async Task TellManifestAsync(CancellationToken ct = default)
    {
        await _telling.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Tell(ManifestType, Manifest(), Name);
        }
        finally
        {
            _telling.Release();
        }
    }

    /// <summary>
    /// The slice's worker, until <paramref name="ct"/>: a judgement at once, one for every wake, and at each period a reconciling
    /// judgement and the manifest. Subscribes what wakes it for as long as it runs.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var heard = WakesOn.Select(wake => _bus.Subscribe(Module, wake.Type, message =>
        {
            if (wake.When is null || wake.When(message.Payload)) Wake();
            return Task.CompletedTask;
        })).ToList();
        heard.AddRange(Hears.Select(hear => hear(Wake)));
        try
        {
            var clock = Stopwatch.StartNew();
            var due = ManifestEvery;
            await JudgeAsync(ct).ConfigureAwait(false);
            while (!ct.IsCancellationRequested)
            {
                var wait = due - clock.Elapsed;
                if (wait > TimeSpan.Zero)
                {
                    using var period = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    period.CancelAfter(wait);
                    try
                    {
                        if (await _wakes.Reader.WaitToReadAsync(period.Token).ConfigureAwait(false)) _wakes.Reader.TryRead(out _);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // The period, not a wake: judged again below as a reconciliation, which pushes nothing unchanged.
                    }
                }

                await JudgeAsync(ct).ConfigureAwait(false);
                if (clock.Elapsed >= due)
                {
                    await TellManifestAsync(ct).ConfigureAwait(false);
                    due = clock.Elapsed + ManifestEvery;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Stopped with the loop.
        }
        finally
        {
            foreach (var subscription in heard) subscription.Dispose();
        }
    }

    private void Tell(string type, object payload, string coalesce) =>
        _bus.Emit(new EventMessage { Module = Module, Type = type, Payload = payload, CoalesceKey = coalesce });
}

/// <summary>
/// The slices this Daoris pushes (PUSH1a, D182), by name: what the snapshot route finds, and what the shell's loop runs. A slice not
/// held here is one the page keeps pulling (capability per slice, D182 point 7).
/// </summary>
public sealed class StateSlices(params StateSlice[] slices)
{
    private readonly IReadOnlyList<StateSlice> _slices = slices;

    /// <summary>The slice named <paramref name="name"/>, or null where this Daoris pushes none by that name.</summary>
    public StateSlice? Find(string name) => _slices.FirstOrDefault(slice => string.Equals(slice.Name, name, StringComparison.Ordinal));

    /// <summary>Every slice's worker, until <paramref name="ct"/>.</summary>
    public Task RunAsync(CancellationToken ct) => Task.WhenAll(_slices.Select(slice => slice.RunAsync(ct)));
}
