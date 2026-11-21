using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary><c>daoris session list --json</c> carries each vacant step beside its sessions, and none where none is given.</summary>
public sealed class SessionsJsonVacantTests
{
    [Fact]
    public void The_json_answer_lists_each_vacant_step_with_its_run_node_role_wait_and_since()
    {
        var since = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        PersonWait[] vacant = [new("q1", "review", PersonWaits.Accept, "engine", since) { Role = "acceptor", Vacant = true }, new("q2", "release", PersonWaits.Actor, "game", null) { Role = "approver", Vacant = true }];

        using var answer = JsonDocument.Parse(SessionsCommand.Json([], null, vacant));
        var listed = answer.RootElement.GetProperty("vacant").EnumerateArray().ToList();
        using var none = JsonDocument.Parse(SessionsCommand.Json([]));

        Assert.Equal(
            [("q1", "review", "engine", "acceptor", PersonWaits.Accept), ("q2", "release", "game", "approver", PersonWaits.Actor)],
            listed.Select(step => (step.GetProperty("run").GetString()!, step.GetProperty("node").GetString()!, step.GetProperty("repository").GetString()!,
                step.GetProperty("role").GetString()!, step.GetProperty("why").GetString()!)));
        Assert.Equal(since, listed[0].GetProperty("since").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, listed[1].GetProperty("since").ValueKind);
        Assert.False(none.RootElement.TryGetProperty("vacant", out _));
    }
}
