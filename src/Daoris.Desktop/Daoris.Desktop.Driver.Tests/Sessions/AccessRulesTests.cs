using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTONOMY1a (D184 points 1, 5): a repository's session access setup, <c>access</c>, <c>workspaceAccess</c> and <c>machineAccess</c> in
/// <c>driver.json</c>, read, resolved pause by pause (repository, then workspace, then machine, then today's behaviour), written and
/// edited. Nothing set anywhere is today's behaviour, so nothing loosens by itself.
/// </summary>
public sealed class AccessRulesTests
{
    private const string Today = "permissions=auto@default routing=person@default landing=person@default goAheads=named@default questions=claude-code@default";

    /// <summary>Each pause at each level, the defaults where nothing is set, and full access at each level.</summary>
    public static TheoryData<string, string?, string?, string?, string> ResolveRows() => new()
    {
        { "nothing set anywhere: today's behaviour, from the defaults", null, null, null, Today },
        {
            "the repository's full access: every pause off, from the repository", "\"full\"", null, null,
            "permissions=unrestricted@repository routing=intake@repository landing=rule@repository goAheads=never@repository questions=claude-code@repository"
        },
        {
            "the workspace's full access, the repository naming nothing", null, "\"full\"", null,
            "permissions=unrestricted@workspace routing=intake@workspace landing=rule@workspace goAheads=never@workspace questions=claude-code@workspace"
        },
        {
            "this machine's full access, nothing nearer", null, null, "\"full\"",
            "permissions=unrestricted@machine routing=intake@machine landing=rule@machine goAheads=never@machine questions=claude-code@machine"
        },
        {
            "permission prompts at the repository: that pause alone", """{"permissions":"asking"}""", null, null,
            "permissions=asking@repository routing=person@default landing=person@default goAheads=named@default questions=claude-code@default"
        },
        {
            "intake routing at the workspace", null, """{"routing":"intake"}""", null,
            "permissions=auto@default routing=intake@workspace landing=person@default goAheads=named@default questions=claude-code@default"
        },
        {
            "landing at this machine", null, null, """{"landing":"rule"}""",
            "permissions=auto@default routing=person@default landing=rule@machine goAheads=named@default questions=claude-code@default"
        },
        {
            "go-aheads at the repository", """{"goAheads":"never"}""", null, null,
            "permissions=auto@default routing=person@default landing=person@default goAheads=never@repository questions=claude-code@default"
        },
        {
            "questions at this machine: Claude Code's, the one value", null, null, """{"questions":"claude-code"}""",
            "permissions=auto@default routing=person@default landing=person@default goAheads=named@default questions=claude-code@machine"
        },
        {
            "one pause at every level: the repository's wins, the rest from this machine's full access",
            """{"permissions":"asking"}""", """{"permissions":"unrestricted"}""", "\"full\"",
            "permissions=asking@repository routing=intake@machine landing=rule@machine goAheads=never@machine questions=claude-code@machine"
        },
        {
            "the workspace's full access under one pause the repository names", """{"permissions":"asking"}""", "\"full\"", null,
            "permissions=asking@repository routing=intake@workspace landing=rule@workspace goAheads=never@workspace questions=claude-code@workspace"
        },
        {
            "the repository's full access over the workspace's narrower pauses", "\"full\"", """{"permissions":"asking","landing":"person"}""", null,
            "permissions=unrestricted@repository routing=intake@repository landing=rule@repository goAheads=never@repository questions=claude-code@repository"
        },
        {
            "a pause and a value spelled in another case read as the file's", """{" Permissions ":" Unrestricted "}""", null, null,
            "permissions=unrestricted@repository routing=person@default landing=person@default goAheads=named@default questions=claude-code@default"
        },
        {
            "a pause this build does not know is passed over, the rest read", """{"plan":"always","landing":"rule"}""", null, null,
            "permissions=auto@default routing=person@default landing=rule@repository goAheads=named@default questions=claude-code@default"
        },
        {
            "a value this build does not know reads as today's at that level, never as the next level's looser one",
            """{"permissions":"sometimes"}""", "\"full\"", null,
            "permissions=auto@repository routing=intake@workspace landing=rule@workspace goAheads=never@workspace questions=claude-code@workspace"
        },
        { "a setting of another shape is not read", "\"partial\"", "true", "[\"full\"]", Today },
    };

