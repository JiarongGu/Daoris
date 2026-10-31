using System.Text.Json;
using System.Text.Json.Nodes;
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
///
/// <para><b>Or by its inputs' versions</b> (FOUNDATION1s, D182's part B note): an ask handed its route's versions (the scopes and slices
/// its answer is made of, <see cref="ByEventAttribute"/>) is answered with no work while they are those of the answer held, and works
/// again once one moved; a work under way at those versions is joined. Null versions work at every ask, as above.</para>
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
    private long _served;
    private long _worked;

    public HeldAnswers(IEventBus bus) => _bus = bus;

    /// <summary>How many asks were answered from what was held with no work begun, its inputs standing (FOUNDATION1s).</summary>
    public long Served => Interlocked.Read(ref _served);

    /// <summary>How many works were begun.</summary>
    public long Worked => Interlocked.Read(ref _worked);

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
    /// one under way. <paramref name="held"/> is the page's query the event names; null tells nothing. <paramref name="versions"/>
    /// reads the route's inputs' versions now: while they are the held answer's, no work begins (FOUNDATION1s); null works.
    /// </summary>
    /// <param name="numbered">
    /// A numbered read (COWORKVIEW1a, the design §4.1): never a refusal, every reply and event carrying <c>at</c>, its
    /// <see cref="Epoch"/> and its landing's number, a failure answered with its words, failed or over the held answer stale.
    /// </param>
    public async Task<object?> AskAsync(
        string key, string? held, Func<CancellationToken, Task<object?>> work, Func<string?>? versions = null, bool numbered = false)
    {
        Held entry;
        Task<object?> flight;
        long begun;
        var at = versions?.Invoke();
        lock (_gate)
        {
            if (!_held.TryGetValue(key, out entry!))
            {
                Trim();
                _held[key] = entry = new Held { Numbered = numbered };
            }

            entry.Asked = ++_seq;
            if (held is not null) entry.Pages.Add(held);
            if (at is not null && entry.Answered && entry.Made == at)
            {
                Interlocked.Increment(ref _served);
                return Reply(entry);
            }

            // Versions read join only a work begun at them; none read join any begun since the last press.
            var stale = at is null ? entry.Begun <= _forgotten : entry.FlightAt != at;
            if (entry.Flight is not { } under || stale)
            {
                entry.Begun = ++_seq;
                entry.FlightAt = at;
                under = entry.Flight = Begin(key, entry, entry.Begun, at, work);
            }

            flight = under;
            begun = entry.Begun;
            if (entry.Answered) return Reply(entry);
            if (entry.Fault is { } fault)
            {
                // A numbered read's fault is its answer at every ask until a landing replaces it.
                if (numbered) return Reply(entry);
                entry.Fault = null;
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(fault);
            }
        }

        // Nothing held: the work's answer, if it comes within the patience, waited for off the window's thread.
        var waited = await Task.WhenAny(flight, Task.Delay(Patience)).ConfigureAwait(false);
        if (waited != flight) return Pending;
        if (!numbered) return await flight.ConfigureAwait(false);
        return flight.IsCompletedSuccessfully ? Numbered(flight.Result, begun) : Failure(WhyOf(flight.Exception), begun);
    }

    /// <summary>This module's life (COWORKVIEW1a): a numbered read's number is within it.</summary>
    public string Epoch { get; } = Guid.NewGuid().ToString("N")[..12];

    // Under the gate: what is held, as an ask is answered it; a numbered read's with its number, stale where a failure is kept.
    private object? Reply(Held entry)
    {
        if (!entry.Numbered) return entry.Answer;
        if (!entry.Answered) return Failure(WhyOf(entry.Fault), entry.Landed);
        var reply = Numbered(entry.Answer, entry.Landed);
        if (entry.Kept is { } why) reply["stale"] = new JsonObject { ["why"] = why };
        return reply;
    }

    private JsonObject Numbered(object? answer, long n)
    {
        var written = JsonSerializer.SerializeToNode(answer, s_wire) as JsonObject ?? new JsonObject { ["value"] = JsonSerializer.SerializeToNode(answer, s_wire) };
        written["at"] = new JsonObject { ["epoch"] = Epoch, ["n"] = n };
        return written;
    }

    private JsonObject Failure(string why, long n) =>
        new() { ["failed"] = new JsonObject { ["why"] = why }, ["at"] = new JsonObject { ["epoch"] = Epoch, ["n"] = n } };

    /// <summary>The refusal's words, the host's own.</summary>
    private static string WhyOf(Exception? fault) =>
        (fault is AggregateException { InnerException: { } inner } ? inner : fault)?.Message ?? "the read failed";

    /// <summary>
    /// The works under way are joined no more: the next ask of each starts afresh (a press answered, as the shared reads'
    /// <see cref="RouteFlights.Forget"/>). They still land, where nothing newer did.
    /// </summary>
    public void Forget()
    {
        lock (_gate) _forgotten = _seq;
    }

    /// <summary>
    /// The last work begun for <paramref name="key"/> landed, its answer held: what a test waits for when an ask was answered
    /// <see cref="Pending"/>, or held.
    /// </summary>
    public Task LandedAsync(string key)
    {
        lock (_gate) return _held.TryGetValue(key, out var entry) ? entry.Landing : Task.CompletedTask;
    }

    // The work on a task of its own: Task.Run, so not a line of it runs on the asker's thread, whatever the work does before its
    // first wait. Its token is none: the page that asked has been answered, and the next asker reads what it lands.
    private Task<object?> Begin(string key, Held entry, long begun, string? at, Func<CancellationToken, Task<object?>> work)
    {
        var slowed = BeforeWork;
        Interlocked.Increment(ref _worked);
        var flight = Task.Run(async () =>
        {
            if (slowed is not null) await slowed(key).ConfigureAwait(false);
            return await work(CancellationToken.None).ConfigureAwait(false);
        });
        entry.Landing = flight.ContinueWith(done => Land(key, entry, begun, at, done), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return flight;
    }

    /// <summary>
    /// Told each answer that lands as the one held, its key beside it, whether it moved or not, never one a newer landing superseded: what
    /// a read's own judge registers its dependencies by (COWORKVIEW1a, the design §4.1).
    /// </summary>
    public event Action<string, object?>? Landed;

    private void Land(string key, Held entry, long begun, string? at, Task<object?> done)
    {
        if (Took(key, entry, begun, at, done) && done.IsCompletedSuccessfully) Landed?.Invoke(key, done.Result);
    }

    // Whether the landing became the one held.
    private bool Took(string key, Held entry, long begun, string? at, Task<object?> done)
    {
        string[] pages;
        object? answer = null;
        var failed = false;
        lock (_gate)
        {
            if (ReferenceEquals(entry.Flight, done)) entry.Flight = null;
            // An older work landing after a newer one moves nothing.
            if (begun < entry.Landed) return false;
            entry.Landed = begun;
            if (done.IsCompletedSuccessfully)
            {
                answer = done.Result;
                var wire = JsonSerializer.Serialize(answer, s_wire);
                // A numbered read's first landing after a kept failure is told even unchanged: it clears the stale.
                var moved = !entry.Answered || !string.Equals(wire, entry.Wire, StringComparison.Ordinal) || entry.Kept is not null;
                entry.Answer = answer;
                entry.Wire = wire;
                entry.Answered = true;
                entry.Made = at;
                entry.Fault = null;
                entry.Kept = null;
                if (!moved) return true;
                if (entry.Numbered) answer = Reply(entry);
            }
            else if (entry.Numbered)
            {
                // Told with its words: over the held answer as stale, else as the failed value every ask answers until a landing.
                var fault = done.Exception?.InnerException ?? done.Exception;
                if (entry.Answered) entry.Kept = WhyOf(fault);
                else entry.Fault = fault;
                answer = Reply(entry);
            }
            else
            {
                // A failure keeps what was held: the page goes on reading it. With nothing held it is the next ask's refusal.
                if (entry.Answered) return true;
                entry.Fault = done.Exception?.InnerException ?? done.Exception;
                failed = true;
            }

            if (!_held.ContainsKey(key)) return true;
            pages = [.. entry.Pages];
        }

        foreach (var page in pages)
        {
            _bus.Emit(Module, Event, failed ? new { Held = page, Failed = true } : new { Held = page, Answer = answer });
        }

        return true;
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
        public Task Landing = Task.CompletedTask;

        // The versions the work under way began at, and those the answer held was made at (FOUNDATION1s); null where none was read.
        public string? FlightAt;
        public string? Made;
        public long Begun;
        public long Landed;
        public long Asked;
        public readonly HashSet<string> Pages = new(StringComparer.Ordinal);

        // COWORKVIEW1a: a numbered read, and the words of a failure kept over its held answer.
        public bool Numbered;
        public string? Kept;
    }
}
