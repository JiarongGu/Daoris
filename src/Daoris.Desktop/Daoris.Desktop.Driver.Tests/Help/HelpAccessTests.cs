using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// HELPMODE1a1 (D183 points 1, 3): what a start of Ask Daoris gets on its harness under the mode the person chose. Restricted
/// keeps each harness as it ran; full runs each in its own mode that asks nothing, a flag on the native door and a required mode
/// on the protocol door; a harness with no such mode is refused, naming it. No process, so the fast half.
/// </summary>
public sealed class HelpAccessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-access-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpAccessTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static ISessionAdapter Adapter(string name) => AdapterSet.Built().Resolve(name);

    /// <summary>Restricted is Ask Daoris as it ran: the agent's asking mode asked for on the protocol door, no flag on the native one.</summary>
    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    [InlineData("codex-acp")]
    [InlineData("dsh")]
    public void Restricted_keeps_each_harness_as_it_ran(string name)
    {
        var access = HelpAccess.For(HelpModes.Restricted, Adapter(name));

        Assert.False(access.Full);
        Assert.Null(access.Refusal);
        Assert.Equal((HelpRoom.Posture, (string?)null), (access.Posture, access.PermissionMode));
        Assert.Contains("restricted", access.Said);
        Assert.Contains($"`{name}`", access.Said);
    }

    /// <summary>
    /// Full runs each harness in its mode that asks nothing: Claude Code's flag on the native door, a mode the protocol door must set
    /// or fail on the others. What its conversation is told names the mode and the harness's own word for it.
    /// </summary>
    [Theory]
    [InlineData("claude-code", null, "bypassPermissions")]
    [InlineData("claude-code-acp", "!bypassPermissions", null)]
    [InlineData("codex-acp", "!agent-full-access", null)]
    public void Full_runs_each_harness_in_its_mode_that_asks_nothing(string name, string? posture, string? permissionMode)
    {
        var access = HelpAccess.For(HelpModes.Full, Adapter(name));

        Assert.True(access.Full);
        Assert.Null(access.Refusal);
        Assert.Equal((posture, permissionMode), (access.Posture, access.PermissionMode));
        Assert.Contains("full access", access.Said);
        Assert.Contains($"`{Adapter(name).FullPosture}`", access.Said);
    }

    /// <summary>A harness with no mode that asks nothing mapped is refused in full access, naming it, and the person's two ways on.</summary>
    [Theory]
    [InlineData("dsh")]
    [InlineData("acp-stub")]
    public void Full_on_a_harness_with_no_such_mode_is_refused_naming_it(string name)
    {
        var refusal = HelpAccess.For(HelpModes.Full, Adapter(name)).Refusal;

        Assert.NotNull(refusal);
        Assert.Contains($"`{name}`", refusal);
        Assert.Contains("help mode restricted", refusal);
        Assert.Contains("another agent", refusal);
    }

    /// <summary>
    /// Its place under full access: no allow-list and nothing asked, its harness's mode that asks nothing, every plugin server and no
    /// tree guard, since its tree is the room and its housekeeping is outside it; the person's own denies still go, the shipped
    /// no-push among them. Restricted leaves the place exactly as HELP1a made it.
    /// </summary>
    [Fact]
    public void Its_place_under_full_access_asks_nothing_and_keeps_the_persons_denies()
    {
        var place = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), [new("console-ui", "work", "/work/console-ui")]);
        var file = PermissionFile.Empty with
        {
            Machine = new RuleLists(["Bash(npm test:*)"], ["Bash(git commit:*)"], ["WebFetch(domain:secrets.example)"]),
        };

        Assert.Same(place, ChatRunner.HelpUnder(place, HelpAccess.For(HelpModes.Restricted, Adapter("claude-code-acp"))));
        Assert.Equal((true, false, (string?)null), (place.Guarded, place.EveryServer, place.PermissionMode));

        var full = ChatRunner.HelpUnder(place, HelpAccess.For(HelpModes.Full, Adapter("claude-code-acp")));
        var rules = full.Rules(file, "s1");

        Assert.Empty(rules.Allow);
        Assert.Empty(rules.Ask);
        Assert.Contains("WebFetch(domain:secrets.example)", rules.Deny);
        Assert.Contains("Bash(git push:*)", rules.Deny);
        Assert.Equal("!bypassPermissions", full.Posture);
        Assert.Equal((false, true), (full.Guarded, full.EveryServer));
        Assert.Equal((HelpRoom.Repository, PluginServerTo.Help, true), (full.Name, full.ServersTo, full.Keeps));

        var native = ChatRunner.HelpUnder(place, HelpAccess.For(HelpModes.Full, Adapter("claude-code")));
        Assert.Equal("bypassPermissions", native.PermissionMode);
    }

    /// <summary>
    /// A start's place is put under the mode in the driver's file at that start, with what it got for its transcript's first line
    /// (D183 point 3), kept apart from the mode chosen; a repository's conversation is left as it is and says nothing; and full access
    /// on an agent with none such ends the start, saying so.
    /// </summary>
    [Fact]
    public void A_start_takes_the_mode_in_the_file_and_says_what_it_got_first()
    {
        var help = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), []);
        var repository = help with { Name = "engine" };
        var full = DriverConfig.Empty.WithHelpMode(HelpModes.Full);

        Assert.Equal((repository, (string?)null), ChatRunner.UnderHelpMode(repository, Adapter("claude-code-acp"), full));

        var (restricted, saidRestricted) = ChatRunner.UnderHelpMode(help, Adapter("claude-code-acp"), DriverConfig.Empty);
        Assert.Same(help, restricted);
        Assert.StartsWith("— Ask Daoris runs restricted on `claude-code-acp`", saidRestricted);

        var (fully, saidFull) = ChatRunner.UnderHelpMode(help, Adapter("codex-acp"), full);
        Assert.Equal("!agent-full-access", fully.Posture);
        Assert.Contains("full access", saidFull);
        Assert.Contains("takes no rules", saidFull);

        var refused = Assert.Throws<DriverException>(() => ChatRunner.UnderHelpMode(help, Adapter("dsh"), full));
        Assert.Contains("`dsh`", refused.Message);
    }
}
