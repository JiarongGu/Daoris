using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>The token's adoption runs again when the plugins change, not only at the loop's start (<see cref="PluginTokenAdoptions"/>).</summary>
public sealed class PluginTokenAdoptionsTests : IDisposable
{
    private const string Token = "throwaway-adopted-token";
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-adoptions-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// A token an identity holds when no plugin adopts it waits; a plugin installed after the start, told as the plugins' change, takes
    /// it at once. A change naming no plugin asks nothing.
    /// </summary>
    [Fact]
    public async Task A_plugin_installed_after_the_start_adopts_the_token_when_the_plugins_change()
    {
        var identity = GitIdentityHttps.AddHttps(_home, "tickets.example", "Work", "alice", null, null, "throwaway-https-token");
        GitIdentityTokens.Set(_home, identity.Id, GitTokens.Api, Token);
        var handed = new List<IReadOnlyDictionary<string, string>>();
        PluginTokenAdoption.Round round = (_, _, answers, _) =>
        {
            lock (handed) handed.Add(answers);
            return Task.FromResult(new CommandRound(["kept"], null, null, null, 0));
        };
        using var log = new MachineLog(_home, "desktop", () => DateTimeOffset.UtcNow);
        var adoptions = new PluginTokenAdoptions(_home, log, round);

        await adoptions.Ask();
        Assert.Empty(handed);

        Install();
        adoptions.Told(new StateChangedFrame(["plugins", "harnesses", "driver"], null));
        await adoptions.Ask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Token, Assert.Single(handed)["token"]);
        Assert.False(GitTokens.Has(_home, identity.Id, GitTokens.Api));
    }

    /// <summary>The window's change naming the plugins asks a run.</summary>
    [Fact]
    public async Task A_change_naming_the_plugins_asks_a_run_and_another_does_not()
    {
        var identity = GitIdentityHttps.AddHttps(_home, "tickets.example", "Work", "alice", null, null, "throwaway-https-token");
        GitIdentityTokens.Set(_home, identity.Id, GitTokens.Api, Token);
        Install();
        var ran = new TaskCompletionSource();
        PluginTokenAdoption.Round round = (_, _, _, _) =>
        {
            ran.TrySetResult();
            return Task.FromResult(new CommandRound(["kept"], null, null, null, 0));
        };
        using var log = new MachineLog(_home, "desktop", () => DateTimeOffset.UtcNow);
        var adoptions = new PluginTokenAdoptions(_home, log, round);

        adoptions.Told(new StateChangedFrame(["sessions", "driver"], null));
        await Task.Delay(100);
        Assert.False(ran.Task.IsCompleted);

        adoptions.Told(new StateChangedFrame(["plugins", "harnesses", "driver"], null));
        await ran.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private void Install()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.tickets");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
            """{ "id": "acme.tickets", "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] }, "interfaces": { "commands": [{ "name": "login", "about": "Keeps a token.", "interactive": true, "adopts": { "hosts": ["tickets.example"], "answer": "token" } }], "storage": { "for": "the token" } } }""");
    }
}
