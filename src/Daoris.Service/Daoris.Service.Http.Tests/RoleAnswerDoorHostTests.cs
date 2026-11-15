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
            "Keeper", change, new MemoryAuthor(session, "default", null, "stub", null, null, null, DateTimeOffset.UtcNow));
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
        AssertRefused(await host.PostAsync($"/api/sessions/{session}/memory", new { keep = true, role = Role }),
            PersonDoors.DriverOnly, "a role's keep or drop of an ended session's memory changes", "a keyless keep naming a role");
        Assert.Equal(MemoryOpState.Staged, (await host.Composed.Sessions.Memory.FindAsync(op.Id))!.State);

        var kept = await host.PostAsync($"/api/sessions/{session}/memory", new { keep = true, role = Role }, person: Key);

        Assert.Equal(200, kept.Status);
        Assert.Equal(1, kept.Json.GetProperty("moved").GetInt32());
        Assert.EndsWith("kept by role:daoris-landing · codex-acp.", kept.Json.GetProperty("message").GetString());
        Assert.NotEqual(MemoryOpState.Staged, (await host.Composed.Sessions.Memory.FindAsync(op.Id))!.State);
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

    /// <summary>🔴 A departure's yes naming a role is the driver's: keyless refused, and nothing is accepted.</summary>
    [Fact]
    public async Task A_roles_yes_to_a_departure_is_refused_without_the_key()
    {
        AssertRefused(await host.PostAsync("/api/quests/7/accept", new { role = Role }),
            PersonDoors.DriverOnly, "a role's yes to a departure", "a keyless yes naming a role");
        AssertRefused(await host.PostAsync("/api/quests/7/accept", new { }),
            PersonDoors.PersonOnly, "the yes to a departure", "a keyless yes");
        Assert.Equal("a role's yes to a departure", PersonDoors.Wanted(RoleAnswerDoors.Accept, System.Text.Json.JsonDocument.Parse("""{"role":{}}""").RootElement));
    }
}
