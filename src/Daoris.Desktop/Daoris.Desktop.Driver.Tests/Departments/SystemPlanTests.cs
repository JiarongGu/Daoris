using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A system place's plan, review and apply over its adapter's channel: the plan kept as the record's numbered change set, shown on the
/// review as rows before and after, applied, and a stale apply answered with each resource's present version for the work to plan again.
/// The channel here answers as a scripted adapter; <see cref="SystemPlanProcessTests"/> runs the stand-in system's own process.
/// </summary>
public sealed class SystemPlanTests : IDisposable
{
    private static readonly SystemPlace Place = new("acme.console", "store");
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-system-plan-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class Scripted(params string[] answers) : IHookChannel
    {
        private readonly Queue<string> _answers = new(answers);

        public List<(string Point, string Params)> Asked { get; } = [];

        public IReadOnlyList<string> Points => HookPoints.System;

        public bool Alive => true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => throw new NotSupportedException();

        public Task EndedAsync(object payload, CancellationToken ct) => throw new NotSupportedException();

        public Task<JsonElement> SystemAsync(string point, object payload, CancellationToken ct)
        {
            Asked.Add((point, JsonSerializer.Serialize(payload)));
            return Task.FromResult(JsonDocument.Parse(_answers.Dequeue()).RootElement.Clone());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private const string Planned = """
        { "operations": [
          { "resource": "release", "op": "update", "before": "1", "after": "2", "precondition": { "kind": "etag", "version": "e1" } },
          { "resource": "owner", "op": "create", "before": null, "after": "team", "precondition": { "kind": "etag", "version": null } } ] }
        """;

    [Fact]
    public async Task A_plan_is_kept_numbered_and_shown_on_the_review_as_each_resource_before_and_after()
    {
        var channel = new Scripted(Planned);
        var adapter = new SystemAdapter(channel, Place, new SystemPlans(_home), () => Now);

        var plan = await adapter.PlanAsync("rec-1", [new SystemIntent("release", "2"), new SystemIntent("owner", "team")], CancellationToken.None);

        Assert.Equal((1, "acme.console/store"), (plan.Plan, plan.Place));
        Assert.Equal(HookPoints.PlanRead, channel.Asked[0].Point);
        Assert.Equal("""{"system":"store","changes":[{"resource":"release","value":"2"},{"resource":"owner","value":"team"}]}""", channel.Asked[0].Params);
        Assert.Equal(["release update e1", "owner create "], plan.Operations.Select(each => $"{each.Resource} {each.Op} {each.Precondition.Version}"));

        var review = Assert.IsType<TreeDiff>(SystemChanges.Of(_home, "rec-1"));
        Assert.Equal(["release modified 1/1", "owner added 1/0"], review.Files.Select(file => $"{file.Path} {file.Status} {file.Added}/{file.Removed}"));
        Assert.Contains("-1", review.Files[0].Patch);
        Assert.Contains("+2", review.Files[0].Patch);
        Assert.Null(SystemChanges.Of(_home, "rec-none"));
    }

    [Fact]
    public async Task A_stale_apply_names_each_present_version_and_the_next_plan_applies()
    {
        var channel = new Scripted(
            Planned,
            """{ "state": "stale", "applied": [], "refused": "release", "present": [{ "resource": "release", "exists": true, "value": "by hand", "version": "e2" }] }""",
            Planned.Replace("\"before\": \"1\"", "\"before\": \"by hand\"").Replace("\"e1\"", "\"e2\""),
            """{ "state": "applied", "applied": ["release", "owner"] }""");
        var plans = new SystemPlans(_home);
        var adapter = new SystemAdapter(channel, Place, plans, () => Now);
        var intents = new[] { new SystemIntent("release", "2"), new SystemIntent("owner", "team") };

        var first = await adapter.PlanAsync("rec-1", intents, CancellationToken.None);
        var stale = await adapter.ApplyAsync("rec-1", first, CancellationToken.None);

        Assert.Equal((SystemApplyStates.Stale, "release"), (stale.State, stale.Refused));
        Assert.Equal(stale, plans.AppliedOf("rec-1", 1), SystemAppliedComparer.Instance);
        var words = SystemStaleWords.Of("apply", first.Plan, stale);
        Assert.Contains("`apply` refused plan 1 as stale at `release`", words);
        Assert.Contains("- `release` is now `by hand` at version `e2`", words);

        var second = await adapter.PlanAsync("rec-1", intents, CancellationToken.None);
        Assert.Equal(2, second.Plan);
        Assert.Equal("e2", second.Operations[0].Precondition.Version);
        var applied = await adapter.ApplyAsync("rec-1", second, CancellationToken.None);
        Assert.Equal((SystemApplyStates.Applied, "release owner"), (applied.State, string.Join(" ", applied.Applied)));
        Assert.Contains("\"plan\":2", channel.Asked[3].Params);
    }

    [Theory]
    [InlineData("""{ "operations": [{ "resource": "a", "op": "patch", "precondition": { "kind": "etag" } }] }""")]
    [InlineData("""{ "operations": [{ "resource": "a", "op": "update" }] }""")]
    [InlineData("""{ "plan": [] }""")]
    public async Task An_answer_that_is_not_a_change_set_is_refused_and_nothing_is_kept(string answer)
    {
        var adapter = new SystemAdapter(new Scripted(answer), Place, new SystemPlans(_home), () => Now);
        var refused = await Assert.ThrowsAsync<DriverException>(() => adapter.PlanAsync("rec-1", [new SystemIntent("a", "1")], CancellationToken.None));
        Assert.Contains("which is not a change set", refused.Message);
        Assert.Null(new SystemPlans(_home).Latest("rec-1"));
    }

    [Theory]
    [InlineData("acme.console/store", "acme.console", "store")]
    [InlineData("acme.console/", null, null)]
    [InlineData("/store", null, null)]
    [InlineData("a/b/c", null, null)]
    [InlineData("acme.console/Store", null, null)]
    public void A_system_place_is_its_adapter_and_its_system(string word, string? plugin, string? system) =>
        Assert.Equal(plugin is null ? null : new SystemPlace(plugin, system!), SystemPlace.Of(word));

    private sealed class SystemAppliedComparer : IEqualityComparer<SystemApplied?>
    {
        public static readonly SystemAppliedComparer Instance = new();

        public bool Equals(SystemApplied? x, SystemApplied? y) =>
            x is not null && y is not null && x.State == y.State && x.Refused == y.Refused && x.Applied.SequenceEqual(y.Applied) && x.Present.SequenceEqual(y.Present);

        public int GetHashCode(SystemApplied? obj) => obj?.State.GetHashCode() ?? 0;
    }
}
