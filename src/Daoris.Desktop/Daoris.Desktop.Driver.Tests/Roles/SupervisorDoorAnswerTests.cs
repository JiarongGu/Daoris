using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A role's answers at the person's accept-step doors: a session's staged memory kept, a memory end decided, a departure accepted, each
/// held for the role's window with <i>Overrule</i>, its file naming the role and its agent, then posted to its door as the role's. Without
/// the role's grant the same answer is refused and nothing is posted.
/// </summary>
public sealed class SupervisorDoorAnswerTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-door", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-16T10:00:00Z");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    /// <summary>A role <c>daoris-landing</c> holding exactly <paramref name="granted"/>, as the admission reads its catalogue.</summary>
    private sealed class Holds(params string[] granted) : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) =>
            act is null || granted.Contains(SupervisorKinds.Qualified(kind, act))
                ? new("g-1", null) { Window = TimeSpan.FromMinutes(10), Role = "daoris-landing" }
                : DecisionAdmitted.Refused(SupervisorWhy.NotGranted);
    }

    private static readonly SupervisorHolder Codex = SupervisorHolders.OfAgent(new("daoris-landing", 3, "codex-acp", "gpt-5", "high", "gpt-5", "read-only", "t1"));

    private static SupervisorDoorAnswer Staged(string choice = SupervisorDoorActs.Keep) => new(
        new SupervisorSubject(SupervisorKinds.Memory, "s1", SupervisorDoorActs.Staged), "reports", "g-1", "keep", choice,
        [new SupervisorReason("The staged rows restate the area.", "the session's memory")], Codex);

    private static SupervisorDoorAnswer Departure() => new(
        new SupervisorSubject(SupervisorKinds.Departure, "s1", "q7"), "reports", "g-1", SupervisorDoorActs.Accept, null,
        [new SupervisorReason("The departure is the brief's own fallback.", "the quest's answers")], Codex);

    private static SupervisorDoorPost Posting(List<SupervisorDoorDue> posted, string? why = null) => (due, _) =>
    {
        posted.Add(due);
        return Task.FromResult(why);
    };

    [Fact]
    public async Task A_role_holding_memory_keep_keeps_a_sessions_staged_memory_after_its_window_and_the_record_names_the_role_and_its_agent()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("memory.keep"));
        var kept = await decisions.DoorAsync(Staged(), null, At);

        Assert.True(kept.Kept, kept.Why);
        Assert.Equal(At.AddMinutes(10), kept.GoesOnAt);
        var file = File.ReadAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + QuestionSupervisorDecisions.DecisionsSuffix), kept.Decision + ".json"));
        Assert.Contains("\"role\": \"daoris-landing\"", file);
        Assert.Contains("\"agent\": \"codex-acp \\u00B7 gpt-5 \\u00B7 high\"", file);
        Assert.False(decisions.RefersDoor(new SupervisorSubject(SupervisorKinds.Memory, "s1", SupervisorDoorActs.Staged)));

        var posted = new List<SupervisorDoorDue>();
        Assert.Empty(await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(5)));
        Assert.Empty(posted);
        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        var due = Assert.Single(posted);
        Assert.Equal(("daoris-landing", "codex-acp · gpt-5 · high", true), (due.Role, due.Agent, due.Yes));
    }

    /// <summary>🔴 The page reads a session's held answer, its subject, act, choice, role, agent and moment, to offer <i>Overrule</i> on it; an overrule says so.</summary>
    [Fact]
    public async Task A_sessions_held_door_answer_is_read_for_the_page_with_its_role_its_agent_and_its_moment()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("memory.keep", "departure.accept"));
        var kept = await decisions.DoorAsync(Staged(SupervisorDoorActs.Drop), null, At);
        await decisions.DoorAsync(Departure(), null, At.AddMinutes(1));

        var read = decisions.DoorsOf("s1");

        Assert.Equal(2, read.Count);
        var staged = read[0];
        Assert.Equal((kept.Decision, SupervisorKinds.Memory, SupervisorDoorActs.Staged, "keep", SupervisorDoorActs.Drop), (staged.Id, staged.Kind, staged.Subject, staged.Do, staged.Choice));
        Assert.Equal(("daoris-landing", "codex-acp · gpt-5 · high", SupervisorState.Held, At, At.AddMinutes(10)), (staged.Role, staged.Agent, staged.State, staged.KeptAt, staged.GoesOnAt));
        Assert.Equal((SupervisorKinds.Departure, "q7", SupervisorDoorActs.Accept), (read[1].Kind, read[1].Subject, read[1].Do));
        Assert.Empty(decisions.DoorsOf("s2"));

        await decisions.OverruleAsync("s1", kept.Decision!, (_, _, _) => Task.FromResult(true), At.AddMinutes(2));
        Assert.Equal(SupervisorState.Overruled, decisions.DoorsOf("s1")[0].State);
    }

    [Fact]
    public async Task The_same_keep_without_the_role_is_refused_and_nothing_is_posted()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("landing.accept"));
        var refused = await decisions.DoorAsync(Staged(), null, At);

        Assert.False(refused.Kept);
        Assert.Equal(SupervisorWhy.NotGranted, refused.Why);
        var posted = new List<SupervisorDoorDue>();
        Assert.Empty(await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Empty(posted);
        Assert.Equal(SupervisorWhy.NotOffered, (await new QuestionSupervisorDecisions(_home, new Holds("memory.keep")).DoorAsync(Staged("approve"), null, At)).Why);
    }

    [Fact]
    public async Task The_owners_overrule_inside_the_window_undoes_a_keep_and_the_subject_is_theirs()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("memory.keep"));
        var kept = await decisions.DoorAsync(Staged(SupervisorDoorActs.Drop), null, At);

        Assert.Equal(SupervisorState.Overruled, await decisions.OverruleAsync("s1", kept.Decision!, (_, _, _) => Task.FromResult(true), At.AddMinutes(1)));
        var posted = new List<SupervisorDoorDue>();
        Assert.Empty(await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Empty(posted);
        Assert.Equal(SupervisorWhy.Overruled, (await decisions.DoorAsync(Staged(), null, At.AddMinutes(12))).Why);
        Assert.False(decisions.RefersDoor(new SupervisorSubject(SupervisorKinds.Memory, "s1", SupervisorDoorActs.Staged)));
    }

    [Fact]
    public async Task A_role_holding_departure_accept_accepts_after_its_window_and_the_owners_overrule_or_a_missing_grant_stops_it()
    {
        var posted = new List<SupervisorDoorDue>();
        Assert.Equal(SupervisorWhy.NotGranted, (await new QuestionSupervisorDecisions(_home, new Holds("memory.keep")).DoorAsync(Departure(), null, At)).Why);

        var decisions = new QuestionSupervisorDecisions(_home, new Holds("departure.accept"));
        var kept = await decisions.DoorAsync(Departure(), null, At);
        Assert.True(kept.Kept, kept.Why);
        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Equal(("q7", SupervisorDoorActs.Accept, "daoris-landing"), (posted.Single().Subject.Id, posted.Single().Do, posted.Single().Role));

        var overruled = new QuestionSupervisorDecisions(_home, new Holds("departure.accept"));
        var held = await overruled.DoorAsync(Departure() with { Subject = new SupervisorSubject(SupervisorKinds.Departure, "s2", "q8") }, null, At);
        Assert.Equal(SupervisorState.Overruled, await overruled.OverruleAsync("s2", held.Decision!, (_, _, _) => Task.FromResult(true), At.AddMinutes(2)));
        Assert.Empty(await overruled.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Single(posted);
    }

    [Fact]
    public async Task A_post_its_door_refuses_goes_stale_and_the_kinds_are_role_set_vocabulary()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("memory.keep"));
        await decisions.DoorAsync(Staged(), null, At);

        Assert.Equal([SupervisorState.Stale], await decisions.ReleaseDoorsAsync(Posting([], "running"), At.AddMinutes(11)));
        Assert.Equal(["memory.keep", "memory.decide", "departure.accept"],
            SupervisorKinds.Acts.Where(act => act.StartsWith("memory.", StringComparison.Ordinal) || act.StartsWith("departure.", StringComparison.Ordinal)));
    }
}
