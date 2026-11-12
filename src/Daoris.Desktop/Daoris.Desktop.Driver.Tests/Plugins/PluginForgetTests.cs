using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's removal runs the command its <c>storage.forget</c> names, once, <c>from: removal</c>, before its files go: its exit says
/// whether it confirmed signing out, a plugin that fails or never ends in its bound is not confirmed, and the removal goes on either way.
/// </summary>
public sealed class PluginForgetTests : IDisposable
{
    private readonly string _home = ScratchFolders.New("daoris-plugin-forget-");

    public void Dispose() => ScratchFolders.Remove(_home);

    private string Install(string storage)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme-tickets");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), $$"""
            { "id": "acme-tickets", "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] },
              "interfaces": { "commands": [ { "name": "logout", "about": "Forgets the token." }, { "name": "login", "about": "Keeps it.", "interactive": true } ],
                              "storage": {{storage}} } }
            """);
        return folder;
    }

    private static PluginCommandRun.Starter Answering(string answer, List<string>? calls = null) => (_, _) =>
    {
        calls?.Add("started");
        return Task.FromResult<IHookChannel>(new PluginCommandNoticesTests.Channel(answer));
    };

    [Theory]
    [InlineData("""{ "said": ["signed out"], "exit": 0 }""", ForgetOutcome.Confirmed)]
    [InlineData("""{ "said": ["refused"], "exit": 1 }""", ForgetOutcome.NotConfirmed)]
    public void The_forget_command_ends_confirmed_only_at_exit_zero(string answer, ForgetOutcome expected)
    {
        Install("""{ "for": "The token.", "forget": "logout" }""");

        var outcome = PluginForget.Run(_home, PluginCatalog.Load(_home).Plugins.Single(), Answering(answer));

        Assert.Equal(expected, outcome);
    }

    [Fact]
    public void A_plugin_with_no_forget_or_one_that_asks_is_not_run()
    {
        Install("""{ "for": "The token." }""");
        var calls = new List<string>();
        Assert.Equal(ForgetOutcome.None, PluginForget.Run(_home, PluginCatalog.Load(_home).Plugins.Single(), Answering("""{ "exit": 0 }""", calls)));
        Install("""{ "for": "The token.", "forget": "login" }""");
        Assert.Equal(ForgetOutcome.None, PluginForget.Run(_home, PluginCatalog.Load(_home).Plugins.Single(), Answering("""{ "exit": 0 }""", calls)));

        Assert.Empty(calls);
    }

    [Fact]
    public void A_removal_runs_it_before_the_files_go_and_says_the_outcome()
    {
        var folder = Install("""{ "for": "The token.", "forget": "logout" }""");
        var there = new List<bool>();
        PluginCommandRun.Starter start = (_, _) =>
        {
            there.Add(File.Exists(Path.Combine(folder, PluginCatalog.ManifestName)));
            return Task.FromResult<IHookChannel>(new PluginCommandNoticesTests.Channel("""{ "said": ["bye"], "exit": 0 }"""));
        };
        ForgetOutcome? told = null;

        PluginInstall.Remove(_home, "acme-tickets", outcome => told = outcome, start);

        Assert.Equal(ForgetOutcome.Confirmed, told);
        Assert.Equal([true], there);
        Assert.False(Directory.Exists(folder));
    }
}