    [Theory]
    [MemberData(nameof(ResolveRows))]
    public void Each_pause_resolves_from_the_repository_then_the_workspace_then_this_machine_then_today(
        string name, string? repository, string? workspace, string? machine, string expected)
    {
        var config = DriverConfig.Parse(File(repository, workspace, machine));

        Assert.True(expected == Spelled(AccessRules.Resolve(config, "engine", "shop")), $"{name}: {Spelled(AccessRules.Resolve(config, "engine", "shop"))}");
    }

    [Fact]
    public void The_resolver_is_a_pure_function_of_the_three_levels()
    {
        var asking = new AccessSetting { Pauses = new Dictionary<string, string> { [AccessRules.Permissions] = AccessRules.Asking } };

        Assert.Equal(Today, Spelled(AccessRules.ResolveLevels(null, null, null)));
        Assert.Equal(
            "permissions=asking@repository routing=intake@machine landing=rule@machine goAheads=never@machine questions=claude-code@machine",
            Spelled(AccessRules.ResolveLevels(asking, null, AccessSetting.FullAccess)));
        Assert.True(AccessRules.ResolveLevels(null, AccessSetting.FullAccess, null).Full);
        Assert.False(AccessRules.ResolveLevels(asking, AccessSetting.FullAccess, null).Full);
        Assert.True(AccessRules.ResolveLevels(null, null, null).Today);
        Assert.False(AccessRules.ResolveLevels(asking, null, null).Today);
    }

    [Fact]
    public void A_repository_is_named_in_any_case_and_one_in_no_workspace_takes_the_default_workspace_s()
    {
        var config = DriverConfig.Parse("""{"access":{"Engine":{"landing":"rule"}},"workspaceAccess":{"default":{"routing":"intake"}}}""");

        var resolved = AccessRules.Resolve(config, " engine ", null);

        Assert.Equal(("rule", "repository"), (resolved.Value(AccessRules.Landing), resolved.Source(AccessRules.Landing)));
        Assert.Equal(("intake", "workspace"), (resolved.Value(AccessRules.Routing), resolved.Source(AccessRules.Routing)));
    }

    /// <summary>D184 point 5: nothing loosens by itself. A file from before says nothing, and nothing is written for it.</summary>
    [Fact]
    public void Nothing_set_writes_nothing_and_every_level_survives_every_other_edit()
    {
        Assert.DoesNotContain("ccess", DriverConfig.Empty.ToJson());
        Assert.Empty(DriverConfig.Parse("{}").Access);
        Assert.Null(DriverConfig.Parse("{}").MachineAccess);

        var file = File("""{"permissions":"asking","goAheads":"never"}""", "\"full\"", """{"landing":"rule"}""");
        var edited = DriverConfig.Parse(DriverConfig.Parse(file).WithNotify(false).WithDrivable("tools", true).ToJson());

        Assert.Equal(
            "permissions=asking@repository routing=intake@workspace landing=rule@workspace goAheads=never@repository questions=claude-code@workspace",
            Spelled(AccessRules.Resolve(edited, "engine", "shop")));
        Assert.Equal(
            "permissions=auto@default routing=person@default landing=rule@machine goAheads=named@default questions=claude-code@default",
            Spelled(AccessRules.Resolve(edited, "tools", "games")));
    }

    [Fact]
    public void A_setting_is_written_as_full_or_its_pauses_in_their_order()
    {
        var config = DriverConfig.Empty
            .WithAccess(new AccessEdit { Repository = "engine", Set = [new("goAheads", "never"), new("permissions", "Asking")] })
            .WithAccess(new AccessEdit { Workspace = "shop", Full = true })
            .WithAccess(new AccessEdit { Machine = true, Set = [new("landing", "rule")] });

        var written = JsonNode.Parse(config.ToJson())!.AsObject();

        Assert.Equal("""{"engine":{"permissions":"asking","goAheads":"never"}}""", written["access"]!.ToJsonString());
        Assert.Equal("""{"shop":"full"}""", written["workspaceAccess"]!.ToJsonString());
        Assert.Equal("""{"landing":"rule"}""", written["machineAccess"]!.ToJsonString());
    }

