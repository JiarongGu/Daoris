using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>daoris role set|remove|default|policy|rules</c> as their press's card shows it: judged by the role door's judges against the
/// catalogue, staffing and rules the machine holds now, refused with no card, and a supervisor's record and a place's rules carried before
/// and after for the page to word.
/// </summary>
public sealed class RoleChangeViewTests : DriverModuleBridge
{
    private readonly RoleStaffing _staffing;

    public RoleChangeViewTests()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code" }""");
        _staffing = RoleStaffing.Wire(Home, () => StoreSettled.Yes);
        _staffing.ImportFile();
        SupervisorRuleBook.SeedFresh(Home, () => StoreSettled.Yes);
        // The seeded Design role takes design machine-wide; the cases below add one that does, so it is removed first.
        RoleCatalog.Write(DesktopStore.LocationIn(Home), RoleChange.Remove("design"), () => StoreSettled.Yes);
    }

    private Task<PressViewed> View(object payload)
    {
        var grounds = new StubGrounds { Config = DriverConfig.Load(DriverConfigPath) };
        grounds.Facts = grounds.Facts with { Home = Home };
        return new RoleChangeView().ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);
    }

    private static object Designer(long revision, object[]? choices = null) => new
    {
        change = "set", id = "designer", revision,
        role = new { name = new { en = "Designer", zh = "设计师" }, takes = new[] { "design" }, scope = new { kind = "machine" } },
        choices = choices ?? [new { agent = "claude-code", model = "opus", effort = "high" }],
    };

    /// <summary>The designer set as the route sets it, its revision moving to 1.</summary>
    private void Designed()
    {
        var press = RoleDoor.ReadPress(JsonSerializer.SerializeToElement(Designer(0)));
        RoleDoor.Apply(_staffing, press.Change, press.Choices, press.Read);
    }

    [Fact]
    public async Task A_working_role_set_is_shown_with_its_work_its_scope_its_choices_and_the_command()
    {
        var viewed = await View(Designer(0));

        Assert.Null(viewed.Refusal);
        var view = viewed.View!;
        Assert.Equal("role-set", view.Kind);
        Assert.Equal(("designer", "Designer · 设计师", "design", "machine"), (view.Facts["role"], view.Facts["name"], view.Facts["takes"], view.Facts["scope"]));
        Assert.Equal("role-choices", Assert.Single(view.Also!).Kind);
        Assert.Equal("claude-code · opus · high", view.Also![0].Facts["choices"]);
        Assert.Equal("daoris role set designer --name Designer --name-in zh=设计师 --takes design --scope machine \"claude-code opus high\"", view.Command);
    }

    [Fact]
    public async Task A_change_the_role_door_refuses_or_one_made_since_the_revision_read_is_refused_with_no_card()
    {
        Designed();

        var moved = await View(Designer(0));
        var unknown = await View(new { change = "remove", id = "nobody" });
        var unread = await View(new { change = "rename", id = "designer" });

        Assert.Null(moved.View);
        Assert.Contains("changed since it was read", moved.Refusal);
        Assert.Null(unknown.View);
        Assert.NotNull(unknown.Refusal);
        Assert.Contains("`set`, `remove`, `default` or `default-all`", unread.Refusal);
    }

    [Fact]
    public async Task A_remove_a_default_and_every_default_are_shown_by_what_they_do()
    {
        Designed();

        var removed = await View(new { change = "remove", id = "designer", revision = 1 });
        var all = await View(new { change = "default-all" });

        Assert.Equal(("role-remove", "designer", "daoris role remove designer"), (removed.View!.Kind, removed.View.Facts["role"], removed.View.Command));
        Assert.Equal(("role-default-all", "daoris role default --all"), (all.View!.Kind, all.View.Command));
    }

    [Fact]
    public async Task A_place_s_supervisor_rules_are_shown_before_and_after_and_a_moved_revision_is_refused()
    {
        var press = new { id = "supervisor", place = "repository:daoris", rulesRevision = 0, rules = new[] { new { count = "answers-alone", at = 3, @do = "hold" } } };

        var viewed = await View(press);

        var view = viewed.View!;
        Assert.Equal(("role-rules", "repository:daoris"), (view.Kind, view.Facts["place"]));
        Assert.Equal("daoris role rules --place repository:daoris --clear all --set answers-alone=3:hold", view.Command);
        Assert.Equal("repository:daoris", view.Rules!.Place);
        Assert.Empty(view.Rules.Before);
        Assert.Equal(("answers-alone", 3, "hold"), (view.Rules.After.Single().Count, view.Rules.After.Single().At, view.Rules.After.Single().Do));

        new SupervisorRuleStore(Home, () => StoreSettled.Yes).Write("repository:daoris", 0, [], "test", new SupervisorRuleWorld(["build"]));
        Assert.Contains("(revision)", (await View(press)).Refusal);
    }

    [Fact]
    public async Task A_supervisor_set_is_shown_with_its_record_before_and_after_once_the_home_is_converted()
    {
        var press = new
        {
            change = "set", id = "gatekeeper", revision = 0,
            role = new
            {
                name = new { en = "Gatekeeper" }, scope = new { kind = "workspace", id = "w1" }, filled = "appointed", kind = "supervisor",
                decides = new[] { "allow" }, authority = new[] { "allow.once" }, holder = "terminal", window = 10,
            },
        };

        var unconverted = await View(press);
        HomeMigration.Run(Home, [new SupervisorStep(() => null)]);
        var viewed = await View(press);

        Assert.Null(unconverted.View);
        Assert.NotNull(unconverted.Refusal);
        var view = viewed.View!;
        Assert.Equal(("role-supervisor-set", "gatekeeper", "Gatekeeper"), (view.Kind, view.Facts["role"], view.Facts["name"]));
        Assert.Equal(("gatekeeper", "workspace", "w1"), (view.Supervisor!.Id, view.Supervisor.Scope.Kind, view.Supervisor.Scope.Id));
        Assert.Equal(["allow"], view.Supervisor.Hold.Decides);
        Assert.Equal(["allow.once"], view.Supervisor.Hold.Acts);
        Assert.Equal(("terminal", 10), (view.Supervisor.Hold.Holder, view.Supervisor.Hold.Window));
        Assert.Null(view.Supervisor.Before);
        Assert.Equal("daoris role set gatekeeper --name Gatekeeper --scope workspace:w1 --decides allow --acts allow.once --holder terminal --window 10", view.Command);
    }

    [Fact]
    public void The_role_route_has_its_view()
    {
        Assert.IsType<RoleChangeView>(PressViews.Find(DriverModule.Name, "SET_ROLE"));
    }
}
