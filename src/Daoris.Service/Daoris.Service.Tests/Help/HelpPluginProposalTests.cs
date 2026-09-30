using System.Text.Json;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>plugin</c> kind's writer (PLUG9): a plugin that has landed, added from its folder in the checkout of the
/// repository that holds it, or one installed here switched on or off — and since PLUG9 (c) and (d) one of the
/// install's own by id, or an update. The driver reads the folder with the catalogue's own reader; here only the
/// shape is checked.
/// </summary>
public sealed class HelpPluginProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_plugin_is_written_with_its_repository_and_folder_or_its_id()
    {
        var (add, message) = Box().ProposePlugin("add", id: null, folder: "plugins/quiet-hours", repository: "house-plugins",
            "the person wants quests held overnight", "h1", Now);
        var (off, _) = Box().ProposePlugin(" Disable ", "example.lands", folder: null, repository: null, "the person wants it off", "h1", Now);
        var whole = Path.Combine(_home, "given", "quiet-hours");
        var (given, _) = Box().ProposePlugin("add", null, whole, null, "the person named the folder", "h1", Now);

        Assert.Contains($"#{add}", message);
        var added = Written(add!);
        Assert.Equal(("plugin", "add"), (added.GetProperty("kind").GetString(), added.GetProperty("door").GetString()));
        Assert.Equal(("house-plugins", "plugins/quiet-hours"), (added.GetProperty("repository").GetString(), added.GetProperty("folder").GetString()));
        Assert.Equal(JsonValueKind.Null, added.GetProperty("target").ValueKind);
        var switched = Written(off!);
        Assert.Equal(("plugin", "disable", "example.lands"),
            (switched.GetProperty("kind").GetString(), switched.GetProperty("door").GetString(), switched.GetProperty("target").GetString()));
        Assert.Equal(JsonValueKind.Null, switched.GetProperty("folder").ValueKind);
        Assert.Equal(JsonValueKind.Null, switched.GetProperty("repository").ValueKind);
        // A whole path only when the person gave one, and then it names no repository.
        Assert.Equal(whole, Written(given!).GetProperty("folder").GetString());
        Assert.Equal(JsonValueKind.Null, Written(given!).GetProperty("repository").ValueKind);
    }

    /// <summary>
    /// PLUG9 (c) and (d): an add may name one of the install's own plugins by its id (<c>offer</c>, never a path),
    /// and an <c>update</c> names an installed plugin by its id. Whether the install offers it, and whether the
    /// plugin recorded where it came from, is the driver's to judge.
    /// </summary>
    [Fact]
    public void An_offer_is_written_by_its_id_and_an_update_names_the_installed_plugin()
    {
        var (offered, _) = Box().ProposePlugin("add", null, null, null, "the person wants pull requests opened", "h1", Now, offer: "github-pull-request");
        var (updated, _) = Box().ProposePlugin(" Update ", "acme.quiet-hours", null, null, "a newer one has landed", "h1", Now);

        var added = Written(offered!);
        Assert.Equal(("plugin", "add", "github-pull-request"),
            (added.GetProperty("kind").GetString(), added.GetProperty("door").GetString(), added.GetProperty("offer").GetString()));
        Assert.Equal(JsonValueKind.Null, added.GetProperty("folder").ValueKind);
        Assert.Equal(JsonValueKind.Null, added.GetProperty("repository").ValueKind);
        Assert.Equal(JsonValueKind.Null, added.GetProperty("target").ValueKind);
        var update = Written(updated!);
        Assert.Equal(("plugin", "update", "acme.quiet-hours"),
            (update.GetProperty("kind").GetString(), update.GetProperty("door").GetString(), update.GetProperty("target").GetString()));
        Assert.Equal(JsonValueKind.Null, update.GetProperty("offer").ValueKind);
    }

    [Theory]
    [InlineData("add", "", "quiet-hours", "house-plugins", "github-pull-request", "an offer or a folder, never both")]
    [InlineData("add", "", "", "", "two words", "one word")]
    [InlineData("add", "acme.x", "", "", "github-pull-request", "names no id")]
    [InlineData("update", "", "", "", "", "`update` names the plugin by its id")]
    [InlineData("update", "acme.x", "quiet-hours", "", "", "names only the plugin's id")]
    [InlineData("enable", "acme.x", "", "", "github-pull-request", "names only the plugin's id")]
    public void A_malformed_offer_or_update_is_refused_with_nothing_written(
        string action, string id, string folder, string repository, string offer, string says)
    {
        var (written, message) = Box().ProposePlugin(action, Named(id), Named(folder), Named(repository), "a reason", "h1", Now, offer: Named(offer));

        Assert.Null(written);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }

    /// <summary>PLUG9: a plugin proposal's shape, checked here and nothing more; the driver's twin table holds what the catalogue says.</summary>
    [Theory]
    [InlineData("install", "", "quiet-hours", "house-plugins", "`add`, `enable`, `disable` or `update`")]
    [InlineData("add", "", "", "house-plugins", "names the plugin's folder")]
    [InlineData("add", "acme.quiet-hours", "quiet-hours", "house-plugins", "names no id")]
    [InlineData("add", "", "quiet-hours", "", "the repository whose checkout holds it")]
    [InlineData("add", "", "../elsewhere/quiet-hours", "house-plugins", "stays inside its checkout")]
    [InlineData("add", "", "quiet-hours", "two words", "one word")]
    [InlineData("enable", "", "", "", "names the plugin by its id")]
    [InlineData("disable", "example.lands", "quiet-hours", "", "names only the plugin's id")]
    [InlineData("enable", "two words", "", "", "one word")]
    public void A_malformed_plugin_proposal_is_refused_with_nothing_written(string action, string id, string folder, string repository, string says)
    {
        var (written, message) = Box().ProposePlugin(action, Named(id), Named(folder), Named(repository), "a reason", "h1", Now);

        Assert.Null(written);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }

    /// <summary>PLUG9: a folder in a repository is written from its checkout's root, never as a whole path.</summary>
    [Fact]
    public void A_folder_in_a_repository_is_never_a_whole_path()
    {
        var (id, message) = Box().ProposePlugin("add", null, Path.Combine(_home, "quiet-hours"), "house-plugins", "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains("from its checkout's root", message);
        Assert.Null(Box().ProposePlugin("add", null, "quiet-hours", "house-plugins", " ", "h1", Now).Id);
    }
}
