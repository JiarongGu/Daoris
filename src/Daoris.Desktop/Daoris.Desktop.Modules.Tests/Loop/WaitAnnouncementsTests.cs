using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A tool call a session holds for the person's answer is announced as a park is:
/// once, where the attention slice first sees its row, on <c>SESSION_ATTENTION</c> in the shape the shell's notifier and the page's
/// toast read, and only while the notify switch is on. Seen with the switch off, it is seen: turned back on, it is not said late.
/// </summary>
public sealed class WaitAnnouncementsTests : DriverModuleBridge
{
    private bool _notify = true;

    private JsonElement[] Said() => [.. Raised
        .Where(message => message.Type == "SESSION_ATTENTION")
        .Select(message => JsonSerializer.SerializeToElement(message.Payload))];

    private static JsonObject Session(string id, string repository) =>
        JsonNode.Parse($$"""{"id":"{{id}}","state":"working","repository":"{{repository}}","workspace":"default","updated":"2026-10-22T09:00:00Z"}""")!.AsObject();

    private static JsonObject Held(string session, string request, string? title = "npm test") =>
        JsonNode.Parse($$"""
            {"session":"{{session}}","request":"{{request}}","call":"c{{request}}",{{(title is null ? "" : $"\"title\":\"{title}\",")}}"kind":"execute",
             "since":"2026-10-22T09:30:00Z","until":"2026-10-22T09:45:00Z","choices":[{"id":"a","name":"Allow","kind":"allow_once"}]}
            """)!.AsObject();

    private (StateSlice Slice, Func<AttentionFacts> Facts, Action<AttentionFacts> Set) Slice(params JsonObject[] held)
    {
        var facts = new AttentionFacts { Sessions = [Session("s1a2b3c4", "engine"), Session("s2b3c4d5", "game")], Permissions = [.. held] };
        var slice = AttentionStateSlice.Of(() => facts, Bus, [], held: new WaitAnnouncements(Bus, () => _notify));
        return (slice, () => facts, now => facts = now);
    }

    [Fact]
    public async Task A_held_call_is_announced_once_where_its_row_is_first_seen()
    {
        var (slice, facts, set) = Slice(Held("s1a2b3c4", "900"));

        await slice.JudgeAsync();
        await slice.JudgeAsync();

        var told = Assert.Single(Said());
        Assert.Equal(
            (WaitAnnouncements.AttentionKind, "s1a2b3c4#900", "s1a2b3c4", "engine", "npm test"),
            (told.GetProperty("Kind").GetString(), told.GetProperty("Id").GetString(), told.GetProperty("Session").GetString(),
                told.GetProperty("Repository").GetString(), told.GetProperty("Detail").GetString()));
        // Its English headline for a reader that reads it, the English catalogue's (`SessionBalloon`).
        Assert.Equal("engine: a session holds a tool call for you", told.GetProperty("Headline").GetString());

        // A second held beside it is said alone; the first, still held, is not said again.
        set(facts() with { Permissions = [.. facts().Permissions, Held("s2b3c4d5", "7", title: null)] });
        await slice.JudgeAsync();

        Assert.Equal(["s1a2b3c4#900", "s2b3c4d5#7"], Said().Select(each => each.GetProperty("Id").GetString()));
        Assert.Equal(JsonValueKind.Null, Said()[^1].GetProperty("Detail").ValueKind);
    }

    /// <summary>The switch off says nothing, and what is held meanwhile is seen: turned back on, only what arrives after is said.</summary>
    [Fact]
    public async Task With_the_switch_off_nothing_is_said_and_nothing_is_said_late()
    {
        _notify = false;
        var (slice, facts, set) = Slice(Held("s1a2b3c4", "900"));

        await slice.JudgeAsync();
        Assert.Empty(Said());

        _notify = true;
        await slice.JudgeAsync();
        Assert.Empty(Said());

        set(facts() with { Permissions = [.. facts().Permissions, Held("s1a2b3c4", "901")] });
        await slice.JudgeAsync();
        Assert.Equal(["s1a2b3c4#901"], Said().Select(each => each.GetProperty("Id").GetString()));
    }

    /// <summary>A call answered or released is forgotten with its row, so what is kept is bounded by what is held.</summary>
    [Fact]
    public async Task A_call_no_longer_held_is_forgotten_and_one_held_again_is_said_again()
    {
        var (slice, facts, set) = Slice(Held("s1a2b3c4", "900"));
        await slice.JudgeAsync();

        set(facts() with { Permissions = [] });
        await slice.JudgeAsync();
        set(facts() with { Permissions = [Held("s1a2b3c4", "900")] });
        await slice.JudgeAsync();

        Assert.Equal(["s1a2b3c4#900", "s1a2b3c4#900"], Said().Select(each => each.GetProperty("Id").GetString()));
    }

