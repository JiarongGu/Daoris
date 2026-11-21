using Daoris.Knowledge;
using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The person's doors that are a workflow's accept steps take an appointed role's answer through the driver: a session's staged memory
/// kept, a memory end approved, a departure's yes. With the key and a <c>role</c>, the answer is kept as the role's and its agent's; a
/// keyless call is refused as before, the person's without a role and the driver's with one, and nothing is kept.
/// </summary>
public sealed class RoleAnswerDoorHostTests(KeyedHost host) : IClassFixture<KeyedHost>
{
    private const string Key = KeyedHost.Key;

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly object Role = new { role = "daoris-landing", agent = "codex-acp", decision = "d1" };

    private static void AssertRefused(Answer answer, string code, string act, string what)
    {
        Assert.True(answer.Status == 403, $"{what} answered {answer.Status}: {answer.Body}");
        Assert.Equal(code, answer.Json.GetProperty("code").GetString());
        Assert.Contains(act, answer.Error);
    }

    /// <summary>A chat of Keeper's, a change it staged, and the state it moved to after.</summary>
    private async Task<(string Session, MemoryOp Op)> StagedAsync(MemoryChange change, string state)
    {
        var opened = await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", door = "stub" }, person: Key);
        var session = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var each in new[] { "starting", "working" }) Assert.Equal(200, (await host.PostAsync($"/api/sessions/{session}/state", new { state = each }, person: Key)).Status);
        var op = await host.Composed.Sessions.Memory.StageAsync(
            "Keeper", change, new MemoryAuthor(session, "default", null, "stub", null, null, null, At));
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{session}/state", new { state }, person: Key)).Status);
        return (session, op);
    }

    /// <summary>🔴 A stopped session's staged change, kept by a role through the driver: keyless refused with or without the role, kept with the key as the role's.</summary>
    [Fact]
    public async Task A_roles_keep_of_staged_memory_is_the_drivers_to_post_and_names_the_role_and_its_agent()
    {
        var (session, op) = await StagedAsync(new MemoryChange(MemoryOpKind.Write, Text: "Keeper keeps a garden."), "stopped");

        AssertRefused(await host.PostAsync($"/api/sessions/{session}/memory", new { keep = true }),
            PersonDoors.PersonOnly, "an ended session's memory changes kept or dropped", "a keyless keep");
        AssertRefused(await host.PostAsync($"/api/sessions/{session}/memory", new { keep = true, role = Role, ops = new[] { op.Id } }),
            PersonDoors.DriverOnly, "a role's keep or drop of an ended session's memory changes", "a keyless keep naming a role");
        Assert.Equal(MemoryOpState.Staged, (await host.Composed.Sessions.Memory.FindAsync(op.Id))!.State);

        var kept = await host.PostAsync($"/api/sessions/{session}/memory", new { keep = true, role = Role, ops = new[] { op.Id } }, person: Key);

        Assert.Equal(200, kept.Status);
        Assert.Equal(1, kept.Json.GetProperty("moved").GetInt32());
        Assert.EndsWith("kept by role:daoris-landing · codex-acp.", kept.Json.GetProperty("message").GetString());
        Assert.NotEqual(MemoryOpState.Staged, (await host.Composed.Sessions.Memory.FindAsync(op.Id))!.State);
    }

    /// <summary>
    /// 🔴 A role keeps or drops only the rows it judged: its form names them, a row staged since stays staged for the next look, and a
    /// form naming no rows, a row no longer staged or another session's row is refused, nothing moved.
    /// </summary>
    [Fact]
    public async Task A_roles_keep_or_drop_moves_exactly_the_staged_rows_it_names()
    {
        var (session, judged) = await StagedAsync(new MemoryChange(MemoryOpKind.Write, Text: "Keeper keeps a garden."), "stopped");
        var since = await host.Composed.Sessions.Memory.StageAsync(
            "Keeper", new MemoryChange(MemoryOpKind.Write, Text: "Keeper keeps bees."), new MemoryAuthor(session, "default", null, "stub", null, null, null, At));
        var (_, other) = await StagedAsync(new MemoryChange(MemoryOpKind.Write, Text: "Keeper keeps a pond."), "stopped");
        string State(MemoryOp op) => host.Composed.Sessions.Memory.FindAsync(op.Id).GetAwaiter().GetResult()!.State;

        Assert.Equal(400, (await host.PostAsync($"/api/sessions/{session}/memory", new { keep = true, role = Role }, person: Key)).Status);
        Assert.Equal(409, (await host.PostAsync($"/api/sessions/{session}/memory", new { keep = true, role = Role, ops = new[] { judged.Id, other.Id } }, person: Key)).Status);
        Assert.Equal((MemoryOpState.Staged, MemoryOpState.Staged, MemoryOpState.Staged), (State(judged), State(since), State(other)));

        var kept = await host.PostAsync($"/api/sessions/{session}/memory", new { keep = true, role = Role, ops = new[] { judged.Id } }, person: Key);

        Assert.Equal(200, kept.Status);
        Assert.Equal(1, kept.Json.GetProperty("moved").GetInt32());
        Assert.Equal((MemoryOpState.Ready, MemoryOpState.Staged), (State(judged), State(since)));
        var again = await host.PostAsync($"/api/sessions/{session}/memory", new { keep = false, role = Role, ops = new[] { judged.Id, since.Id } }, person: Key);
        Assert.Equal(409, again.Status);
        Assert.Contains("no longer staged", again.Error);
        Assert.Equal(MemoryOpState.Staged, State(since));
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{session}/memory", new { keep = false, role = Role, ops = new[] { since.Id } }, person: Key)).Status);
        Assert.Equal((MemoryOpState.Dropped, MemoryOpState.Staged), (State(since), State(other)));
    }

    /// <summary>🔴 An awaiting end approved by a role: its row's approver is the role and its agent, never the person.</summary>
    [Fact]
    public async Task A_roles_approval_of_a_memory_end_names_the_role_as_its_approver()
    {
        var (_, op) = await StagedAsync(new MemoryChange(MemoryOpKind.End, Text: "Keeper moved its area.", Target: "1", Revision: 1), "completed");

        AssertRefused(await host.PostAsync($"/api/memory/{op.Id}/decide", new { approve = true, role = Role }),
            PersonDoors.DriverOnly, "a role's approval of a memory end", "a keyless approval naming a role");

        var decided = await host.PostAsync($"/api/memory/{op.Id}/decide", new { approve = true, role = Role }, person: Key);

        Assert.Equal(200, decided.Status);
        Assert.Equal("role:daoris-landing · codex-acp", decided.Json.GetProperty("op").GetProperty("approver").GetString());
    }

    /// <summary>
    /// 🔴 A role's approval of an end is refused where the turn that held the role is the proposer or of its lineage; a supervisor's own
    /// record, which serves the session it decides for, is no part of that lineage, so its approval is kept.
    /// </summary>
    [Fact]
    public async Task A_roles_approval_is_refused_where_its_holder_is_of_the_proposers_lineage()
    {
        var (session, op) = await StagedAsync(new MemoryChange(MemoryOpKind.End, Text: "Keeper moved its area.", Target: "1", Revision: 1), "completed");
        var own = new { role = "daoris-landing", agent = "codex-acp", decision = "d1", session };

        var refused = await host.PostAsync($"/api/memory/{op.Id}/decide", new { approve = true, role = own }, person: Key);

        Assert.NotEqual(200, refused.Status);
        Assert.Contains("held the role from that session or its lineage", refused.Body);
        Assert.Equal(MemoryOpState.Awaiting, (await host.Composed.Sessions.Memory.FindAsync(op.Id))!.State);

        var opened = await host.PostAsync("/api/supervise", new
        {
            decides = "a-1", role = "daoris-landing", repository = "Keeper", workspace = "default", serves = session, door = "stub", tree = "t",
        }, person: Key);
        Assert.True(opened.Status == 200, opened.Body);
        var supervising = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        var decided = await host.PostAsync($"/api/memory/{op.Id}/decide", new { approve = true, role = own with { session = supervising } }, person: Key);

        Assert.Equal(200, decided.Status);
        Assert.Equal("role:daoris-landing · codex-acp", decided.Json.GetProperty("op").GetProperty("approver").GetString());
    }

    /// <summary>🔴 A departure's yes naming a role or the risk policy is the driver's: keyless refused, and nothing is accepted.</summary>
    [Fact]
    public async Task A_roles_yes_to_a_departure_is_refused_without_the_key()
    {
        const string form = "a role's or the risk policy's yes to a departure";
        AssertRefused(await host.PostAsync("/api/quests/7/accept", new { role = Role }), PersonDoors.DriverOnly, form, "a keyless yes naming a role");
        AssertRefused(await host.PostAsync("/api/quests/7/accept", new { policy = new { act = "work.done", rule = "seed", revision = "r1" } }),
            PersonDoors.DriverOnly, form, "a keyless yes naming the risk policy");
        AssertRefused(await host.PostAsync("/api/quests/7/accept", new { }),
            PersonDoors.PersonOnly, "the yes to a departure", "a keyless yes");
        Assert.Equal(form, PersonDoors.Wanted(RoleAnswerDoors.Accept, System.Text.Json.JsonDocument.Parse("""{"role":{}}""").RootElement));
        Assert.Equal(form, PersonDoors.Wanted(RoleAnswerDoors.Accept, System.Text.Json.JsonDocument.Parse("""{"policy":{}}""").RootElement));
    }

    /// <summary>A quest of Keeper's closed done departing from its second requirement, held for a yes.</summary>
    private async Task<string> DepartedAsync()
    {
        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence = "Build the weekly report, using the common-report." }, person: Key);
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var published = await host.PostAsync($"/api/asks/{ask}/publish", new
        {
            to = "Keeper", title = "Build the weekly report", body = "Reached through the bridge.",
            requirements = new[] { new { quote = "the weekly report", check = "It opens." }, new { quote = "using the common-report", check = "It is one." } },
        }, person: Key);
        var quest = published.Json.GetProperty("quest").GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "take" }, person: Key)).Status);
        var closed = await host.PostAsync($"/api/quests/{quest}/respond", new
        {
            action = "done", reason = "Built.",
            answers = new object[] { new { requirement = 1, met = "It opens." }, new { requirement = 2, departed = "A type of its own.", quote = "using the common-report" } },
        }, person: Key);
        Assert.True(closed.Json.GetProperty("quest").GetProperty("held").GetBoolean(), closed.Body);
        return quest;
    }

    /// <summary>🔴 A keyed role's yes to a departure is kept on its move as the role's and its agent's; the person's own yes names no one.</summary>
    [Fact]
    public async Task A_keyed_roles_yes_to_a_departure_is_kept_on_its_move_by_the_role()
    {
        var quest = await DepartedAsync();

        var accepted = await host.PostAsync($"/api/quests/{quest}/accept", new { role = Role }, person: Key);

        Assert.Equal(200, accepted.Status);
        var moves = (await host.GetAsync($"/api/quests/{quest}/moves", person: Key)).Json.GetProperty("moves").EnumerateArray().ToList();
        Assert.Equal("role:daoris-landing · codex-acp", Assert.Single(moves, move => move.GetProperty("kind").GetString() == "accepted").GetProperty("by").GetString());

        var own = await DepartedAsync();
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{own}/accept", new { }, person: Key)).Status);
        var person = (await host.GetAsync($"/api/quests/{own}/moves", person: Key)).Json.GetProperty("moves").EnumerateArray()
            .Single(move => move.GetProperty("kind").GetString() == "accepted");
        Assert.False(person.TryGetProperty("by", out var by) && by.ValueKind == System.Text.Json.JsonValueKind.String, person.GetRawText());
    }

    /// <summary>🔴 A role form naming no role or no agent is refused at each of the three doors, so a blank role never stores the person's name as a role's.</summary>
    [Fact]
    public async Task A_role_form_naming_no_role_is_refused_and_nothing_moves()
    {
        var quest = await DepartedAsync();
        var blank = new { role = "", agent = "codex-acp", decision = "d1" };

        Assert.Equal(400, (await host.PostAsync($"/api/quests/{quest}/accept", new { role = blank }, person: Key)).Status);
        Assert.Equal(400, (await host.PostAsync($"/api/quests/{quest}/accept", new { role = new { role = "daoris-landing", agent = " ", decision = "d1" } }, person: Key)).Status);
        Assert.Equal(400, (await host.PostAsync("/api/memory/feedface/decide", new { approve = true, role = blank }, person: Key)).Status);
        Assert.Equal(400, (await host.PostAsync("/api/sessions/feedface/memory", new { keep = true, role = blank }, person: Key)).Status);
        Assert.True((await host.GetAsync("/api/quests", person: Key)).Json.EnumerateArray().Single(each => each.GetProperty("id").GetString() == quest).GetProperty("held").GetBoolean());
    }

    /// <summary>🔴 A keyless session may not ask the person to confirm a press carrying the role form: that form is the driver's alone.</summary>
    [Fact]
    public async Task A_confirmation_carrying_the_role_form_is_refused()
    {
        const string text = """{"role":{"role":"daoris-landing","agent":"codex-acp","decision":"d1"}}""";
        var asked = await host.PostAsync("/api/confirmations", new { method = "POST", path = "/api/quests/7/accept", body = text, session = "s-role" });

        Assert.Equal(400, asked.Status);
        Assert.Contains("the driver's alone", asked.Error);
        Assert.Equal(200, (await host.PostAsync("/api/confirmations", new { method = "POST", path = "/api/quests/7/accept", body = "{}", session = "s-role" })).Status);
        var body = System.Text.Json.JsonDocument.Parse(text).RootElement;
        Assert.True(PersonDoors.DriversForm(RoleAnswerDoors.Accept, body));
        Assert.False(PersonDoors.DriversForm(RoleAnswerDoors.Accept, System.Text.Json.JsonDocument.Parse("{}").RootElement));
        Assert.False(PersonDoors.DriversForm(PersonDoors.Respond, System.Text.Json.JsonDocument.Parse("""{"whileOpen":true}""").RootElement));
    }
}
