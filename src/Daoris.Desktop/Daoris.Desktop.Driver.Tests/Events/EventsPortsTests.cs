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

    private sealed record Refusal(ContinueReason Why, string? Words) : IResumeRefused;

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

    [Fact]
    public void A_refused_resume_is_noted_from_a_refusal_that_answers_the_port_as_the_doors_failure_is()
    {
        var reason = ContinueWhy.Of(ContinueWhy.Refused);

        var viaPort = DoorNotes.ResumeRefused(new Refusal(reason, "no such conversation"), ranOn: "codex-acp");
        var viaDoor = DoorNotes.ResumeRefused(new AcpResumeRefused(reason, "no such conversation"), ranOn: "codex-acp");

        Assert.Equal(viaDoor.Note, viaPort.Note);
        Assert.Equal(viaDoor.Parts!.Select(part => part.Code), viaPort.Parts!.Select(part => part.Code));
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
    }
}
