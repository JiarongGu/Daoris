using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[CollectionDefinition("agent operation ports", DisableParallelization = true)]
public sealed class AgentOperationPortsCollection;

[Collection("agent operation ports")]
public sealed class AgentWorkOrdersPortsTests
{
    private static SessionTarget Target() => new("q1", "Title", "Body", "ask #a1", "engine", "C:/stand/engine", "http://localhost");
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-01T02:26:00Z");

    [Fact]
    public void Every_foundation_port_fixture_runs_with_no_unrelated_consumer_beside_it()
    {
        var definition = Assert.Single(typeof(AgentOperationPortsCollection)
            .GetCustomAttributes(typeof(CollectionDefinitionAttribute), inherit: false).Cast<CollectionDefinitionAttribute>());
        Assert.True(definition.DisableParallelization);
        var name = Assert.Single(typeof(AgentOperationPortsCollection).CustomAttributes
            .Where(attribute => attribute.AttributeType == typeof(CollectionDefinitionAttribute))).ConstructorArguments[0].Value;
        // Workplace replaces ChildGit as well as AgentWorkplace; both need the same isolation.
        Type[] replacements = [typeof(ParticipantPortsTests), typeof(AgentPluginPortsTests),
            typeof(AgentWorkplacePortsTests), typeof(AgentWorkOrdersPortsTests),
            typeof(ServicePortsTests), typeof(InstallPortsTests), typeof(KnowledgePortsTests), typeof(AgentContextPortsTests), typeof(AgentCoursePortsTests),
            typeof(WorkplaceContextPortsTests)];
        foreach (var type in replacements)
        {
            var collection = Assert.Single(type.CustomAttributes.Where(attribute => attribute.AttributeType == typeof(CollectionAttribute)));
            Assert.Equal(name, collection.ConstructorArguments[0].Value);
        }
    }

