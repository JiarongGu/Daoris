using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// A session working in a system department plans through one typed tool, <c>system_plan</c>: each call is a keyed request in the
/// home's session requests for the session's own record, which the loop running it takes, hands to the system's adapter and answers
/// beside with the plan's number and its resources. The connector judges only what it can: that it speaks for a session, and that the
/// call names changes; which system, and what they come to there, are the driver's and the adapter's.
/// </summary>
/// <remarks>
/// A twin: the request's fields are the driver's <c>SessionRequests</c> (<c>text</c> holding the changes), held there by
/// <c>SystemPlanningTests</c> with the same names; the two change together.
/// </remarks>
public sealed class McpSystemPlanToolTests : McpToolsFixture
{
    private string Home => Path.Combine(_root, "rules-home");

    private string Requests => DaorisHome.Entry(Home, HomeEntry.Requests);

    private SystemPlanBox Box(double? take = null) => new(Home)
    {
        TakeWithin = take is { } t ? TimeSpan.FromSeconds(t) : Poll.Patience, AnswerWithin = Poll.Patience, Poll = TimeSpan.FromMilliseconds(10),
    };

    private async Task<(SystemPlanTools Tools, string? Session)> ConnectorAsync(bool named = true, SystemPlanBox? box = null)
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection));
        var session = named ? (await sessions.CreateAsync(null, "store", "claude-code", DateTimeOffset.UtcNow)).Id : null;
        return (new SystemPlanTools(new IntakeScope(null, session), ledger, box ?? Box()), session);
    }

    /// <summary>A loop standing in for the driver's: takes the first request, keeps it, and answers <paramref name="answer"/> beside it.</summary>
    private Task<JsonElement> LoopAsync(string answer) => Task.Run(async () =>
    {
        for (var tries = 0; tries < Poll.Patience.TotalMilliseconds / 10; tries++)
        {
            var found = Directory.Exists(Requests)
                ? Directory.GetFiles(Requests, "*.json").FirstOrDefault(path => !path.EndsWith(".answer.json", StringComparison.Ordinal))
                : null;
            if (found is not null)
            {
                string text;
                try
                {
                    text = File.ReadAllText(found);
                    File.Delete(found);
                }
                catch (Exception busy) when (busy is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(10);
                    continue;
                }

                File.WriteAllText(found[..^".json".Length] + ".answer.json", answer);
                using var document = JsonDocument.Parse(text);
                return document.RootElement.Clone();
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("no request was written");
    });

    /// <summary>A plan is one keyed request for the session's own record, its changes in order as the request's text; the tool says the loop's words.</summary>
    [Fact]
    public async Task A_plan_is_a_request_for_the_session_s_own_record_carrying_its_changes_in_order()
    {
        var (tools, session) = await ConnectorAsync();
        var loop = LoopAsync("""{ "operation": "plan", "done": true, "code": null, "said": "Plan 1 is kept: create `release`." }""");

        var said = await tools.PlanAsync([new SystemPlanChange("release", "2"), new SystemPlanChange("old", null, Remove: true)]);

        Assert.Equal("Plan 1 is kept: create `release`.", said);
        var request = await loop;
        Assert.Equal(["session", "move", "at", "by", "key", "text"], request.EnumerateObject().Select(field => field.Name));
        Assert.Equal((session, "plan", "connector"),
            (request.GetProperty("session").GetString(), request.GetProperty("move").GetString(), request.GetProperty("by").GetString()));
        Assert.Equal("""[{"resource":"release","value":"2"},{"resource":"old","remove":true}]""", request.GetProperty("text").GetString());
        Assert.Empty(Directory.GetFiles(Requests));
    }

    [Theory]
    [InlineData(null, "names no change")]
    [InlineData("", "names no resource")]
    [InlineData("valueless", "neither a value nor a removal")]
    public async Task A_call_naming_no_change_the_driver_can_plan_is_refused_here(string? resource, string said)
    {
        var (tools, _) = await ConnectorAsync();
        SystemPlanChange[] changes = resource is null ? [] : [new SystemPlanChange(resource, null)];

        var answer = await tools.PlanAsync(changes);

        Assert.Contains(said, answer, StringComparison.Ordinal);
        Assert.EndsWith("Nothing was planned.", answer, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Requests) && Directory.GetFiles(Requests).Length > 0);
    }

    /// <summary>Two changes to one resource, however spaced, would be read against the one version the plan reads, so the second would be stale before it is applied.</summary>
    [Fact]
    public async Task A_call_changing_one_resource_twice_is_refused_here_naming_it()
    {
        var (tools, _) = await ConnectorAsync();

        var answer = await tools.PlanAsync([new SystemPlanChange("release", "2"), new SystemPlanChange(" release", "3")]);

        Assert.Contains("`release` is named twice", answer, StringComparison.Ordinal);
        Assert.EndsWith("Nothing was planned.", answer, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Requests) && Directory.GetFiles(Requests).Length > 0);
    }

    [Fact]
    public async Task A_connector_speaking_for_no_session_plans_nothing()
    {
        var (tools, _) = await ConnectorAsync(named: false);

        Assert.EndsWith("Nothing was planned.", await tools.PlanAsync([new SystemPlanChange("release", "2")]), StringComparison.Ordinal);
    }

    /// <summary>A session's work is read in a system where its record's department is registered as one, and in none elsewhere.</summary>
    [Fact]
    public async Task A_session_s_work_is_in_a_system_where_its_department_is_registered_as_one()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection));
        await _service.RegisterAsync(new Registration("store", false, null, [], [], [], 0, Kind: PlaceKinds.System, System: "stand-in-system/stand-in"), DateTimeOffset.UtcNow);
        await _service.RegisterAsync(new Registration("engine", false, null, [], [], [], 0, Root: _root), DateTimeOffset.UtcNow);
        var store = await sessions.CreateAsync(null, "store", "claude-code", DateTimeOffset.UtcNow);
        var engine = await sessions.CreateAsync(null, "engine", "claude-code", DateTimeOffset.UtcNow);

        Assert.True((await new SessionWorks(ledger, new IntakeScope(null, store.Id)).ReadAsync(default)).InSystem);
        Assert.False((await new SessionWorks(ledger, new IntakeScope(null, engine.Id)).ReadAsync(default)).InSystem);
    }

    [Fact]
    public async Task A_plan_no_loop_takes_is_said_untaken()
    {
        var (tools, _) = await ConnectorAsync(box: Box(take: 0.05));

        Assert.Contains("No Daoris on this machine took it", await tools.PlanAsync([new SystemPlanChange("release", "2")]), StringComparison.Ordinal);
    }
}