    /// <summary>One change at a time, at exactly one level, every pause and value known: anything else is refused, nothing changed.</summary>
    public static TheoryData<string, string, string> RefusalRows() => new()
    {
        { "no level named", """{"access":"full"}""", "an access setup is set for a repository, a workspace or this machine — name exactly one." },
        { "two levels named", """{"repository":"engine","machine":true,"access":"full"}""", "an access setup is set for a repository, a workspace or this machine — name exactly one." },
        { "a pause the setup does not have", """{"repository":"engine","access":{"plan":"always"}}""", "`plan` is not a pause a session stops for — one of `permissions`, `routing`, `landing`, `goAheads` or `questions`." },
        { "a value its pause does not have", """{"repository":"engine","access":{"permissions":"sometimes"}}""", "`permissions` is `asking`, `auto` or `unrestricted`, not `sometimes`." },
        { "a value that is no word", """{"workspace":"shop","access":{"landing":true}}""", "`landing` is `person` or `rule`, not `true`." },
        { "a word that is not full", """{"machine":true,"access":"everything"}""", "an access setup is `full`, or the pauses it names, each with its value — or none, to clear it." },
        { "an empty list of pauses", """{"repository":"engine","access":{}}""", "one change at a time: full access, the pauses it names with their values, or clear." },
    };

    [Theory]
    [MemberData(nameof(RefusalRows))]
    public void An_edit_the_setup_cannot_hold_is_refused_in_its_words(string name, string payload, string refusal)
    {
        using var document = JsonDocument.Parse(payload);

        var thrown = Assert.Throws<DriverException>(() => DriverConfig.Empty.WithAccess(AccessRules.EditOf(document.RootElement)));

        Assert.True(refusal == thrown.Message, $"{name}: {thrown.Message}");
    }

    [Fact]
    public void A_pause_set_over_full_access_keeps_the_rest_as_full_access_had_them_and_one_taken_away_comes_from_above()
    {
        var full = DriverConfig.Empty.WithAccess(new AccessEdit { Repository = "engine", Full = true });

        var asking = full.WithAccess(Edit("""{"repository":"engine","access":{"permissions":"asking"}}"""));
        Assert.Equal(
            "permissions=asking@repository routing=intake@repository landing=rule@repository goAheads=never@repository questions=claude-code@repository",
            Spelled(AccessRules.Resolve(asking, "engine", null)));

        var unset = asking.WithAccess(Edit("""{"repository":"ENGINE","access":{"routing":null,"landing":""}}"""));
        Assert.Equal(
            "permissions=asking@repository routing=person@default landing=person@default goAheads=never@repository questions=claude-code@repository",
            Spelled(AccessRules.Resolve(unset, "engine", null)));
        Assert.Equal(["engine"], unset.Access.Keys);

        var cleared = unset.WithAccess(Edit("""{"repository":"engine","access":null}"""));
        Assert.Empty(cleared.Access);
        Assert.Equal(Today, Spelled(AccessRules.Resolve(cleared, "engine", null)));

        // The last pause taken away clears the level: a setting naming none is never kept.
        var last = DriverConfig.Empty.WithAccess(Edit("""{"machine":true,"access":{"landing":"rule"}}""")).WithAccess(Edit("""{"machine":true,"access":{"landing":null}}"""));
        Assert.Null(last.MachineAccess);
    }

    [Fact]
    public void A_start_says_its_setup_in_one_line()
    {
        Assert.Equal(
            "Access setup: today's defaults — permissions auto, routing person, landing person, goAheads named, questions claude-code.",
            AccessRules.Said(AccessRules.ResolveLevels(null, null, null)));
        Assert.Equal(
            "Access setup: full access, set for its workspace — permissions unrestricted, routing intake, landing rule, goAheads never, questions claude-code.",
            AccessRules.Said(AccessRules.ResolveLevels(null, AccessSetting.FullAccess, null)));
        Assert.Equal(
            "Access setup: permissions asking (its repository), routing intake (this machine), landing rule (this machine), goAheads never (this machine), questions claude-code (this machine).",
            AccessRules.Said(AccessRules.ResolveLevels(
                new AccessSetting { Pauses = new Dictionary<string, string> { ["permissions"] = "asking" } }, null, AccessSetting.FullAccess)));
        Assert.DoesNotContain('\n', AccessRules.Said(AccessRules.ResolveLevels(null, null, AccessSetting.FullAccess)));
    }

    private static AccessEdit Edit(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return AccessRules.EditOf(document.RootElement);
    }

    private static string File(string? repository, string? workspace, string? machine)
    {
        var parts = new List<string>();
        if (repository is not null) parts.Add($"\"access\":{{\"engine\":{repository}}}");
        if (workspace is not null) parts.Add($"\"workspaceAccess\":{{\"shop\":{workspace}}}");
        if (machine is not null) parts.Add($"\"machineAccess\":{machine}");
        return "{" + string.Join(",", parts) + "}";
    }

    private static string Spelled(ResolvedAccess access) =>
        string.Join(' ', access.Pauses.Select(pause => $"{pause.Pause}={pause.Value}@{pause.Source}"));
}
