using Daoris.Driver;
using Lyntai.Agents;
using Lyntai.Inference;
using SessionEnded = Lyntai.Agents.SessionEnded;
using SessionStarted = Lyntai.Agents.SessionStarted;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The events' translation (the agent-lyntai design §5.4): each layer event read once into the readings today's native capture hands its
/// writer for the same thing, in its order (a failure first, then the conversation named, the lines shown, the events kept), one terminal
/// a turn, its failure entering through the ingress and a stop Daoris asked read as a stop. On events the tests make.
/// </summary>
public sealed class LayerReadingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private static LayerReadings Readings() => new("pipe", new FixedClock(Now), TimeSpan.FromHours(1));

    [Fact]
    public void The_agent_s_words_are_its_lines_shown_and_one_message_kept()
    {
        var readings = Readings().Read(new TextDelta("first\nsecond"));

        Assert.Collection(
            readings,
            each => Assert.Equal("first", Assert.IsType<WireReading.Shown>(each).Text),
            each => Assert.Equal("second", Assert.IsType<WireReading.Shown>(each).Text),
            each => Assert.Equal(
                (SessionEventKind.Message, "first\nsecond"),
                (Assert.IsType<WireReading.Said>(each).Event.Kind, ((WireReading.Said)each).Event.Text)));
    }

    [Fact]
    public void The_conversation_is_named_once_and_the_first_id_kept()
    {
        var translation = Readings();

        var first = translation.Read(new SessionStarted("conv-1"));
        var again = translation.Read(new SessionStarted("conv-2"));

        Assert.Equal("conv-1", Assert.IsType<WireReading.ConversationNamed>(Assert.Single(first)).Conversation);
        Assert.Empty(again);
        Assert.Equal("conv-1", translation.Conversation);
    }

    [Fact]
    public void A_tool_call_and_its_result_are_said_by_the_call_s_name_never_its_id()
    {
        var translation = Readings();

        var called = translation.Read(new ToolCall("Read", """{"path":"a"}""", "c1"));
        var returned = translation.Read(new ToolResult("c1", "its words", IsError: false));
        var failed = translation.Read(new ToolResult("c9", "", IsError: true));

        Assert.Equal("→ Read", Assert.IsType<WireReading.Shown>(called[0]).Text);
        var call = Assert.IsType<WireReading.Said>(called[1]).Event;
        Assert.Equal((SessionEventKind.Tool, "c1", "in_progress"), (call.Kind, call.Id, call.Status));
        Assert.Equal("  ✓ Read", Assert.IsType<WireReading.Shown>(returned[0]).Text);
        var result = Assert.IsType<WireReading.Said>(returned[1]).Event;
        Assert.Equal(("completed", "its words"), (result.Status, Assert.Single(result.Content!).Text));
        Assert.Equal("  ✗ a tool failed", Assert.IsType<WireReading.Shown>(failed[0]).Text);
    }

    [Fact]
    public void A_turn_that_ended_is_its_line_and_its_turn_with_the_reported_counts()
    {
        var translation = Readings();

        Assert.Empty(translation.Read(new UsageFinal(10, 20, 3, 4, "the-model")));
        var ended = translation.Read(new SessionEnded(ProviderVerdict.Ok, false, "success", "conv-1", "done", null));

        Assert.Equal("— the turn ended: success", Assert.IsType<WireReading.Shown>(ended[0]).Text);
        var turn = Assert.IsType<WireReading.Said>(ended[1]).Event;
        Assert.Equal((SessionEventKind.Turn, "end_turn"), (turn.Kind, turn.StopReason));
        Assert.Equal(new TurnTokens(10, 20, 3, 4), turn.Tokens);
        Assert.Null(translation.Failure);
    }

    [Fact]
    public void A_failed_turn_s_failure_is_its_first_reading_and_enters_through_the_ingress()
    {
        var translation = Readings();

        var ended = translation.Read(new SessionEnded(ProviderVerdict.Failed, true, "error_during_execution", null, null, "the words")
        {
            Failure = new AgentFailure(AgentFailureKind.Limit, "tests:limit"),
        });

        var failed = Assert.IsType<WireReading.Failed>(ended[0]).Failure;
        Assert.Equal(("pipe", DoorWhere.Result, "the words"), (failed.Door, failed.Where, failed.Words));
        Assert.Equal(("layer:tests:limit", AgentErrorKind.Limit), (translation.Failure!.By, translation.Failure.Kind));
        Assert.Equal("— the turn failed (error_during_execution): the words", Assert.IsType<WireReading.Shown>(ended[1]).Text);
    }

    [Fact]
    public void One_terminal_a_turn()
    {
        var translation = Readings();

        translation.Read(new SessionEnded(ProviderVerdict.Ok, false, "success", null, null, null));

        Assert.Empty(translation.Read(new SessionEnded(ProviderVerdict.Failed, true, null, null, null, "late")));
        Assert.Null(translation.Failure);
    }

    [Fact]
    public void A_stop_daoris_asked_before_the_terminal_is_a_stop_never_the_fatal_exit()
    {
        var translation = Readings();
        translation.Stop();

        var ended = translation.Read(new SessionEnded(ProviderVerdict.Failed, true, null, null, null, "exited 1")
        {
            Failure = new AgentFailure(AgentFailureKind.Fatal, "process.exit"),
        });

        Assert.DoesNotContain(ended, each => each is WireReading.Failed);
        Assert.Equal("— the turn was stopped", Assert.IsType<WireReading.Shown>(ended[0]).Text);
        Assert.Equal("cancelled", Assert.IsType<WireReading.Said>(ended[1]).Event.StopReason);
        Assert.Null(translation.Failure);
    }

    [Fact]
    public void A_protocol_door_s_context_figures_are_said_as_the_agent_sent_them_and_never_inferred()
    {
        var translation = Readings();

        var both = Assert.IsType<WireReading.Said>(Assert.Single(translation.Read(new ContextReading(150, 200000, Now)))).Event;
        var window = Assert.IsType<WireReading.Said>(Assert.Single(translation.Read(new ContextReading(null, 200000, Now)))).Event;

        Assert.Equal((SessionEventKind.Usage, 150L, 200000L), (both.Kind, both.Used, both.Size));
        Assert.Equal((null, 200000L), (window.Used, window.Size));
        Assert.Empty(translation.Read(new ContextReading(null, null, Now)));
    }

    [Fact]
    public void A_start_the_agent_did_not_announce_names_no_conversation_and_the_first_announced_one_does()
    {
        var translation = Readings();

        Assert.Empty(translation.Read(new SessionStarted("status-id") { Announced = false }));
        var named = translation.Read(new SessionStarted("conv-1"));

        Assert.Equal("conv-1", Assert.IsType<WireReading.ConversationNamed>(Assert.Single(named)).Conversation);
        Assert.Equal("conv-1", translation.Conversation);
    }

    [Fact]
    public void A_turn_s_report_is_kept_for_its_terminal_and_a_denial_of_an_unseen_call_is_a_line_alone()
    {
        var translation = Readings();
        translation.Read(new ToolCall("Read", "{}", "c1"));
        translation.Read(new UsageLive(100, 5, 50));

        Assert.Empty(translation.Read(new AgentToolDenied("Read", "c1", null)));
        Assert.Empty(translation.Read(new AgentToolDenied("Read", "c1", "twice")));
        Assert.Empty(translation.Read(new AgentToolDenied("Edit", "c9", null)));
        Assert.Empty(translation.Read(new AgentToolDenied("Edit", null, null)));
        Assert.Empty(translation.Read(new AgentModelUsage([new("m1", 100, 5, 50, 0, 200000, true)], Now)));
        var ended = translation.Read(new SessionEnded(ProviderVerdict.Ok, false, "success", null, null, null));

        Assert.Equal(
            ["  permission refused: Read", "  permission refused: Edit", "— the turn ended: success"],
            ended.OfType<WireReading.Shown>().Select(each => each.Text));
        var said = ended.OfType<WireReading.Said>().Select(each => each.Event).ToList();
        Assert.Equal(("refused", "Read"), (said[0].Status, said[0].ToolName));
        Assert.Equal((SessionEventKind.Usage, 150L, 200000L), (said[1].Kind, said[1].Used, said[1].Size));
        Assert.Equal(SessionEventKind.Turn, said[2].Kind);
        Assert.Equal(new AcpUsage(150, 200000), translation.Usage);
    }

    [Fact]
    public void A_context_without_a_window_or_a_window_without_a_context_is_no_usage_record()
    {
        var noWindow = Readings();
        noWindow.Read(new UsageLive(100, 5, 50));
        var withoutWindow = noWindow.Read(new SessionEnded(ProviderVerdict.Ok, false, "success", null, null, null));

        var noContext = Readings();
        noContext.Read(new AgentModelUsage([new("m1", 1, 1, 1, 1, 200000, true)], Now));
        var withoutContext = noContext.Read(new SessionEnded(ProviderVerdict.Ok, false, "success", null, null, null));

        Assert.DoesNotContain(withoutWindow.OfType<WireReading.Said>(), each => each.Event.Kind == SessionEventKind.Usage);
        Assert.DoesNotContain(withoutContext.OfType<WireReading.Said>(), each => each.Event.Kind == SessionEventKind.Usage);
        Assert.Null(noWindow.Usage);
        Assert.Null(noContext.Usage);
    }

    private static readonly AgentRestriction OwnersModel = new(RestrictionCause.Unknown, RestrictionSubject.Model, RestrictionRecovery.OwnerAction);

    private static bool Refused(
        string door, AgentFailureKind kind, string by, AgentRestriction? restriction, bool speaks = false, bool stops = false)
    {
        var translation = new LayerReadings(door, new FixedClock(Now), TimeSpan.FromHours(1), new LayerRefusals([LayerTestProvider.RefusedBy]));
        if (speaks) translation.Read(new TextDelta("words"));
        if (stops) translation.Stop();
        translation.Read(new SessionEnded(ProviderVerdict.Failed, true, "error_during_execution", null, null, "why")
        {
            Failure = new AgentFailure(kind, by) { Restriction = restriction },
        });
        return translation.ModelRefused;
    }

    [Fact]
    public void A_refused_model_is_the_ending_s_flag_by_its_whole_outcome_on_a_native_turn_that_ran_nothing()
    {
        Assert.True(Refused("pipe", AgentFailureKind.Fatal, LayerTestProvider.RefusedBy, OwnersModel));

        // A limit and a capacity scoped to the model, another entry, no restriction, a turn that spoke, a stop, a protocol open's turn: none.
        Assert.False(Refused("pipe", AgentFailureKind.Limit, LayerTestProvider.RefusedBy, OwnersModel with { Cause = RestrictionCause.Credit }));
        Assert.False(Refused(
            "pipe", AgentFailureKind.Transient, LayerTestProvider.RefusedBy, new(RestrictionCause.Capacity, RestrictionSubject.Model, RestrictionRecovery.Unknown)));
        Assert.False(Refused("pipe", AgentFailureKind.Fatal, "tests:other", OwnersModel));
        Assert.False(Refused("pipe", AgentFailureKind.Fatal, LayerTestProvider.RefusedBy, null));
        Assert.False(Refused("pipe", AgentFailureKind.Fatal, LayerTestProvider.RefusedBy, OwnersModel, speaks: true));
        Assert.False(Refused("pipe", AgentFailureKind.Fatal, LayerTestProvider.RefusedBy, OwnersModel, stops: true));
        Assert.False(Refused("protocol", AgentFailureKind.Fatal, LayerTestProvider.RefusedBy, OwnersModel));
    }

    [Fact]
    public void A_child_s_words_do_not_count_as_the_turn_having_run()
    {
        var translation = new LayerReadings("pipe", new FixedClock(Now), TimeSpan.FromHours(1), new LayerRefusals([LayerTestProvider.RefusedBy]));
        translation.Read(new TextDelta("a child's") { ParentCallId = "c1" });
        translation.Read(new SessionEnded(ProviderVerdict.Failed, true, "error_during_execution", null, null, "why")
        {
            Failure = new AgentFailure(AgentFailureKind.Fatal, LayerTestProvider.RefusedBy) { Restriction = OwnersModel },
        });

        Assert.True(translation.ModelRefused);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
