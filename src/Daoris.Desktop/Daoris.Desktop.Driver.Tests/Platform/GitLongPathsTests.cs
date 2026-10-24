using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREELONG1's shapes: a session's start is handed <c>core.longpaths=true</c> in its command scope, after what the scope already
/// holds, unless the person's configuration or the scope itself already says it. <see cref="GitLongPathsProcessTests"/> holds that a
/// real git reads a deep tree clean so.
/// </summary>
public sealed class GitLongPathsTests
{
    [Theory]
    [InlineData("local\ttrue\n", true)]
    [InlineData("global\tfalse\n", true)]
    [InlineData("command\ttrue\nsystem\ttrue\n", true)]
    [InlineData("command\ttrue\n", false)]
    [InlineData("", false)]
    public void Only_a_scope_of_the_persons_says_it(string scoped, bool says)
    {
        Assert.Equal(says, GitLongPaths.Says(scoped));
    }

    [Fact]
    public void A_start_is_handed_it_after_the_author()
    {
        var info = new ProcessStartInfo("x");
        info.Environment.Remove("GIT_CONFIG_COUNT");
        GitAuthor.Hand(info.Environment, ("Work Name", "work@example.invalid"));

        GitLongPaths.Hand(info.Environment, needed: true);

        Assert.Equal("3", info.Environment["GIT_CONFIG_COUNT"]);
        Assert.Equal(("core.longpaths", "true"), (info.Environment["GIT_CONFIG_KEY_2"], info.Environment["GIT_CONFIG_VALUE_2"]));
    }

    [Fact]
    public void A_start_whose_person_or_scope_says_it_is_handed_nothing()
    {
        var info = new ProcessStartInfo("x");
        info.Environment.Remove("GIT_CONFIG_COUNT");
        GitLongPaths.Hand(info.Environment, needed: false);
        Assert.False(info.Environment.ContainsKey("GIT_CONFIG_COUNT"));

        GitRouting.Scope(info.Environment, [("core.longPaths", "false")]);
        GitLongPaths.Hand(info.Environment, needed: true);
        Assert.Equal("1", info.Environment["GIT_CONFIG_COUNT"]);
    }
}
