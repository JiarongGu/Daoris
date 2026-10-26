using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class ActorOpeningTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "actor-opening", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private sealed class Events : ISessionEvents
    {
        public event Action<string, SessionEvent>? Evented;
        public List<(string Session, SessionEvent Event, Action<string>? Say)> Written { get; } = [];
        public void Keep(string sessionId, SessionEvent e, Action<string>? say)
        {
            Written.Add((sessionId, e, say));
            Evented?.Invoke(sessionId, e);
        }
    }

    private static readonly ActorEffective Ran = new("codex-acp", "small", "high", "3");
    private static readonly BuildPass[] Passed =
    [
        new(new("claude-code-acp", "first", null, "1"), "not-offered", "not offered."),
        new(new("codex-acp", null, null, "2"), "cooling", "cooling."),
    ];

    [Fact]
    public void An_actor_opening_keeps_its_exact_order_and_facts_on_real_and_unrelated_event_sinks()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var real = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var written = new List<SessionEvent>();
        real.Evented += (_, e) => written.Add(e);
        var standIn = new Events();
        foreach (var events in new ISessionEvents[] { real, standIn })
        {
            events.Keep("s1", new SessionEvent { Kind = SessionEventKind.Note, Text = "before" }, null);
            RotatedOpening.SayActor(events, "s1", "build", Ran, Passed);
            events.Keep("s1", new SessionEvent { Kind = SessionEventKind.Note, Text = "after" }, null);
        }

        const string expected = "Ran as Build's choice 3, codex-acp · small: choice 1, claude-code-acp · first: not offered; choice 2, codex-acp · account default: cooling.";
        Assert.Equal(new[] { "before", expected, "after" }, written.Select(e => e.Text));
        Assert.Equal(written.Select(e => e.Text), standIn.Written.Select(e => e.Event.Text));
        Assert.All(standIn.Written, entry => { Assert.Equal("s1", entry.Session); Assert.Null(entry.Say); });
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(written[1].Parts), System.Text.Json.JsonSerializer.Serialize(standIn.Written[1].Event.Parts));
        var part = Assert.Single(written[1].Parts!);
        Assert.Equal("opening.actor", part.Code);
        Assert.Equal("build", part.Value("actor"));
        Assert.Equal(3, part.Value("n"));
        Assert.Equal("codex-acp · small", part.Value("entry"));
        Assert.Equal("choice 1, claude-code-acp · first: not offered; choice 2, codex-acp · account default: cooling", part.Value("passed"));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", true)]
    [InlineData("before", true)]
    [InlineData(" 2", true)]
    [InlineData("+2", true)]
    [InlineData("2", false)]
    public void A_first_or_non_order_choice_or_no_passes_emits_nothing(string from, bool passes)
    {
        var events = new Events();
        RotatedOpening.SayActor(events, "s1", "build", Ran with { From = from }, passes ? Passed : []);
        Assert.Empty(events.Written);
    }

    [Fact]
    public void Capture_keeps_owner_supervisor_and_Daoris_words_in_their_supplied_order_and_authorship()
    {
        var at = DateTimeOffset.Parse("2026-10-27T00:00:00Z");
        var events = HarnessCapture.Opening("task", "typed", handed:
        [
            new("p", "owner words", at, [], true),
            new("s", "supervisor words", at, [], false) { From = SaidFrom.Supervisor },
            new("d", "Daoris words", at, [], false) { From = DaorisWords.From },
        ]);

        Assert.Equal(["task", "typed", "owner words", "supervisor words", "Daoris words"], events.Select(e => e.Text));
        Assert.Equal([null, null, null, SaidFrom.Supervisor, DaorisWords.From], events.Select(e => e.From));
        Assert.Equal([null, null, "p", "s", "d"], events.Select(e => e.Id));
    }
}
