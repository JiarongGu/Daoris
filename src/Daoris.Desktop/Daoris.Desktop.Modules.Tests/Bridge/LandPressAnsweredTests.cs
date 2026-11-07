using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The window's landing press is always answered. It is answered within its bound with what it came to, or held in words
/// while it goes on off the asking window's wait; a second press of a session whose press runs is held, never run twice. Every answer
/// is the machine log's <c>landing.pressed</c> with its outcome and sentence, and a press that ends after its held answer tells the
/// windows again, so the review reads what it came to.
/// </summary>
public sealed class LandPressAnsweredTests : DriverModuleBridge
{
    private static readonly StateScopes Scopes = StateScopes.Sessions | StateScopes.Landing;

    private DriverLoop LoggedLoop(MachineLog log) => new(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0", log: log);

    private MachineLog Log() => new(Home, "desktop", () => new DateTimeOffset(2026, 11, 7, 3, 17, 0, TimeSpan.Zero));

    /// <summary>The <c>landing.pressed</c> lines written so far, each its data, in the order written.</summary>
    private JsonElement[] Pressed()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.GetFiles(folder).SelectMany(file =>
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).Select(line => JsonDocument.Parse(line).RootElement)
          .Where(line => line.GetProperty("event").GetString() == LandPresses.Event)
          .Select(line => line.GetProperty("data").Clone())];
    }

    private bool Told(string session) => Raised.Any(message => message.Type == StatePublisher.Type
        && message.Payload is StateChanged { Sessions: { } sessions } && sessions.Contains(session));

    private static object Landed(string session) =>
        TreesRoutes.Pressed(session, new TreeLanding(true, "Landed on `main`."), LandingGate.Open("engine"),
            TreesWords.Land(new TreeLanding(true, "Landed on `main`."), []), []);

    [Fact]
    public async Task A_press_slower_than_its_bound_is_held_in_words_and_then_lands_logged_and_told()
    {
        using var log = Log();
        var presses = new LandPresses(LoggedLoop(log), Bus) { Within = TimeSpan.FromMilliseconds(50) };
        var reads = new TaskCompletionSource();

        var answer = JsonSerializer.SerializeToElement(await presses.PressAsync("s1a2b3c4", Scopes, async _ =>
        {
            // The precondition's read, slow as a loaded machine's git.
            await reads.Task;
            return Landed("s1a2b3c4");
        }, CancellationToken.None));

        Assert.False(answer.GetProperty("Done").GetBoolean());
        Assert.True(answer.GetProperty("Held").GetBoolean());
        Assert.Equal(LandPresses.Slow("s1a2b3c4"), answer.GetProperty("Message").GetString());
        var held = Assert.Single(Pressed());
        Assert.Equal((LandPresses.Held, LandPresses.Slow("s1a2b3c4")), (held.GetProperty("outcome").GetString(), held.GetProperty("said").GetString()));
        Assert.False(Told("s1a2b3c4"));

        reads.SetResult();
        await UntilAsync(() => Pressed().Length == 2 && Told("s1a2b3c4"));
        var ended = Pressed()[1];
        Assert.Equal((LandPresses.Landed, "Landed on `main`."), (ended.GetProperty("outcome").GetString(), ended.GetProperty("said").GetString()));
        Assert.True(ended.GetProperty("afterHeld").GetBoolean());
    }

    [Fact]
    public async Task A_second_press_while_the_first_runs_is_held_and_runs_nothing_twice()
    {
        using var log = Log();
        var presses = new LandPresses(LoggedLoop(log), Bus) { Within = TimeSpan.FromMilliseconds(50) };
        var reads = new TaskCompletionSource();
        var ran = 0;
        Task<object?> Press(CancellationToken _)
        {
            Interlocked.Increment(ref ran);
            return reads.Task.ContinueWith(_ => (object?)Landed("s1a2b3c4"), TaskScheduler.Default);
        }

        await presses.PressAsync("s1a2b3c4", Scopes, Press, CancellationToken.None);
        var again = JsonSerializer.SerializeToElement(await presses.PressAsync("s1a2b3c4", Scopes, Press, CancellationToken.None));

        Assert.Equal(1, ran);
        Assert.Equal(LandPresses.UnderWay("s1a2b3c4"), again.GetProperty("Message").GetString());
        Assert.Equal([LandPresses.Held, LandPresses.Held], Pressed().Select(line => line.GetProperty("outcome").GetString()));
        reads.SetResult();
        await UntilAsync(() => Pressed().Length == 3);
    }

    [Fact]
    public async Task A_press_whose_window_stopped_waiting_still_goes_on_and_is_logged()
    {
        using var log = Log();
        var presses = new LandPresses(LoggedLoop(log), Bus);
        var reads = new TaskCompletionSource();
        using var window = new CancellationTokenSource();
        CancellationToken seen = default;

        var asked = presses.PressAsync("s1a2b3c4", Scopes, async token =>
        {
            seen = token;
            await reads.Task;
            return Landed("s1a2b3c4");
        }, window.Token);
        await window.CancelAsync();
        var answer = JsonSerializer.SerializeToElement(await asked);

        Assert.True(answer.GetProperty("Held").GetBoolean());
        Assert.False(seen.IsCancellationRequested);
        reads.SetResult();
        await UntilAsync(() => Pressed().Length == 2);
        Assert.Equal(LandPresses.Landed, Pressed()[1].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task A_refused_press_is_answered_with_why_and_logged_with_it()
    {
        using var log = Log();
        var loop = LoggedLoop(log);
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new SlowService()))));

        var answer = await AnswerAsync(module, "LAND_SESSION_TREE", new { id = "s1a2b3c4" });

        var why = TreesWords.NoTreeToLand("s1a2b3c4").Error[0]["trees: ".Length..];
        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(why, answer.GetProperty("message").GetString());
        var line = Assert.Single(Pressed());
        Assert.Equal((LandPresses.Refused, why), (line.GetProperty("outcome").GetString(), line.GetProperty("said").GetString()));
    }

    [Fact]
    public async Task A_press_whose_records_read_is_slow_is_held_then_refused_in_words_logged_and_told()
    {
        using var log = Log();
        var loop = LoggedLoop(log);
        var module = new DriverModule(Bus, loop) { PressBound = TimeSpan.FromMilliseconds(100) };
        var service = new SlowService();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(service))));
        service.Slow();

        var answer = await AnswerAsync(module, "LAND_SESSION_TREE", new { id = "s1a2b3c4" });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.True(answer.GetProperty("held").GetBoolean());
        Assert.Equal(LandPresses.Slow("s1a2b3c4"), answer.GetProperty("message").GetString());
        Assert.Equal(LandPresses.Held, Assert.Single(Pressed()).GetProperty("outcome").GetString());

        service.Answer();
        await UntilAsync(() => Pressed().Length == 2 && Told("s1a2b3c4"));
        var ended = Pressed()[1];
        Assert.Equal(LandPresses.Refused, ended.GetProperty("outcome").GetString());
        Assert.Equal(TreesWords.NoTreeToLand("s1a2b3c4").Error[0]["trees: ".Length..], ended.GetProperty("said").GetString());
    }

    /// <summary>
    /// The press kept the person's Accept and the pass it asked had not come within its bound, so the gate it holds is the one read
    /// before its own Accept: that hold is no refusal, and the press goes on to the landing's wait.
    /// </summary>
    [Fact]
    public void A_gate_held_only_by_the_review_the_press_just_accepted_is_no_refusal()
    {
        var waits = LandingGate.Open("engine") with
        {
            Graph = new GraphGateState(GraphGateStates.Waits, "q1") { Kind = NodeKinds.Review, Node = "yes", Commit = "abc123" },
        };
        var look = waits with { Graph = new GraphGateState(GraphGateStates.Waits, "q1") { Kind = NodeKinds.Look, Node = "look", Commit = "abc123" } };

        Assert.True(LandPresses.HeldByOwnAccept(waits, accepted: true));
        Assert.False(LandPresses.HeldByOwnAccept(waits, accepted: false));
        Assert.False(LandPresses.HeldByOwnAccept(look, accepted: true));
        Assert.False(LandPresses.HeldByOwnAccept(LandingGate.Open("engine"), accepted: true));
    }

    /// <summary>A service holding no session, whose session records answer only once <see cref="Answer"/> is called after <see cref="Slow"/>.</summary>
    private sealed class SlowService : HttpMessageHandler
    {
        private volatile TaskCompletionSource? _held;

        public void Slow() => _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Answer() => _held?.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (_held is { } held && request.RequestUri!.AbsolutePath.StartsWith("/api/sessions", StringComparison.Ordinal))
            {
                await held.Task.WaitAsync(ct).ConfigureAwait(false);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
