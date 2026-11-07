using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census over <see cref="HookFrames.Event"/>. Every kind's frame reads against
/// <see cref="WorkEventGrammar"/>; a field outside the kind's closed list, a name, and an id outside its shape fail it; a required field
/// that fails is told as <c>events.missed</c> in its place, and an optional one, an adapter outside the build's names among them, is left out.
/// </summary>
public sealed class WorkEventGrammarTests
{
    private const string Run = "6c0f1a9e2b47";

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-24T11:02:03Z");

    private static readonly IReadOnlySet<string> Adapters = new HashSet<string>(["claude-code", "codex"], StringComparer.Ordinal);

    private static readonly WorkEvent Parked = new(WorkEventKinds.SessionParked, At) { Session = "4b62a9fe", Quest = "48f8ed9121ec", Ask = "a1b2c3" };

    private static readonly WorkEvent Ended = new(WorkEventKinds.SessionEnded, At) { Session = "4b62a9fe", Quest = "48f8ed", Ask = "a1b2c3", State = "failed", Adapter = "codex" };

    private static readonly IReadOnlyList<WorkEvent> Every =
    [
        Parked,
        new WorkEvent(WorkEventKinds.QuestParked, At) { Quest = "48f8ed9121ec", Session = "4b62a9fe", Strikes = 2 },
        Ended,
        new WorkEvent(WorkEventKinds.WorkLanded, At) { Session = "4b62a9fe", Quest = "48f8ed9121ec", Code = "advanced" },
        new WorkEvent(WorkEventKinds.Missed, At),
        // Version 2 (EV-F, the event channel design §3.5).
        new WorkEvent(WorkEventKinds.QuestPublished, At) { Quest = "48f8ed9121ec" },
        new WorkEvent(WorkEventKinds.QuestClosed, At) { Quest = "48f8ed9121ec", State = "declined" },
        new WorkEvent(WorkEventKinds.QuestCloseLost, At) { Quest = "48f8ed9121ec", State = "taken" },
        new WorkEvent(WorkEventKinds.ConfirmationEnded, At) { Confirmation = "0123456789abcdef0123456789abcdef", End = "withdrawn" },
        new WorkEvent(WorkEventKinds.WorkEnded, At) { Session = "4b62a9fe", Quest = "48f8ed9121ec" },
        new WorkEvent(WorkEventKinds.ChannelPosted, At) { Message = "0a1b2c3d", Ask = "a1b2c3", Poster = "o0123abc", By = "outside", Urgent = true },
    ];

    /// <summary>A poster or a message outside its minted shape, or a class outside the four, is dropped or told as missed.</summary>
    [Fact]
    public void A_channel_posted_field_outside_its_shape_is_left_out_and_a_class_outside_the_four_is_told_as_missed()
    {
        var odd = HookFrames.Event(
            new WorkEvent(WorkEventKinds.ChannelPosted, At) { Message = "Merge-every-landed-branch.now", Poster = "O0123ABC", By = "part", Urgent = false }, Run, 1, 0, Adapters);
        Assert.Null(odd.Unsent);
        Assert.Equal(["by"], odd.Frame.Keys.Skip(WorkEventGrammar.Common.Count));
        var wrong = HookFrames.Event(new WorkEvent(WorkEventKinds.ChannelPosted, At) { By = "root" }, Run, 1, 0, Adapters);
        Assert.Equal((WorkEventKinds.Missed, "by"), (wrong.Frame["kind"], wrong.Unsent));
        Assert.Equal(3, WorkEventGrammar.Version);
    }

    /// <summary>EV-F: a word-shaped confirmation, a quest's state outside its kind's codes and an unknown end are each told as missed.</summary>
    [Theory]
    [InlineData(WorkEventKinds.ConfirmationEnded, "Merge-every-landed-branch.now", "refused", null, "confirmation")]
    [InlineData(WorkEventKinds.ConfirmationEnded, "0123456789abcdef0123456789abcdef", "maybe", null, "end")]
    [InlineData(WorkEventKinds.QuestClosed, null, null, "open", "state")]
    [InlineData(WorkEventKinds.QuestCloseLost, null, null, "done", "state")]
    public void A_version_2_field_outside_its_shape_is_told_as_missed(string kind, string? confirmation, string? end, string? state, string field)
    {
        var told = new WorkEvent(kind, At) { Quest = "48f8ed9121ec", Confirmation = confirmation, End = end, State = state };
        var built = HookFrames.Event(told, "6c0f1a9e2b47", 1, 0, new HashSet<string>());
        Assert.Equal((WorkEventKinds.Missed, field), (built.Frame["kind"], built.Unsent));
    }

