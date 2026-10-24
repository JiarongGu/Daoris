using System.Text.Json;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The answers of the reads marked <see cref="ByEventAttribute"/> (NOBLOCK1, D56's NOBLOCK1 note): an ask is answered at once from
/// what is held, its work runs on a .NET task of its own, and what the work answers reaches the page as <c>DAORIS.ANSWERED</c>.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> The install's machine log ranked the routes that held the window (<c>ui.stalled</c>): <c>SESSION_WHERE</c> 1,439
/// times, up to 65.9 s, each a read of the landings' record and a folder per session on the window's thread before its first wait.
/// The owner could not type meanwhile. A route answered from memory holds the thread for a dictionary's look.</para>
///
/// <para><b>What an ask is answered.</b> The answer held for its route and payload, at once; with none held yet (the first ask
/// since the start, or one the bound let go), what the work answers within <see cref="Patience"/>, waited for off the window's
/// thread, else <see cref="Pending"/>, which the page reads as "keep what you hold". A work that failed with nothing held is
/// answered as the refusal it was, once, so a page asking again reads the failure rather than waiting for ever.</para>
///
/// <para><b>Every ask starts the work again</b>, or joins the one under way for its key, so a held answer is never more than one
/// ask behind; a press forgets the works under way (<see cref="Forget"/>), as it does a shared read's, so the ask after a change
/// never joins a work begun before it, and an older work landing after a newer one never overwrites it.</para>
///
/// <para><b>Told only when it moved.</b> A work answering what was held tells nothing, so the page's ask, the work and the event
/// never chase each other. The event names the page's own query (the payload's <c>held</c>, its key's hash), never the payload,
/// whose trees are machine paths: the page sends <c>held</c>, and an ask without one is answered and told nothing.</para>
/// </remarks>
public sealed class HeldAnswers
{
    /// <summary>The module every Daoris event is raised under.</summary>
    public const string Module = "DAORIS";

    /// <summary>The event's type: a held read's work answered.</summary>
    public const string Event = "ANSWERED";

    /// <summary>The payload's field naming the page's query, echoed by the event.</summary>
    public const string HeldField = "held";

    /// <summary>How many answers are held, each by route and payload; the least recently asked goes first.</summary>
    public const int Kept = 128;

    /// <summary>The answer to an ask whose work has not answered yet and of which nothing is held: the page keeps what it holds.</summary>
    public static readonly object Pending = new { Pending = true };

    private static readonly JsonSerializerOptions s_wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IEventBus _bus;
    private readonly object _gate = new();
    private readonly Dictionary<string, Held> _held = new(StringComparer.Ordinal);
    private long _seq;
    private long _forgotten;

    public HeldAnswers(IEventBus bus) => _bus = bus;

    /// <summary>
    /// How long a first ask waits for its work, off the window's thread, before it is answered <see cref="Pending"/>: well inside
    /// the window's bound (<see cref="UiStallWatch.Bound"/>), since a page told nothing draws nothing.
    /// </summary>
    public TimeSpan Patience { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The test seam: awaited on the work's task before the work, with its key, so a test makes a work as slow as it likes and
    /// times the ask. Production sets none.
    /// </summary>
    public Func<string, Task>? BeforeWork { get; set; }

    /// <summary>Whether an answer is <see cref="Pending"/>.</summary>
    public static bool IsPending(object? answer) => ReferenceEquals(answer, Pending);

    /// <summary>
    /// Answer <paramref name="key"/> from what is held, and start its <paramref name="work"/> on a task of its own, or join the
    /// one under way. <paramref name="held"/> is the page's query the event names; null tells nothing.
    /// </summary>
    public async Task<object?> AskAsync(string key, string? held, Func<CancellationToken, Task<object?>> work)
    {
        Held entry;
        Task<object?> flight;
        lock (_gate)
        {
            if (!_held.TryGetValue(key, out entry!))
            {
                Trim();
                _held[key] = entry = new Held();
            }

            entry.Asked = ++_seq;
            if (held is not null) entry.Pages.Add(held);
            if (entry.Flight is not { } under || entry.Begun <= _forgotten)
            {
                entry.Begun = ++_seq;
                under = entry.Flight = Begin(key, entry, entry.Begun, work);
            }

            flight = under;
            if (entry.Answered) return entry.Answer;
            if (entry.Fault is { } fault)
            {
                entry.Fault = null;
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(fault);
            }
        }

        // Nothing held: the work's answer, if it comes within the patience, waited for off the window's thread.
        var waited = await Task.WhenAny(flight, Task.Delay(Patience)).ConfigureAwait(false);
        return waited == flight ? await flight.ConfigureAwait(false) : Pending;
    }

    /// <summary>
    /// The works under way are joined no more: the next ask of each starts afresh (a press answered, as the shared reads'
    /// <see cref="RouteFlights.Forget"/>). They still land, where nothing newer did.
    /// </summary>
    public void Forget()
    {
        lock (_gate) _forgotten = _seq;
    }

    /// <summary>
    /// The work under way for <paramref name="key"/>, or none: what a test waits for when an ask was answered <see cref="Pending"/>.
    /// </summary>
    public Task LandedAsync(string key)
    {
        lock (_gate) return _held.TryGetValue(key, out var entry) && entry.Flight is { } flight ? flight : Task.CompletedTask;
    }

    // The work on a task of its own: Task.Run, so not a line of it runs on the asker's thread, whatever the work does before its
    // first wait. Its token is none: the page that asked has been answered, and the next asker reads what it lands.
    private Task<object?> Begin(string key, Held entry, long begun, Func<CancellationToken, Task<object?>> work)
    {
        var slowed = BeforeWork;
        var flight = Task.Run(async () =>
        {
            if (slowed is not null) await slowed(key).ConfigureAwait(false);
            return await work(CancellationToken.None).ConfigureAwait(false);
        });
        _ = flight.ContinueWith(done => Land(key, entry, begun, done), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return flight;
    }

    private void Land(string key, Held entry, long begun, Task<object?> done)
    {
        string[] pages;
        object? answer = null;
        var failed = false;
        lock (_gate)
        {
            if (ReferenceEquals(entry.Flight, done)) entry.Flight = null;
            // An older work landing after a newer one moves nothing.
            if (begun < entry.Landed) return;
            entry.Landed = begun;
            if (done.IsCompletedSuccessfully)
            {
                answer = done.Result;
                var wire = JsonSerializer.Serialize(answer, s_wire);
                var moved = !entry.Answered || !string.Equals(wire, entry.Wire, StringComparison.Ordinal);
                entry.Answer = answer;
                entry.Wire = wire;
                entry.Answered = true;
                entry.Fault = null;
                if (!moved) return;
            }
            else
            {
                // A failure keeps what was held: the page goes on reading it. With nothing held it is the next ask's refusal.
                if (entry.Answered) return;
                entry.Fault = done.Exception?.InnerException ?? done.Exception;
                failed = true;
            }

            if (!_held.ContainsKey(key)) return;
            pages = [.. entry.Pages];
        }

        foreach (var page in pages)
        {
            _bus.Emit(Module, Event, failed ? new { Held = page, Failed = true } : new { Held = page, Answer = answer });
        }
    }

    // Under the gate: room for one more, the least recently asked let go.
    private void Trim()
    {
        while (_held.Count >= Kept)
        {
            var oldest = _held.MinBy(each => each.Value.Asked).Key;
            _held.Remove(oldest);
        }
    }

    /// <summary>What is held of one route and payload, and its work under way. Read and written under the gate.</summary>
    private sealed class Held
    {
        public object? Answer;
        public string? Wire;
        public bool Answered;
        public Exception? Fault;
        public Task<object?>? Flight;
        public long Begun;
        public long Landed;
        public long Asked;
        public readonly HashSet<string> Pages = new(StringComparer.Ordinal);
    }
}