    [Fact]
    public void The_actual_port_reads_unrelated_ask_facts_and_keeps_quoting_bounds_and_serialized_properties()
    {
        Assert.IsType<WorkOrderAgentWords>(AgentWorkWords.Current);
        var said = new AskWordView[]
        {
            new(AskWordView.Asked, "First line\nSecond line", At),
            new(AskWordView.Added, new string('x', AskWordsText.WordLimit + 20), At.AddMinutes(1)),
            new(AskWordView.Answered, "exact answer", At.AddMinutes(2), "s1", "q1"),
        };
        var original = new AskWords("a1", said, At) { GoAheads = [] };
        IAgentAskWords stand = new Words(said);
        Assert.Equal(AskWordsText.Beneath(original, "q1"), AgentWorkWords.Current.AskWords(stand, "q1"));
        Assert.Contains("20 more characters", AgentWorkWords.Current.AskWords(stand, "q1"));
        Assert.True(AgentWorkWords.Current.Quotes(stand, " exact answer "));
        Assert.False(AgentWorkWords.Current.Quotes(stand, "another answer"));
        Assert.Equal(GoAheadsText.Beneath(original), AgentWorkWords.Current.GoAheads(stand));
        var target = Target() with { Words = original };
        Assert.Same(original, target.Words);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.SerializeToElement(target).GetProperty("Words").GetRawText());
        Assert.Equal(JsonValueKind.Null, JsonSerializer.SerializeToElement(Target()).GetProperty("Words").ValueKind);
        var messages = new ChannelItemView[] { new(ChannelItemView.Message, At) { Id = "m1", Person = true, Text = "Keep this\nverbatim" } };
        var channel = new ChannelHeld("a1", "entry", messages, "s1");
        Assert.Equal(ChannelText.Beneath(channel), AgentWorkWords.Current.Channel(channel));
        Assert.Same(messages, channel.Messages);
        Assert.Equal("s1", channel.Session);
    }

    [Fact]
    public void An_independent_renderer_receives_original_inputs_in_instruction_order()
    {
        var actual = AgentWorkWords.Current;
        var words = new Words([]);
        var channel = new ChannelHeld("a1", "entry", [], "s1");
        var stand = new Renderer();
        try
        {
            AgentWorkWords.Current = stand;
            var prompt = TargetPrompt.Compose(Target() with { Words = words, Channel = channel });
            Assert.Equal(["ask:q1", "go-aheads", "channel"], stand.Calls);
            Assert.True(prompt.IndexOf("stand ask", StringComparison.Ordinal) < prompt.IndexOf("stand go-aheads", StringComparison.Ordinal));
            Assert.True(prompt.IndexOf("stand go-aheads", StringComparison.Ordinal) < prompt.IndexOf("stand channel", StringComparison.Ordinal));
            Assert.Same(words, stand.Words);
            Assert.Same(channel, stand.Held);
            Assert.True(AgentWorkWords.Current.Quotes(words, "answer"));
        }
        finally { AgentWorkWords.Current = actual; }
        Assert.Same(actual, AgentWorkWords.Current);
    }

    [Fact]
    public void A_missing_renderer_refuses_and_refusal_or_cancellation_reaches_each_operation_unchanged()
    {
        var actual = AgentWorkWords.Current;
        try
        {
            AgentWorkWords.Current = null!;
            Assert.Throws<InvalidOperationException>(() => TargetPrompt.Compose(Target()));
            foreach (var failure in new Exception[] { new IOException("refused"), new OperationCanceledException(new CancellationToken(true)) })
            {
                AgentWorkWords.Current = new Renderer { Failure = failure };
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => TargetPrompt.Compose(Target())));
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => AgentWorkWords.Current.Quotes(null, "answer")));
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => AgentWorkWords.Current.GoAheads(null)));
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => AgentWorkWords.Current.Channel(null)));
            }
        }
        finally { AgentWorkWords.Current = actual; }
    }

    [Fact]
    public void Work_names_keep_the_original_order_and_higher_constants_and_clear_every_inherited_role()
    {
        Assert.Equal(new[]
        {
            "DAORIS_QUEST_ID", "DAORIS_QUEST_TITLE", "DAORIS_QUEST_BODY", "DAORIS_QUEST_ASKER", "DAORIS_QUEST_ATTACHMENTS",
            "DAORIS_TARGET", "DAORIS_ASK_ID", "DAORIS_SESSION_ID", "DAORIS_BRAINSTORM_ID", "DAORIS_BRAINSTORM_TURN",
            "DAORIS_BRAINSTORM_ANSWER", "DAORIS_BRAINSTORM_ROOM", "DAORIS_REPOSITORY", "DAORIS_SERVICE_URL",
        }, Spawning.Roles);
        Assert.Equal(IntakeRoom.AskVariable, AgentWorkVariables.Ask);
        Assert.Equal(IntakeRoom.SessionVariable, ChildMarks.SessionVariable);
        Assert.Equal(new[] { BrainstormVariables.Id, BrainstormVariables.Turn, BrainstormVariables.Answer, BrainstormVariables.Room },
            new[] { AgentWorkVariables.Brainstorm, AgentWorkVariables.Turn, AgentWorkVariables.Answer, AgentWorkVariables.Room });
        var environment = Spawning.Roles.ToDictionary(name => name, _ => (string?)"inherited");
        environment["unrelated"] = "keep";
        Spawning.Unrole(environment);
        Assert.Equal(new KeyValuePair<string, string?>("unrelated", "keep"), Assert.Single(environment));
    }

    [Theory]
    [InlineData(null, "Done", null, null, false, false, false)]
    [InlineData("Taken", "Done", "new", null, true, false, true)]
    [InlineData("Taken", "Open", null, null, true, true, false)]
    [InlineData("Taken", "Taken", "new", null, false, true, false)]
    [InlineData("Taken", "Taken", "old", "old", false, false, true)]
    [InlineData("Taken", "Open", "OLD", "old", false, false, false)]
    [InlineData("Open", "Open", "old", "old", false, false, true)]
    [InlineData("Open", "Open", "new", "old", false, false, true)]
    [InlineData("Taken", "Open", null, null, false, true, true)]
    [InlineData("Taken", "Open", null, null, false, false, false)]
    [InlineData("Taken", "Taken", null, null, false, false, true)]
    [InlineData("Done", "Open", null, null, false, false, false)]
    public async Task Wanted_keeps_missing_then_start_status_then_submission_question_status_and_take_order(
        string? status, string started, string? awaits, string? before, bool submitted, bool took, bool wanted)
    {
        using var doors = new Doors(status, awaits, submitted, took);
        using var client = new ServiceClient(doors);
        Assert.Equal(wanted, await TransientRetries.WantedAsync(client, "q1", "s1", started, before, CancellationToken.None));
        var readsTake = status == "Taken" && started == "Open" && !submitted && (awaits is null || awaits == before);
        Assert.Equal(readsTake
            ? new[] { "/api/quests?includeClosed=true", "/api/sessions?includeClosed=true" }
            : ["/api/quests?includeClosed=true"], doors.Calls);
    }

    [Fact]
    public async Task A_query_refusal_is_still_a_yes_to_the_retry_but_its_own_cancellation_is_not()
    {
        using var doors = new Doors("Open", null, false, false) { Failure = new DriverException("query refused") };
        using var client = new ServiceClient(doors);
        var retry = new TransientRetry
        {
            Read = _ => throw new NotSupportedException(),
            Wanted = ct => TransientRetries.WantedAsync(client, "q1", "s1", "Open", null, ct),
        };
        Assert.True(await retry.WantedAsync(CancellationToken.None));
        doors.Failure = new OperationCanceledException(new CancellationToken(true));
        Assert.True(await retry.WantedAsync(CancellationToken.None));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retry.WantedAsync(new CancellationToken(true)));
    }

    private sealed record Words(IReadOnlyList<AskWordView>? Said) : IAgentAskWords
    {
        public string Ask => "a1";
        public DateTimeOffset? KeptFrom => At;
        public IReadOnlyList<GoAheadView>? GoAheads => [];
    }
    private sealed class Renderer : IAgentWorkWords
    {
        public Exception? Failure { get; init; }
        public List<string> Calls { get; } = [];
        public IAgentAskWords? Words { get; private set; }
        public ChannelHeld? Held { get; private set; }
        private void Check() { if (Failure is { } failure) throw failure; }
        public bool Quotes(IAgentAskWords? words, string text) { Check(); return true; }
        public string AskWords(IAgentAskWords? words, string questId) { Check(); Words = words; Calls.Add("ask:" + questId); return "\nstand ask\n"; }
        public string GoAheads(IAgentAskWords? words) { Check(); Words = words; Calls.Add("go-aheads"); return "\nstand go-aheads\n"; }
        public string Channel(ChannelHeld? held) { Check(); Held = held; Calls.Add("channel"); return "\nstand channel\n"; }
    }
    private sealed class Doors(string? status, string? awaits, bool submitted, bool took) : IServiceDoors
    {
        public string BaseUrl => "http://stand-in";
        public List<string> Calls { get; } = [];
        public Exception? Failure { get; set; }
        public void Dispose() { }
        public Task<string> GetAsync(string path, CancellationToken ct)
        {
            Calls.Add(path);
            if (Failure is { } failure) return Task.FromException<string>(failure);
            if (path.StartsWith("/api/sessions", StringComparison.Ordinal)) return Task.FromResult(took ? "[{\"id\":\"s1\",\"took\":true}]" : "[]");
            var quest = status is null ? "[]" : JsonSerializer.Serialize(new[] { new
            {
                id = "q1", from = "ask #a1", to = "engine", title = "Title", body = "Body", status, awaits,
                closure = submitted ? "graph" : null,
                submission = submitted ? new { machine = "machine", sequence = 1 } : null,
            } });
            return Task.FromResult(quest);
        }
        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();
        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct) => throw new NotSupportedException();
    }
}
