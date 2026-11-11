using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A refused step's <c>needs</c> is judged against the plugin's declared commands where the run is read.</summary>
public sealed class RunNeedsTests
{
    private const string Unit = "run:r1";

    private static NodeRecord Failed(string? needs, string state = NodeStates.Failed) =>
        new(Unit)
        {
            Statuses = new Dictionary<string, NodeStatus> { ["deploy"] = new(Unit, "deploy", state) { Attempt = 1, Code = "signed-out", Needs = needs } },
            Attempts =
            [
                new NodeAttempt("deploy", 1, "acme.deploy", new WorkPerformCall(Unit, "deploy", 1, "inv-1", "key-1", "tip", [], DateTimeOffset.UnixEpoch), AttemptStates.Settled),
            ],
        };

    private static PluginInterfacesDeclared? Declared(string plugin) =>
        plugin == "acme.deploy"
            ? new PluginInterfacesDeclared([new PluginCommandDeclared("login", AuthoredText.Of("Sign in"), true, false) { Title = AuthoredText.Of("Sign in") }], null)
            : null;

    [Fact]
    public void A_needs_naming_a_declared_command_is_kept_with_its_plugin_and_title()
    {
        var read = RunNeeds.Of(Failed("login"), Declared);

        var need = Assert.Single(read).Value;
        Assert.Equal(("acme.deploy", "login", "Sign in"), (need.Needs!.Plugin, need.Needs.Command, need.Needs.Title.ToString()));
        Assert.Null(need.Said);
    }

    [Fact]
    public void A_needs_naming_no_declared_command_is_ignored_and_said()
    {
        var need = Assert.Single(RunNeeds.Of(Failed("logout"), Declared)).Value;

        Assert.Null(need.Needs);
        Assert.Contains("`logout`", need.Said);
        Assert.Contains("acme.deploy", need.Said);
    }

    [Fact]
    public void A_step_that_did_not_fail_or_named_nothing_has_none()
    {
        Assert.Empty(RunNeeds.Of(Failed(null), Declared));
        Assert.Empty(RunNeeds.Of(Failed("login", NodeStates.Reached), Declared));
        Assert.Empty(RunNeeds.Of(null, Declared));
    }
}
