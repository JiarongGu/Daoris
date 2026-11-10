using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's <c>system_plan</c> reaches the driver through the session requests every loop watches: a keyed request for the session's
/// own record, move <c>plan</c>, its changes as the request's text, taken once by the loop running the session and answered beside it.
/// </summary>
/// <remarks>
/// A twin: the request is written by the service's connector (<c>SystemPlanBox</c>), whose <c>McpSystemPlanToolTests</c> hold the same
/// field names in the order that side writes them, and read the answer's <c>said</c>.
/// </remarks>
public sealed class SystemPlanningTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-system-planning-" + Guid.NewGuid().ToString("N")[..8]);

    public SystemPlanningTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private SessionRequests Requests => new(_home);

    private void Written()
    {
        Directory.CreateDirectory(Requests.Folder);
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Requests, "s1.0123456789ab.json"), """
            {
              "session": "s1",
              "move": "plan",
              "at": "2026-11-11T09:00:00.0000000+00:00",
              "by": "connector",
              "key": "0123456789ab",
              "text": "[{\"resource\":\"release\",\"value\":\"2\"},{\"resource\":\"old\",\"remove\":true}]"
            }
            """);
    }

    [Fact]
    public async Task The_loop_running_the_session_takes_the_connector_s_plan_once_and_answers_beside_it()
    {
        Written();
        var asked = new List<IReadOnlyList<SystemIntent>?>();
        await using var watch = new SessionRequestWatch(_home, runsHere: id => id == "s1", stop: _ => true, service: () => null, clock: () => Now)
        {
            Plan = (request, _) =>
            {
                asked.Add(SystemPlanning.Intents(request.Text));
                return Task.FromResult(new SystemPlanAnswer(true, null, "Plan 1 is kept."));
            },
        };

        Assert.Equal("0123456789ab", Assert.Single(await watch.HonourAsync()).Key);

        Assert.Equal([new SystemIntent("release", "2"), new SystemIntent("old", null, Remove: true)], Assert.Single(asked));
        using var answer = JsonDocument.Parse(File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Requests, "s1.0123456789ab.answer.json")));
        Assert.Equal(["operation", "done", "code", "said"], answer.RootElement.EnumerateObject().Select(field => field.Name));
        Assert.Equal(("plan", true, "Plan 1 is kept."),
            (answer.RootElement.GetProperty("operation").GetString(), answer.RootElement.GetProperty("done").GetBoolean(), answer.RootElement.GetProperty("said").GetString()));
        Assert.Empty(await watch.HonourAsync());
    }

    [Fact]
    public async Task A_loop_that_does_not_run_the_session_leaves_it()
    {
        Written();
        await using var watch = new SessionRequestWatch(_home, runsHere: _ => false, stop: _ => true, service: () => null, clock: () => Now)
        {
            Plan = (_, _) => throw new InvalidOperationException("not this loop's"),
        };

        Assert.Empty(await watch.HonourAsync());
        Assert.True(File.Exists(DaorisHome.Entry(_home, HomeEntry.Requests, "s1.0123456789ab.json")));
    }

    [Theory]
    [InlineData("""[{"resource":"a","value":"1"},{"resource":"b","remove":true}]""", 2)]
    [InlineData("""[]""", -1)]
    [InlineData("""[{"resource":"a"}]""", -1)]
    [InlineData("""[{"resource":"a","value":"1","remove":true}]""", -1)]
    [InlineData("""{"resource":"a","value":"1"}""", -1)]
    [InlineData("not json", -1)]
    public void The_changes_read_only_as_a_list_of_values_or_removals(string text, int count) =>
        Assert.Equal(count, SystemPlanning.Intents(text)?.Count ?? -1);

    [Fact]
    public async Task A_session_in_no_system_department_is_refused_and_no_adapter_starts()
    {
        var planning = new SystemPlanning(_home, () => null, (_, _) => throw new InvalidOperationException("no adapter is started"));

        var answer = await planning.PlanAsync("s1", new RepoView("engine", true, null), [new SystemIntent("release", "2")], CancellationToken.None);

        Assert.Equal((false, SystemPlanAnswer.NotSystem), (answer.Done, answer.Code));
        Assert.EndsWith("Nothing was planned.", answer.Said, StringComparison.Ordinal);
        Assert.Null(new SystemPlans(_home).Latest("s1"));
    }

    [Fact]
    public void Its_words_name_the_plan_s_number_and_each_resource_or_that_it_changes_nothing()
    {
        var plan = new SystemChangeSet("stand-in-system/stand-in", 3,
            [new SystemOperation("release", "update", "1", "2", new SystemPrecondition("etag", "e1"))], Now);

        Assert.StartsWith("Plan 3 is kept for `store` (system `stand-in-system/stand-in`): update `release`.", SystemPlanning.Said("store", plan), StringComparison.Ordinal);
        Assert.Contains("changes nothing", SystemPlanning.Said("store", plan with { Operations = [] }), StringComparison.Ordinal);
    }
}