    /// <summary>No other row is announced here: a park, a helper's ask, a terminal's press each has its own voice or none.</summary>
    /// <summary>A held call an allow supervisor takes is never said; its role gone, it is the owner's and said then, once.</summary>
    [Fact]
    public async Task A_held_call_a_role_takes_is_not_announced_until_it_is_the_owner_s()
    {
        var (slice, facts, set) = Slice(Held("s1a2b3c4", "900"));
        set(facts() with { Takers = new AttentionTakers((kind, _, _) => kind == SupervisorKinds.Allow, steward: false) });

        await slice.JudgeAsync();
        Assert.Empty(Said());

        set(facts() with { Takers = AttentionTakers.None });
        await slice.JudgeAsync();
        await slice.JudgeAsync();
        Assert.Equal("900", Assert.Single(Said()).GetProperty("Id").GetString()?.Split('#')[^1]);
    }

    [Fact]
    public async Task Only_a_held_call_is_announced_here()
    {
        var facts = new AttentionFacts { Sessions = [JsonNode.Parse("""{"id":"p1","state":"awaiting-person","repository":"engine"}""")!.AsObject()] };
        var slice = AttentionStateSlice.Of(() => facts, Bus, [], held: new WaitAnnouncements(Bus, () => true));

        await slice.JudgeAsync();

        Assert.Empty(Said());
    }

    [Fact]
    public async Task A_step_waiting_on_the_person_is_announced_once_on_the_session_its_run_placed()
    {
        var parks = true;
        var facts = new AttentionFacts
        {
            Sessions = [Session("s1a2b3c4", "engine")],
            Groups = [JsonNode.Parse("""
                {"session":"s1a2b3c4","group":"needs-you","reason":"waits","waits":[{"run":"q7","node":"sign","why":"task","role":"worker","since":null}]}
                """)!.AsObject()],
            Steps = [JsonNode.Parse("""
                {"run":"q7","node":"sign","repository":"engine","workspace":"default","why":"task","role":"worker","holder":"person","presses":["done"],
                 "since":"2026-11-16T09:51:00Z"}
                """)!.AsObject()],
        };
        var slice = AttentionStateSlice.Of(() => facts, Bus, [], held: new WaitAnnouncements(Bus, () => false, () => parks));

        await slice.JudgeAsync();
        await slice.JudgeAsync();

        var told = Assert.Single(Said());
        Assert.Equal(
            (WaitAnnouncements.StepKind, "q7/sign", "s1a2b3c4", "engine", "worker"),
            (told.GetProperty("Kind").GetString(), told.GetProperty("Id").GetString(), told.GetProperty("Session").GetString(),
                told.GetProperty("Repository").GetString(), told.GetProperty("Role").GetString()));
        Assert.Equal("engine: a step waits on `worker`, which you fill", told.GetProperty("Headline").GetString());
    }

    private static JsonObject StepRow(HeldStep step) => JsonSerializer.SerializeToNode(step, Shenora.Core.Ipc.IpcJson.Options)!.AsObject();

    private static HeldStep ExitStep(string kind = FinalExits.Spent) =>
        new("q7", "check", "engine", "default", PersonWaits.Left, "supervisor", [NodeWordActs.Again, NodeWordActs.Stop], "w1",
            DateTimeOffset.Parse("2026-11-16T09:51:00Z", System.Globalization.CultureInfo.InvariantCulture)) { Exit = new StepExit(FinalExits.Key("q7", "check", 3), kind) };

