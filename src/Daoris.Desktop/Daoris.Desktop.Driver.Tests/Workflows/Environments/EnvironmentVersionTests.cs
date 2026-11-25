using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An environment's version is the content of what it is made of: the declaration's own entry and the content ids of its
/// inputs. Two trees with the same content are the same version wherever and whenever they stand, so a reverted landing is a move back
/// to a version already known, not a new kind of event.
/// </summary>
public sealed class EnvironmentVersionTests
{
    private static readonly EnvironmentSpec Server = new("game-server", ["server/**", "shared/proto/**"], "run-server", ["http"], TimeSpan.FromMinutes(5));

    private static readonly Dictionary<string, string> Trees = new() { ["server/**"] = "tree-a", ["shared/proto/**"] = "tree-b" };

    [Fact]
    public void Content_that_is_the_same_is_the_same_version_whatever_the_order_the_inputs_are_given_in()
    {
        var one = EnvironmentVersion.Of(Server, Trees);
        var other = EnvironmentVersion.Of(Server with { Inputs = ["shared/proto/**", "server/**"] }, new Dictionary<string, string> { ["shared/proto/**"] = "tree-b", ["server/**"] = "tree-a" });

        Assert.Equal(one, other);
        Assert.Equal(12, one.Length);
    }

    [Fact]
    public void A_changed_input_or_a_changed_start_command_is_another_version_and_an_unrelated_path_is_not()
    {
        var before = EnvironmentVersion.Of(Server, Trees);

        Assert.NotEqual(before, EnvironmentVersion.Of(Server, new Dictionary<string, string>(Trees) { ["server/**"] = "tree-a2" }));
        Assert.NotEqual(before, EnvironmentVersion.Of(Server with { Start = "run-server --fast" }, Trees));
        Assert.Equal(before, EnvironmentVersion.Of(Server, new Dictionary<string, string>(Trees) { ["client/**"] = "tree-z" }));
    }

    [Fact]
    public void An_input_the_tree_has_nothing_at_reads_as_absent_not_as_another_input()
    {
        var absent = EnvironmentVersion.Of(Server, new Dictionary<string, string> { ["server/**"] = "tree-a" });

        Assert.NotEqual(EnvironmentVersion.Of(Server, Trees), absent);
        Assert.Equal(absent, EnvironmentVersion.Of(Server, new Dictionary<string, string> { ["server/**"] = "tree-a", ["shared/proto/**"] = "" }));
    }
}
