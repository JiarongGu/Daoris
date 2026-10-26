using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The ports the events declare where a lower reading names a higher type (FOUNDATION1 mod-vii): each is read through a stand-in that is
/// nothing the agents or the context built, so the events' judgements hold on the port's members alone, and the agents' and the context's own
/// types answer each as they did.
/// </summary>
public sealed class EventsPortsTests
{
    private sealed record Ending(bool Failed, string? Failure, IReadOnlyList<string> Lines) : IHarnessEnding;

    private sealed record Episode(int Tries, TimeSpan Waited, string? Words) : ITransientEpisode;

    private sealed record Reason(string Code, string Sentence) : IContinueReason
    {
        public IReadOnlyList<(string Name, string Value)> Values { get; init; } = [];
    }

    private sealed record Refusal(IContinueReason Why, string? Words) : IResumeRefused;

    [Fact]
    public void A_resume_refusals_reason_is_a_reading_contract_without_the_agents_policy()
    {
        var reason = typeof(IResumeRefused).GetProperty(nameof(IResumeRefused.Why))!.PropertyType;

        Assert.True(reason.IsInterface, "Events must read a reason through its own contract, rather than the Agents rule");
        Assert.Equal(["Code", "Sentence", "Values"], reason.GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("gone", "its conversation is open in another client of its agent", "agent words")]
    [InlineData("elsewhere", "a stand-in's own refusal", null)]
    [InlineData("elsewhere", "a stand-in's own refusal", "")]
    public void A_resume_note_keeps_the_supplied_sentence_and_words_independently_of_the_code(
        string code, string sentence, string? words)
    {
        var reason = new Reason(code, sentence);
        var refusal = new Refusal(reason, words);

        var noted = DoorNotes.ResumeRefused(refusal, "codex-acp");

        Assert.Equal($"— {sentence}{(string.IsNullOrEmpty(words) ? "" : ": " + words)}.", noted.Note);
        Assert.Equal(sentence, noted.Parts[0].Text);
        Assert.Equal(code, noted.Parts[0].Value("why"));
        Assert.Equal(code == "elsewhere" ? "codex-acp" : null, noted.Parts[0].Value("agent"));
        NoteAssert.Holds(noted);
    }

    [Fact]
    public void A_reason_preserves_value_order_and_only_fills_an_absent_elsewhere_agent()
    {
        var reason = new Reason("elsewhere", "held") { Values = [("to", "one"), ("from", "two")] };

        Assert.Equal(
            new (string, object?)[] { ("why", "elsewhere"), ("to", "one"), ("from", "two"), ("agent", "fallback") },
            NoteCodes.Reason(reason, "fallback"));
        Assert.Equal(
            new (string, object?)[] { ("why", "elsewhere"), ("agent", "supplied"), ("from", "two") },
            NoteCodes.Reason(reason with { Values = [("agent", "supplied"), ("from", "two")] }, "fallback"));
        Assert.Equal(
            new (string, object?)[] { ("why", "Elsewhere"), ("to", "one"), ("from", "two") },
            NoteCodes.Reason(reason with { Code = "Elsewhere" }, "fallback"));
        Assert.Equal(
            new (string, object?)[] { ("why", "elsewhere"), ("to", "one"), ("from", "two"), ("agent", null) },
            NoteCodes.Reason(reason));
    }

    private sealed class Events : ISessionEvents
    {
        public event Action<string, SessionEvent>? Evented;

        public List<(string Session, SessionEvent Event)> Kept { get; } = [];

        public void Keep(string sessionId, SessionEvent e, Action<string>? say) => Kept.Add((sessionId, e));

        public void Raise(string sessionId, SessionEvent e) => Evented?.Invoke(sessionId, e);
    }

    [Fact]
    public void A_refused_credential_is_read_from_a_failed_run_of_any_ending_that_answers_the_port()
    {
        var pattern = "credential was refused";

        Assert.True(Observation.Refused(new Ending(true, "the credential was refused", []), pattern));
        Assert.True(Observation.Refused(new Ending(true, null, ["x", "Your CREDENTIAL was refused"]), pattern));
        Assert.False(Observation.Refused(new Ending(false, "the credential was refused", []), pattern));
        Assert.False(Observation.Refused(new Ending(true, "overloaded", ["exit"]), pattern));
        Assert.True(Observation.Refused(new HarnessEnding(1, "the credential was refused", []), pattern));
    }

    [Fact]
    public void A_failed_turns_words_are_judged_from_the_failure_of_an_ending_that_answers_the_port()
    {
        var failed = Observation.Resumed(0, before: "completed", startedOn: "Done", questStatus: "Done", turnFailed: "the door failed");
        var before = new PriorSession("s1", "D:/fam/Game", "completed", "it went on with your words and ended; its quest stays as it closed.");

        Assert.Equal(WordsAfter.Wait, FailedTurnWords.Judge(failed, null, new Ending(true, "the door failed", []), before));
        Assert.Equal(WordsAfter.Wait, FailedTurnWords.Judge(failed, null, new HarnessEnding(1, "the door failed", []), before));
        Assert.Equal(WordsAfter.Taken, FailedTurnWords.Judge(Observation.WentOn(3, "completed"), null, new Ending(false, null, []), before));
    }

    [Fact]
    public void A_run_past_its_transient_bound_says_the_tries_the_minutes_and_the_providers_sentence_of_an_episode_that_answers_the_port()
    {
        var conclusion = Observation.Transient(new Episode(3, TimeSpan.FromMinutes(7.4), "overloaded_error"));

        Assert.Equal("failed", conclusion.State);
        Assert.Contains("after 3 tries, with 7 minutes of waits", conclusion.Note, StringComparison.Ordinal);
        Assert.EndsWith("overloaded_error", conclusion.Note, StringComparison.Ordinal);
        Assert.DoesNotContain("overloaded", Observation.Transient(new Episode(1, TimeSpan.Zero, null)).Note, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("offered", null)]
    [InlineData("gone", "")]
    [InlineData("refused", "no such conversation")]
    [InlineData("elsewhere", "thread held by another client")]
    public void A_refused_resume_is_noted_from_a_refusal_that_answers_the_port_as_the_doors_failure_is(string code, string? words)
    {
        var reason = ContinueWhy.Of(code);
        var refusal = new Refusal(new Reason(reason.Code, reason.Sentence) { Values = reason.Values }, words);
        var door = new AcpResumeRefused(reason, words);

        Assert.Same(reason, ((IResumeRefused)door).Why);
        foreach (var note in new Func<IResumeRefused, string?, Noted>[] { DoorNotes.ResumeRefused, DoorNotes.AnswerCarriedOn })
        {
            var viaPort = note(refusal, "codex-acp");
            var viaDoor = note(door, "codex-acp");

            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(viaDoor), System.Text.Json.JsonSerializer.Serialize(viaPort));
            NoteAssert.Holds(viaPort);
        }
    }

    [Fact]
    public void The_watches_of_calls_and_held_permissions_are_kept_by_the_record_whichever_record_it_is()
    {
        var events = new Events();

        var watched = RunningCalls.Watched(events);

        Assert.Same(events, watched);
        Assert.Same(RunningCalls.Of(events), RunningCalls.Of(watched));
        Assert.Same(HeldPermissions.Of(events), HeldPermissions.Of(events));
        Assert.Same(SessionWatch.Of(events), SessionWatch.Of(events));
        Assert.Null(RunningCalls.Of(events).Calling("s1"));
    }

    [Fact]
    public void The_records_own_type_answers_the_events_port()
    {
        Assert.True(typeof(ISessionEvents).IsAssignableFrom(typeof(SessionEvents)));
        Assert.True(typeof(IHarnessEnding).IsAssignableFrom(typeof(HarnessEnding)));
        Assert.True(typeof(ITransientEpisode).IsAssignableFrom(typeof(TransientEpisode)));
        Assert.True(typeof(IResumeRefused).IsAssignableFrom(typeof(AcpResumeRefused)));
        Assert.True(typeof(IContinueReason).IsAssignableFrom(typeof(ContinueReason)));
    }
}