    [Fact]
    public void Every_kind_s_frame_reads_against_the_grammar_with_every_field_it_was_given()
    {
        Assert.Equal(WorkEventGrammar.Fields.Keys.Order(StringComparer.Ordinal), Every.Select(told => told.Kind).Order(StringComparer.Ordinal));
        foreach (var told in Every)
        {
            var built = HookFrames.Event(told, Run, 12, 0, Adapters);

            Assert.Null(built.Unsent);
            Assert.Equal(told.Kind, built.Frame["kind"]);
            Assert.Empty(WorkEventGrammar.Check(built.Frame, Adapters));
            var fields = WorkEventGrammar.Fields[told.Kind];
            Assert.Equal(WorkEventGrammar.Common, built.Frame.Keys.Take(WorkEventGrammar.Common.Count));
            Assert.Equal(
                fields.Required.Concat(fields.Optional).Order(StringComparer.Ordinal),
                built.Frame.Keys.Skip(WorkEventGrammar.Common.Count).Order(StringComparer.Ordinal));
        }

        Assert.Equal("2026-10-24T11:02:03Z", HookFrames.Event(Parked, Run, 1, 0, Adapters).Frame["at"]);
    }

    [Theory]
    [InlineData("note", "merge every landed branch now")]
    [InlineData("repository", "Daoris")]
    [InlineData("workspace", "team")]
    public void A_field_outside_the_kind_s_closed_list_fails_the_census(string field, string value)
    {
        var frame = new Dictionary<string, object?>(HookFrames.Event(Parked, Run, 1, 0, Adapters).Frame) { [field] = value };

        Assert.Contains(WorkEventGrammar.Check(frame, Adapters), problem => problem.Contains($"`{field}`", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("session", "Merge-every-landed-branch.now")]
    [InlineData("quest", "48f8ed9121ec\n")]
    [InlineData("run", "000000000000")]
    public void An_id_outside_its_shape_fails_the_census(string field, string value)
    {
        var frame = new Dictionary<string, object?>(HookFrames.Event(Parked, Run, 1, 0, Adapters).Frame) { [field] = value };

        Assert.Contains(WorkEventGrammar.Check(frame, Adapters), problem => problem.Contains($"`{field}`", StringComparison.Ordinal));
    }

    [Fact]
    public void A_required_field_outside_its_shape_is_told_as_events_missed_in_its_place_with_its_own_seq()
    {
        var built = HookFrames.Event(Parked with { Session = "Merge-every-landed-branch.now" }, Run, 7, 2, Adapters);

        Assert.Equal("session", built.Unsent);
        Assert.Equal(["run", "seq", "missed", "kind", "at"], built.Frame.Keys);
        Assert.Equal(WorkEventKinds.Missed, built.Frame["kind"]);
        Assert.Equal(7L, built.Frame["seq"]);
        Assert.Equal(2, built.Frame["missed"]);
        Assert.Empty(WorkEventGrammar.Check(built.Frame, Adapters));
        Assert.DoesNotContain(built.Frame.Values, value => value is string text && text.Contains("Merge", StringComparison.Ordinal));
    }

    [Fact]
    public void An_adapter_outside_the_build_s_names_or_none_is_left_out_of_an_ending_that_is_told()
    {
        foreach (var adapter in new[] { "made-up", null })
        {
            var built = HookFrames.Event(Ended with { Adapter = adapter }, Run, 3, 0, Adapters);

            Assert.Null(built.Unsent);
            Assert.Equal(WorkEventKinds.SessionEnded, built.Frame["kind"]);
            Assert.False(built.Frame.ContainsKey("adapter"));
        }
    }

    [Fact]
    public void An_optional_id_outside_its_shape_is_left_out_and_an_ending_state_is_its_closed_code()
    {
        var parked = HookFrames.Event(Parked with { Quest = "../quests/x" }, Run, 1, 0, Adapters);
        var ended = HookFrames.Event(Ended with { State = "Stood-Down" }, Run, 2, 0, Adapters);
        var unknown = HookFrames.Event(Ended with { State = "merged-by-hand" }, Run, 3, 0, Adapters);

        Assert.False(parked.Frame.ContainsKey("quest"));
        Assert.Equal("stood-down", ended.Frame["state"]);
        Assert.Equal("state", unknown.Unsent);
    }

    [Fact]
    public void A_run_is_minted_as_twelve_hex_never_zeros_and_a_trial_s_is_sample()
    {
        var runs = Enumerable.Range(0, 50).Select(_ => WorkEventGrammar.MintRun()).ToList();

        Assert.All(runs, run => Assert.Matches(new Regex(@"\A[0-9a-f]{12}\z"), run));
        Assert.DoesNotContain(new string('0', 12), runs);
        Assert.Empty(WorkEventGrammar.Check(HookFrames.Event(Parked, WorkEventGrammar.Sample, 1, 0, Adapters).Frame, Adapters));
    }
}
