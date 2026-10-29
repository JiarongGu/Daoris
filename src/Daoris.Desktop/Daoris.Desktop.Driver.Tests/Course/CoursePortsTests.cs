using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xiv, the Course cuts: the seams they add. The lower homes and each higher spelling held to them, the types Course now
/// declares or holds answering as the runner's, the turns' and Ask Daoris's own did, the installed help judges being Help's and a supplied
/// stand-in used where the loop's watch is built, and a missing operation required only where it is reached. What the cuts keep is
/// <c>CourseBoundaryTests</c>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class CoursePortsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "course-ports", Guid.NewGuid().ToString("N")[..8]);

    public CoursePortsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void The_lower_words_are_spelled_once_and_each_higher_spelling_holds_them()
    {
        Assert.Equal(SessionNotes.LostLine, ChatTurns.LostLine);
        Assert.Equal(SessionNotes.Withdrawn("w1").Text, ChatTurns.Withdrawn("w1").Text);
        Assert.Equal(ChatEnded.NotKept, ChatRunner.NotKept);
        Assert.Equal(ChatEnded.EndedByPersonNote, ChatRunner.EndedByPersonNote);
        Assert.Equal(ChatEnded.EndedNote(true, true).Note, ChatRunner.EndedNote(true, true).Note);
        Assert.Equal(KnowledgeRoomIdentity.Repository, HelpRoom.Repository);
        Assert.Equal(KnowledgeRoomPaths.GoesOnIn(_home, "atlas", null), HelpRoom.GoesOnIn(_home, "atlas", null));
        Assert.Equal(HelpModeNames.Recorded("FULL"), HelpModes.Recorded("FULL"));
        Assert.Equal(HelpModeNames.Recorded("other"), HelpModes.Recorded("other"));
        Assert.Equal(CommitRules.RepositorySource, OpinionSource.Repository);
        Assert.Equal(CommitRules.WorkspaceSource, OpinionSource.Workspace);
    }

    [Fact]
    public void The_pure_rules_Course_owns_answer_as_the_planner_and_the_channels_words_did()
    {
        var message = new ChannelItemView(ChannelItemView.Message, At) { Id = "m1", Text = "Which column?", Repository = "db", Session = "s2" };
        Assert.Equal(ChannelClaims.Claim(message, passedOn: true), ChannelText.Claim(message, passedOn: true));
        Assert.Equal(ChannelClaims.When(At), "2026-10-29 09:00 UTC");

        var last = new PriorSession("s1", "D:/trees/s1", "failed", "gone.") { Updated = At };
        var quest = new QuestView("q1", "Asker", "Game", "Ask q1", "Why.", "Taken") { Updated = At.AddHours(1) };
        var take = new QuestTake("held");
        Assert.Equal(QuestTakes.TakenAway(quest, last, take), Planner.TakenAway(quest, last, take));
        Assert.Equal(QuestTakes.TakenAt(quest, take), Planner.TakenAt(quest, take));
        Assert.Equal(QuestTakes.BroughtForward(last, take), Planner.BroughtForward(last, take));
        Assert.Equal(LimitResume.EndedTransient(last), Planner.EndedTransient(last));

        // the resumed line is judged by the quest and the record a start would resume, not by the planner's consideration
        var limited = last with { Limit = true };
        Assert.Same(limited, LimitResume.Line(quest with { Status = "Open" }, limited, handedFrom: false));
        Assert.Null(LimitResume.Line(quest with { Status = "Open" }, limited, handedFrom: true));
        Assert.Null(LimitResume.Line(quest with { Status = "Done" }, limited, handedFrom: false));
        Assert.Null(LimitResume.Line(quest, null, handedFrom: false));
    }

    [Fact]
    public void Ask_Daoriss_conversations_are_read_where_Knowledge_holds_them_and_the_chat_runners_types_answer_the_port()
    {
        Assert.True(typeof(IChatRunning).IsAssignableFrom(typeof(ChatRunner)));
        Assert.True(typeof(IChatTurns).IsAssignableFrom(typeof(ChatTurns)));
        Assert.True(typeof(IChatHelping).IsAssignableFrom(typeof(ChatHelp)));

        // a conversation's turns, as the port sees them: the same queue the class keeps
        IChatTurns turns = new ChatTurns(ready: () => Task.FromResult(true), take: (_, sent) => { sent(); return Task.CompletedTask; },
            interrupt: () => Task.CompletedTask, changed: _ => { });
        Assert.False(turns.Running);
        Assert.False(turns.Limited);
        Assert.False(turns.Stopped);
        Assert.False(turns.State.Taking);
        Assert.Empty(turns.State.Queued);
        Assert.True(turns.Hold());
        Assert.False(turns.Hold());
        turns.Release();
        Assert.True(turns.Hold());
    }

    [Fact]
    public async Task The_installed_help_judges_are_Helps_and_a_supplied_stand_in_is_what_a_watch_over_processes_answers_with()
    {
        Assert.IsType<CourseHelpOperations>(CourseHelp.Current);
        var judges = CourseHelp.Current;
        Assert.Equal(typeof(HelpHousekeeping), judges.Housekeeping(_home, () => null).Target!.GetType());
        Assert.Equal(typeof(HelpSettings), judges.Settings(_home, () => null).Target!.GetType());

        var actual = CourseHelp.Current;
        try
        {
            var stand = new StandInJudges();
            CourseHelp.Current = stand;
            var processes = new SessionProcesses();
            await using var watch = new SessionRequestWatch(_home, processes, () => null);
            Assert.Same(stand.Housekeep, watch.Housekeep);
            Assert.Same(stand.Set, watch.Set);
            Assert.Equal([_home, _home], stand.Homes);
        }
        finally { CourseHelp.Current = actual; }
    }

    [Fact]
    public async Task A_missing_operation_is_required_only_where_it_is_reached()
    {
        var actual = CourseHelp.Current;
        try
        {
            CourseHelp.Current = null!;
            Assert.Contains("Help installs them", Assert.Throws<InvalidOperationException>(() => CourseHelp.Current).Message);

            // The test seam's watch judges nothing of Ask Daoris's, so it never reaches the port.
            await using var seam = new SessionRequestWatch(_home, runsHere: _ => true, stop: _ => true, service: () => null, clock: () => At);
            Assert.Null(seam.Housekeep);
            Assert.Null(seam.Set);

            // Where reached, the watch over processes says so and the failure propagates.
            var processes = new SessionProcesses();
            Assert.Throws<InvalidOperationException>(() => new SessionRequestWatch(_home, processes, () => null));
        }
        finally { CourseHelp.Current = actual; }
    }

    private sealed class StandInJudges : ICourseHelp
    {
        public List<string> Homes { get; } = [];

        public Func<SessionRequest, CancellationToken, Task<HousekeepingAnswer>> Housekeep { get; } = (_, _) => throw new NotSupportedException();

        public Func<SessionRequest, CancellationToken, Task<SettingAnswer>> Set { get; } = (_, _) => throw new NotSupportedException();

        public Func<SessionRequest, CancellationToken, Task<HousekeepingAnswer>> Housekeeping(string home, Func<ServiceClient?> service)
        {
            Homes.Add(home);
            return Housekeep;
        }

        public Func<SessionRequest, CancellationToken, Task<SettingAnswer>> Settings(string home, Func<ServiceClient?> service)
        {
            Homes.Add(home);
            return Set;
        }
    }
}
