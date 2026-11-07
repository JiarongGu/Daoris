using System.Collections.Concurrent;
using System.Diagnostics;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The window's landing press, always answered (LANDPRESS1): it runs once per session under the loop's own lifetime, never the asking
/// window's, and is answered within <see cref="Within"/> with what it came to, or held in words while it goes on. A second press of a
/// session whose press runs is held in words too, and runs nothing. Every answer is one <see cref="Event"/> line of the machine log, its
/// outcome <see cref="Landed"/>, <see cref="Accepted"/>, <see cref="Held"/> or <see cref="Refused"/> with its sentence; a press that ends
/// after its held answer tells every window its scopes again, so the review reads what it came to.
/// </summary>
/// <remarks>
/// A press's reads (its gate, its subject, the pass it asks) are git's, and a loaded machine's git answers a status in a minute and a
/// review in three: a press held to the page's wait alone ended past it, or with the window, and nobody heard what it came to.
/// </remarks>
public sealed class LandPresses(DriverLoop loop, IEventBus bus)
{
    /// <summary>
    /// How long a press is waited for before it is held: past the two waits its own body bounds (the pass it asks and the landing it waits
    /// for, <see cref="GraphPasses.Bound"/> each) with room for a working machine's reads, and well inside the page's own bound.
    /// </summary>
    public static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    /// <summary>The machine log's event: one line for each answer a press gave, and one more where it ended after its held answer.</summary>
    public const string Event = "landing.pressed";

    /// <summary>The press landed the work.</summary>
    public const string Landed = "landed";

    /// <summary>The press kept the person's Accept, and the pass lands it.</summary>
    public const string Accepted = "accepted";

    /// <summary>The press was answered before it ended, and goes on.</summary>
    public const string Held = "held";

    /// <summary>The press landed nothing, and its sentence says why.</summary>
    public const string Refused = "refused";

    /// <summary>What a press answers that kept the person's Accept, where the pass has not made the landing yet.</summary>
    public const string Landing = "Accepted; landing. Daoris lands it at its pass, and this window shows it when it has.";

    /// <summary>What it answers where the Accept carried the session on to clear its tree.</summary>
    public const string Carried = "Accepted; its session was carried on to clear its tree, and it lands when the tree is clean.";

    /// <summary>What a press that has not ended within its bound is held with.</summary>
    public static string Slow(string session) =>
        $"The landing of session `{session}` waits on its reads of the tree, its gate and Daoris's pass, which are slow on this machine now; "
        + "it goes on, and this window says when it has landed or why it did not.";

    /// <summary>What a press is held with while another press of the same session runs.</summary>
    public static string UnderWay(string session) =>
        $"A press of session `{session}` is already under way; it goes on, and this window says when it has landed or why it did not.";

    private readonly ConcurrentDictionary<string, Pressing> _running = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How long a press is waited for; <see cref="Bound"/> unless a test names its own.</summary>
    public TimeSpan Within { get; set; } = Bound;

    /// <summary>
    /// The press <paramref name="press"/> of <paramref name="session"/>, run off <paramref name="asked"/> and answered within <see cref="Within"/>:
    /// its own answer or refusal, or held in words while it goes on. <paramref name="scopes"/> are told again where it ends after that.
    /// </summary>
    public async Task<object?> PressAsync(string session, StateScopes scopes, Func<CancellationToken, Task<object?>> press, CancellationToken asked)
    {
        var pressing = new Pressing();
        if (!_running.TryAdd(session, pressing))
        {
            Log(session, Held, UnderWay(session), TimeSpan.Zero, afterHeld: false);
            return HeldAnswer(session, UnderWay(session));
        }

        var run = RunAsync(session, scopes, pressing, press);
        // No ConfigureAwait: the kit's pipeline keeps the caller's context, as the publisher's says. A window that stopped waiting ends
        // the wait alone, never the press.
        await Task.WhenAny(run, Task.Delay(Within, asked));
        if (!run.IsCompleted && pressing.Hold())
        {
            Log(session, Held, Slow(session), pressing.Elapsed, afterHeld: false);
            return HeldAnswer(session, Slow(session));
        }

        return await run;
    }

    /// <summary>
    /// Whether what holds <paramref name="gate"/> is only the review the press itself just accepted: the pass it asked had not come within
    /// its bound, so the gate is the one read before its Accept. That is no refusal, and the press goes on to the landing's wait.
    /// </summary>
    public static bool HeldByOwnAccept(LandingGate gate, bool accepted) =>
        accepted && gate.GraphHolds && gate.GraphPart() is { State: GraphGateStates.Waits, Kind: NodeKinds.Review };

    private async Task<object?> RunAsync(string session, StateScopes scopes, Pressing pressing, Func<CancellationToken, Task<object?>> press)
    {
        try
        {
            object? answer;
            try
            {
                answer = await press(loop.Stopping).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                var late = pressing.End();
                Log(session, Refused, error.Message, pressing.Elapsed, late);
                if (!late) throw;
                StatePublisher.Publish(bus, scopes, session);
                return null;
            }

            var (outcome, said) = Read(answer);
            var after = pressing.End();
            Log(session, outcome, said, pressing.Elapsed, after);
            if (after) StatePublisher.Publish(bus, scopes, session);
            return answer;
        }
        finally
        {
            _running.TryRemove(new KeyValuePair<string, Pressing>(session, pressing));
        }
    }

    /// <summary>What a press's answer came to: every landing door answers <c>Done</c> and <c>Message</c>, whatever else it carries.</summary>
    private static (string Outcome, string Said) Read(object? answer)
    {
        var type = answer?.GetType();
        var done = type?.GetProperty("Done")?.GetValue(answer) is true;
        var said = type?.GetProperty("Message")?.GetValue(answer) as string ?? "";
        return (done ? Landed : said is Landing or Carried ? Accepted : Refused, said);
    }

    private void Log(string session, string outcome, string said, TimeSpan elapsed, bool afterHeld) =>
        loop.Log?.Info(Event, ("session", session), ("outcome", outcome), ("ms", (long)elapsed.TotalMilliseconds), ("afterHeld", afterHeld), ("said", said));

    /// <summary>A held press's answer: the fields a refused press carries, nothing landed, its sentence, and <c>held</c>.</summary>
    private static object HeldAnswer(string session, string sentence) => new
    {
        Session = session, Done = false, Message = sentence, Branch = (string?)null,
        Review = (object?)null, Opinion = (object?)null, Workflow = (object?)null, LeftOut = (object?)null, Conflict = (object?)null,
        Said = TreesWords.Land(new TreeLanding(false, sentence), []).ToWire(), Followed = Array.Empty<string>(), Held = true,
    };

    /// <summary>One press's race with its bound: whichever of its end and its hold comes first decides whether its end comes after.</summary>
    private sealed class Pressing
    {
        private const int Running = 0;
        private const int Answered = 1;
        private const int HeldAlready = 2;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private int _state;

        public TimeSpan Elapsed => _clock.Elapsed;

        /// <summary>The bound came first: true where the press had not ended, so it is held.</summary>
        public bool Hold() => Interlocked.CompareExchange(ref _state, HeldAlready, Running) == Running;

        /// <summary>The press ended: true where it was held already, so its end came after its answer.</summary>
        public bool End() => Interlocked.CompareExchange(ref _state, Answered, Running) == HeldAlready;
    }
}
