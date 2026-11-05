using System.Text.Json.Nodes;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The API token's hand-off (D241 point 5): <c>DAORIS_HOST_TOKEN</c> goes to the one plugin process started for it, and the start is
/// refused where no identity serves origin, the identity is read only, Write denies, or no token is set. No process starts.
/// </summary>
public sealed class PluginHostTokenTests : IDisposable
{
    private const string Host = "git.example.invalid";
    private const string Origin = "https://git.example.invalid/acme/app.git";
    private const string Secret = "throwaway-api-token";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-host-token-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Home => Path.Combine(_root, "data");

    private GitIdentity Serving(bool apiToken = true)
    {
        var identity = GitIdentityHttps.AddHttps(Home, Host, "Work", "alice", null, null, "throwaway-https-token");
        if (apiToken) GitIdentityTokens.Set(Home, identity.Id, GitTokens.Api, Secret);
        GitIdentityActs.UseWorkspace(Home, "work", Host, identity.Id);
        return identity;
    }

    private void Deny()
    {
        var file = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        var root = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        root["machineGitAccess"] = new JsonObject { ["write"] = "deny" };
        File.WriteAllText(file, root.ToJsonString());
    }

    private PluginEntry Plugin() => new(
        new PluginManifest("pr", 1, "pr", "1.0.0", "", [], new PluginHooks(["node", "plugin.mjs"], ["quest/consider"]), []),
        _root, Path.Combine(_root, ".data"), Enabled: true, Problem: null);

    [Fact]
    public void The_identity_serving_origin_hands_its_api_token()
    {
        Serving();

        var hand = PluginHostToken.Hand(Home, null, "work", Origin);

        Assert.Null(hand.Refusal);
        Assert.Equal(Secret, hand.Token);
    }

    [Fact]
    public void The_token_is_in_the_environment_of_the_process_started_for_it_and_in_no_other()
    {
        Environment.SetEnvironmentVariable(PluginHostToken.Variable, "inherited-from-the-desktop");
        try
        {
            HookProcess.HostToken.Value = Secret;
            var handed = HookProcess.StartInfo(Plugin(), Home);
            HookProcess.HostToken.Value = null;
            var next = HookProcess.StartInfo(Plugin(), Home);

            Assert.Equal(Secret, handed.Environment[PluginHostToken.Variable]);
            Assert.False(next.Environment.ContainsKey(PluginHostToken.Variable));
        }
        finally
        {
            HookProcess.HostToken.Value = null;
            Environment.SetEnvironmentVariable(PluginHostToken.Variable, null);
        }
    }

    [Fact]
    public void No_identity_serving_origin_refuses_the_start()
    {
        var hand = PluginHostToken.Hand(Home, null, "work", Origin);

        Assert.Null(hand.Token);
        Assert.Contains("no git identity serves", hand.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_read_only_identity_refuses_the_start()
    {
        var identity = Serving();
        GitIdentityActs.ReadOnly(Home, identity.Id, on: true);

        var hand = PluginHostToken.Hand(Home, null, "work", Origin);

        Assert.Null(hand.Token);
        Assert.Contains("read only", hand.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_denied_Write_refuses_the_start()
    {
        Serving();
        Deny();

        var hand = PluginHostToken.Hand(Home, null, "work", Origin);

        Assert.Null(hand.Token);
        Assert.Contains("denies writes", hand.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void An_identity_with_no_api_token_refuses_the_start_and_says_no_value()
    {
        Serving(apiToken: false);

        var hand = PluginHostToken.Hand(Home, null, "work", Origin);

        Assert.Null(hand.Token);
        Assert.Contains("no API token", hand.Refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("throwaway", hand.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_set_does_not_start_a_plugin_whose_hand_is_refused_and_starts_one_with_the_token_alone()
    {
        string? seen = null;
        var started = 0;
        await using var set = new HookSet(
            Home,
            start: (plugin, _, _) =>
            {
                started++;
                seen = HookProcess.HostToken.Value;
                return Task.FromException<IHookChannel>(new DriverException("stub: stops here"));
            },
            say: (_, _) => { },
            hostToken: _ => new HostTokenHand(null, "no git identity serves x, so the plugin was not started"));
        var catalog = new PluginCatalog([Plugin()]);

        await set.ReconcileAsync(catalog, CancellationToken.None);
        Assert.Equal(0, started);

        await using var handed = new HookSet(
            Home,
            start: (plugin, _, _) =>
            {
                started++;
                seen = HookProcess.HostToken.Value;
                return Task.FromException<IHookChannel>(new DriverException("stub: stops here"));
            },
            say: (_, _) => { },
            hostToken: _ => new HostTokenHand(Secret, null));
        await handed.ReconcileAsync(catalog, CancellationToken.None);

        Assert.Equal(1, started);
        Assert.Equal(Secret, seen);
        Assert.Null(HookProcess.HostToken.Value);
    }
}
