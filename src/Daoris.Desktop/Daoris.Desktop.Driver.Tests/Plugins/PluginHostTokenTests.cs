using System.Text.Json.Nodes;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The API token's hand-off: <c>DAORIS_HOST_TOKEN</c> goes to the one plugin process started for it, and the start is
/// refused where no identity serves origin, the identity is read only, Write denies, or no token is set. No process starts.
/// </summary>
/// <remarks>It sets the token's variable in the process's environment, which every class beside it reads, so it runs alone.</remarks>
[Collection(Alone.Name)]
public sealed class PluginHostTokenTests : IDisposable
{
    private const string Host = "git.example.invalid";
    private const string Origin = "https://git.example.invalid/acme/app.git";
    private const string Secret = "throwaway-api-token";

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-host-token-" + Guid.NewGuid().ToString("N")[..8]);

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
    public async Task The_set_starts_a_place_with_the_token_of_its_origin_identity_and_in_no_other_environment()
    {
        Serving();

        var (tokens, _) = await LookAsync();

        Assert.Equal([Secret], tokens);
        Assert.Null(HookProcess.HostToken.Value);
    }

    [Fact]
    public async Task The_set_starts_no_process_where_no_identity_serves_origin()
    {
        var (tokens, said) = await LookAsync();

        Assert.Empty(tokens);
        Assert.Contains(said, line => line.Contains("no git identity serves", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_set_starts_no_process_for_a_read_only_identity()
    {
        GitIdentityActs.ReadOnly(Home, Serving().Id, on: true);

        var (tokens, said) = await LookAsync();

        Assert.Empty(tokens);
        Assert.Contains(said, line => line.Contains("read only", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_set_starts_no_process_where_Write_is_denied()
    {
        Serving();
        Deny();

        var (tokens, said) = await LookAsync();

        Assert.Empty(tokens);
        Assert.Contains(said, line => line.Contains("denies writes", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_set_starts_no_process_for_an_identity_with_no_api_token_and_says_no_value()
    {
        Serving(apiToken: false);

        var (tokens, said) = await LookAsync();

        Assert.Empty(tokens);
        Assert.Contains(said, line => line.Contains("no API token", StringComparison.Ordinal));
        Assert.DoesNotContain(said, line => line.Contains("throwaway", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_place_is_judged_by_origins_push_address_where_a_pushurl_differs_from_the_fetch_address()
    {
        Serving();
        var checkout = Path.Combine(_root, "checkout");
        Directory.CreateDirectory(checkout);
        foreach (var arguments in new[]
        {
            new[] { "init", "-q" },
            ["remote", "add", "origin", "https://elsewhere.example.invalid/acme/app.git"],
            ["remote", "set-url", "--push", "origin", Origin],
        })
        {
            Assert.Equal(0, (await WorkingTree.GitAsync(checkout, arguments, CancellationToken.None)).Code);
        }

        var hand = await PlaceHostTokens.For(Home)(Plugin(), new WorkPlace("app", "work", checkout), CancellationToken.None);

        Assert.Null(hand.Refusal);
        Assert.Equal(Secret, hand.Token);
    }

    /// <summary>One look of a hook set that holds the home, a plugin with a pull-request step awaited at the place <c>work</c>: what started with what token, and what the log says.</summary>
    private async Task<(List<string?> Tokens, List<string> Said)> LookAsync()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins, "pr");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """
            { "id": "pr", "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/watch", "work/step", "work/perform"] },
              "steps": [{ "id": "pull-request", "reach": "performed" }] }
            """);
        var tokens = new List<string?>();
        var said = new List<string>();
        await using var set = new HookSet(
            Home,
            start: (_, _, _) =>
            {
                tokens.Add(HookProcess.HostToken.Value);
                return Task.FromException<IHookChannel>(new DriverException("stub: stops here"));
            },
            say: (_, line) => said.Add(line),
            holdsHome: () => true,
            hostToken: (_, place, _) => Task.FromResult(PluginHostToken.Hand(Home, place.Repository, place.Workspace, Origin)));
        set.Watch(new Dictionary<string, IReadOnlyList<WatchedUnit>>
        {
            ["pr"] = [new WatchedUnit("app", "work", "C:/fam/app", "quest/a", new string('a', 40), "main", null, ["pull-request"]) { Unit = "u1" }],
        });
        await set.ReconcileAsync(PluginCatalog.Load(Home), CancellationToken.None);
        return (tokens, said);
    }
}
