using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The context-one design §3 B (D230 point 2): <c>RECONCILE_PLAN</c> and <c>RECONCILE_APPLY</c> over the bridge. The plan says what the doctrine
/// tool's status said of an adopted repository's checkout and whether that offers a reconcile; the apply is the set-up press's reconcile case,
/// its refusals an answer with nothing published. The status read over a real checkout is <c>ReconcileStatusProcessTests</c>'.
/// </summary>
public sealed class DriverModuleReconcileTests : DriverModuleBridge
{
    private readonly SetupStandIn _world = new();

    /// <summary>Adopted on the registry and on its line, its checkout's status a canon that differs: one file to add, one keep reopened.</summary>
    private void Adopted()
    {
        _world.Registry.Clear();
        _world.Registry.Add(new RepoView("atlas", true, "/fixture/atlas", "work"));
        _world.Facts = _world.Facts with { Adopted = true, Locked = true };
        _world.Doctrine = new(new DoctrineStatus { Synced = true, Update = DoctrineUpdates.Differs, Carried = "0.0.1", Locked = "0.0.1", Added = 1, Reopened = 1 }, null);
    }

    [Fact]
    public async Task The_plan_says_the_status_and_offers_a_reconcile_where_the_canon_moved()
    {
        Adopted();
        var module = await UpAsync();

        var plan = await AnswerAsync(module, "RECONCILE_PLAN", new { repository = "atlas" });

        Assert.Equal("atlas", plan.GetProperty("repository").GetString());
        Assert.True(plan.GetProperty("offered").GetBoolean());
        var status = plan.GetProperty("status");
        Assert.Equal((DoctrineUpdates.Differs, "0.0.1", "0.0.1"),
            (status.GetProperty("update").GetString(), status.GetProperty("carried").GetString(), status.GetProperty("locked").GetString()));
        Assert.Equal((0, 1, 0, 1), (status.GetProperty("changed").GetInt32(), status.GetProperty("added").GetInt32(), status.GetProperty("retired").GetInt32(), status.GetProperty("reopened").GetInt32()));
        Assert.Empty(plan.GetProperty("refusals").EnumerateArray());
        Assert.True(plan.GetProperty("pressable").GetBoolean());
        Assert.StartsWith("Reconcile this repository with the shared doctrine (", plan.GetProperty("title").GetString());
        Assert.Empty(_world.Published);
    }

    /// <summary>Nothing moved: the press is not offered, though it is not refused, since the standing answer's lines are its items too.</summary>
    [Fact]
    public async Task A_canon_that_has_not_moved_offers_nothing()
    {
        Adopted();
        _world.Doctrine = new(new DoctrineStatus { Synced = true }, null);
        var module = await UpAsync();

        var plan = await AnswerAsync(module, "RECONCILE_PLAN", new { repository = "atlas" });

        Assert.False(plan.GetProperty("offered").GetBoolean());
        Assert.True(Absent(plan.GetProperty("status"), "update"));
    }

    /// <summary>A field the bridge leaves out, as the window reads it: absent, or null in the answer before the bridge writes it.</summary>
    internal static bool Absent(System.Text.Json.JsonElement answer, string name) =>
        !answer.TryGetProperty(name, out var value) || value.ValueKind == System.Text.Json.JsonValueKind.Null;

    /// <summary>A repository not adopted is refused by its code and sentence, with no status asked, and offers nothing.</summary>
    [Fact]
    public async Task A_repository_not_adopted_is_refused_and_offers_nothing()
    {
        var module = await UpAsync();

        var plan = await AnswerAsync(module, "RECONCILE_PLAN", new { repository = "atlas" });

        Assert.False(plan.GetProperty("offered").GetBoolean());
        Assert.True(Absent(plan, "status"));
        Assert.False(plan.GetProperty("pressable").GetBoolean());
        var refusal = Assert.Single(plan.GetProperty("refusals").EnumerateArray(), each => each.GetProperty("code").GetString() == SetupRefusals.NotAdopted);
        Assert.Contains("has not taken up the shared doctrine", refusal.GetProperty("sentence").GetString());
    }

    /// <summary>The press: the rule into the repository's scope, then one ask to it, as the person's; it answers the quest it asked.</summary>
    [Fact]
    public async Task The_apply_asks_the_reconcile_of_the_repository_and_answers_its_quest()
    {
        Adopted();
        var module = await UpAsync();

        var answer = await AnswerAsync(module, "RECONCILE_APPLY", new { repository = "atlas" });

        Assert.True(answer.GetProperty("done").GetBoolean(), answer.ToString());
        Assert.Equal("q1a2b3c4", answer.GetProperty("quest").GetString());
        Assert.Equal("Asked as `#a1b2c3d4` in `work`.", answer.GetProperty("message").GetString());
        var (workspace, sentence, to) = Assert.Single(_world.Published);
        Assert.Equal(("work", "atlas"), (workspace, to));
        Assert.StartsWith("Reconcile this repository with the shared doctrine (", sentence);
        Assert.Contains("said its canon differs from the one this repository synced from", sentence);
        Assert.Contains(SetupPressRules.ReconcileRules[0], PermissionRules.Load(Home).Repositories["atlas"].Allow);
    }

    /// <summary>A plan that is refused is answered so, its code and the driver's sentence, and nothing is published or allowed.</summary>
    [Fact]
    public async Task A_refused_apply_is_an_answer_and_publishes_nothing()
    {
        Adopted();
        _world.Doctrine = new(new DoctrineStatus { Synced = true, Carried = "0.0.1", NewerLock = "9.9.9" }, null);
        var module = await UpAsync();

        var answer = await AnswerAsync(module, "RECONCILE_APPLY", new { repository = "atlas" });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(SetupRefusals.NewerLock, answer.GetProperty("refused").GetString());
        Assert.Contains("9.9.9", answer.GetProperty("message").GetString());
        Assert.Empty(_world.Published);
        Assert.False(PermissionRules.Load(Home).Repositories.ContainsKey("atlas"));
    }

    [Fact]
    public async Task A_plan_or_apply_naming_no_repository_is_refused()
    {
        var module = await UpAsync();

        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "RECONCILE_PLAN", new { }));
        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "RECONCILE_APPLY", new { }));
    }

    private async Task<DriverModule> UpAsync()
    {
        (StaffedRoles.Config("claude-code-acp")).WithDrivable("atlas", true).WithTrees("atlas", true).Save(DriverConfigPath);
        var loop = Loop();
        var module = new DriverModule(Bus, loop) { SetupWorlds = _ => _world, Patience = TimeSpan.FromMinutes(1) };
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SetupStandIn.EmptyHost())));
        return module;
    }
}