    /// <summary>The final exit interrupts once: its run, step, story's short form and answer, read by the balloon and the toast; its row stands.</summary>
    [Fact]
    public async Task A_step_at_the_owners_final_exit_is_the_persons_own_row_announced_once_with_its_run_step_and_answer()
    {
        var facts = new AttentionFacts { Sessions = [Session("s1a2b3c4", "engine")], Steps = [StepRow(ExitStep())] };
        var slice = AttentionStateSlice.Of(() => facts, Bus, [], held: new WaitAnnouncements(Bus, () => false, () => true));

        await slice.JudgeAsync();
        await slice.JudgeAsync();

        var told = Assert.Single(Said());
        Assert.Equal(
            (WaitAnnouncements.ExitKind, FinalExits.Key("q7", "check", 3), "engine", "q7", "check", FinalExits.Spent),
            (told.GetProperty("Kind").GetString(), told.GetProperty("Id").GetString(), told.GetProperty("Repository").GetString(),
                told.GetProperty("Run").GetString(), told.GetProperty("Step").GetString(), told.GetProperty("Exit").GetString()));
        Assert.Equal("engine: run #q7, step `check`, reached you as Daoris's final exit", told.GetProperty("Headline").GetString());
        var balloon = SessionBalloon.Words(new BalloonFacts(WaitAnnouncements.ExitKind) { Repository = "engine", Run = "q7", Step = "check", Exit = FinalExits.Spent });
        Assert.Equal((told.GetProperty("Headline").GetString(), BalloonIcon.Warning), (balloon.Title, balloon.Icon));
        Assert.Equal("Red past the rounds Daoris sends back, and nobody else holds it. Your one answer: mend it and check it again, or end this work.", balloon.Body);

        var row = Assert.Single(NeedsAPerson.Rows(facts), each => Json.Text(each, "kind") == "step");
        Assert.Equal(("step", "exit:q7/check#3", FinalExits.Spent, StepHolders.Person), (Json.Text(row, "kind"), Json.Text(row, "obligation"),
            Json.Text(row["step"]!["exit"]!.AsObject(), "kind"), Json.Text(row["step"]!.AsObject(), "holder")));
        Assert.Equal([NodeWordActs.Again, NodeWordActs.Stop], row["step"]!["presses"]!.AsArray().Select(press => press!.GetValue<string>()));
        Assert.Equal(AttentionOwnership.OwnersClass, AttentionOwnership.Of(row, new AttentionTakers((_, _, _) => true, steward: true)).Class);
    }

    /// <summary>A vacancy said once, then past its bound at the final exit: the exit is said under its own key, once, though its row's id is the vacancy's.</summary>
    [Fact]
    public async Task A_vacancy_that_reaches_the_final_exit_is_said_again_once_as_the_exit()
    {
        var vacant = ExitStep() with { Why = PersonWaits.Task, Role = "worker", Presses = [], Exit = null };
        var facts = new AttentionFacts { Sessions = [Session("s1a2b3c4", "engine")], Steps = [StepRow(vacant with { Holder = StepHolders.Agent, Vacant = true })] };
        var slice = AttentionStateSlice.Of(() => facts, Bus, [], held: new WaitAnnouncements(Bus, () => false, () => true));
        await slice.JudgeAsync();

        facts = facts with { Steps = [StepRow(ExitStep(FinalExits.Vacant) with { Why = PersonWaits.Task, Role = "worker" })] };
        await slice.JudgeAsync();
        await slice.JudgeAsync();

        Assert.Equal([WaitAnnouncements.VacantKind, WaitAnnouncements.ExitKind], Said().Select(each => each.GetProperty("Kind").GetString()));
        Assert.Equal(
            "No agent's session answered it, and nobody took it from Vacant. Your one answer: settle it with its own press, or end this work.",
            SessionBalloon.Words(new BalloonFacts(WaitAnnouncements.ExitKind) { Run = "q7", Step = "check", Exit = FinalExits.Vacant }).Body);
    }

    /// <summary>A step a role's agent holds, the default where no cast names another, is the agents' row: never announced as the owner's.</summary>
    [Fact]
    public async Task A_step_its_role_s_agent_holds_is_not_announced()
    {
        var facts = new AttentionFacts
        {
            Sessions = [Session("s1a2b3c4", "engine")],
            Steps = [JsonNode.Parse("""
                {"run":"q7","node":"check","repository":"engine","workspace":"default","why":"held","role":"operations","holder":"agent","presses":[],
                 "since":"2026-11-16T09:51:00Z"}
                """)!.AsObject()],
        };
        var slice = AttentionStateSlice.Of(() => facts, Bus, [], held: new WaitAnnouncements(Bus, () => true, () => true));

        await slice.JudgeAsync();

        Assert.Empty(Said());
    }

    /// <summary>The loop's switch, read at each announcement as a conversation's answer reads it: a choice that does not read errs toward saying it.</summary>
    [Fact]
    public void The_loop_reads_the_notify_switch_at_each_announcement()
    {
        var loop = Loop();
        Assert.True(loop.Notifying());

        DriverConfig.Empty.WithNotify(false).Save(DriverConfigPath);
        Assert.False(loop.Notifying());

        File.WriteAllText(DriverConfigPath, "{ not json");
        Assert.True(loop.Notifying());
    }
}
