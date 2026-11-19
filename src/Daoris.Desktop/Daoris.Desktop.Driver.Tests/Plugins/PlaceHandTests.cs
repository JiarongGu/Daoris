using System.Text.Json.Nodes;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's start: a place's work process starts where origin may be written, with or without an identity serving it, and is
/// refused where that identity is read only or Write is denied; each start of a plugin that implements storage is handed its own private
/// folder, no other plugin's, and nothing of a git identity's token.
/// </summary>
/// <remarks>It plants the storage's variable in the process's environment, which every class beside it reads, so it runs alone.</remarks>
[Collection(Alone.Name)]
public sealed class PlaceHandTests : IDisposable
{
    private const string Host = "git.example.invalid";
    private const string Origin = "https://git.example.invalid/acme/app.git";

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-place-hand-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "data");

    private GitIdentity Serving()
    {
        var identity = GitIdentityHttps.AddHttps(Home, Host, "Work", "alice", null, null, "throwaway-https-token");
        GitIdentityActs.UseWorkspace(Home, "work", Host, identity.Id);
        return identity;
    }

    private PluginEntry Plugin(string id = "pr", bool storage = true) => new(
        new PluginManifest(id, 1, id, "1.0.0", "", [], new PluginHooks(["node", "plugin.mjs"], ["quest/consider"]), [])
        {
            Interfaces = new([], storage ? new PluginStorageDeclared(AuthoredText.Of("its token")) : null),
        },
        _root, Path.Combine(_root, ".data"), Enabled: true, Problem: null);

    [Fact]
    public void A_place_starts_with_or_without_an_identity_serving_origin()
    {
        Assert.Null(PlaceHands.Judge(Home, null, "work", Origin).Refusal);
        Serving();
        Assert.Null(PlaceHands.Judge(Home, null, "work", Origin).Refusal);
    }

    [Fact]
    public void A_read_only_identity_or_a_denied_Write_refuses_the_start()
    {
        var identity = Serving();
        GitIdentityActs.ReadOnly(Home, identity.Id, on: true);
        Assert.Contains("read only", PlaceHands.Judge(Home, null, "work", Origin).Refusal, StringComparison.Ordinal);

        GitIdentityActs.ReadOnly(Home, identity.Id, on: false);
        var file = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        var root = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        root["machineGitAccess"] = new JsonObject { ["write"] = "deny" };
        File.WriteAllText(file, root.ToJsonString());
        Assert.Contains("denies writes", PlaceHands.Judge(Home, null, "work", Origin).Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_start_is_handed_its_own_private_storage_and_no_other_plugins()
    {
        Environment.SetEnvironmentVariable(PluginPrivate.Variable, "inherited-from-the-desktop");
        try
        {
            var own = HookProcess.StartInfo(Plugin("pr"), Home);
            var other = HookProcess.StartInfo(Plugin("tickets"), Home);
            var none = HookProcess.StartInfo(Plugin("plain", storage: false), Home);

            Assert.Equal(PluginPrivate.Folder(Home, "pr"), own.Environment[PluginPrivate.Variable]);
            Assert.Equal(PluginPrivate.Folder(Home, "tickets"), other.Environment[PluginPrivate.Variable]);
            Assert.True(Directory.Exists(PluginPrivate.Folder(Home, "pr")));
            Assert.False(none.Environment.ContainsKey(PluginPrivate.Variable));
            Assert.False(own.Environment.ContainsKey("DAORIS_HOST_TOKEN"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(PluginPrivate.Variable, null);
        }
    }

    [Fact]
    public void Storage_switched_off_is_handed_to_no_start()
    {
        PluginSwitches.Set(Home, "pr", PluginInterfaces.Storage, PluginPlace.MachineKey, on: false);
        Assert.False(HookProcess.StartInfo(Plugin("pr"), Home).Environment.ContainsKey(PluginPrivate.Variable));
        Assert.Equal([new PluginSwitchWord(PluginInterfaces.Storage, PluginPlace.MachineKey, false)], PluginSwitches.Read(Home).Words("pr"));

        PluginSwitches.Set(Home, "pr", PluginInterfaces.Storage, PluginPlace.MachineKey, on: true);
        Assert.True(HookProcess.StartInfo(Plugin("pr"), Home).Environment.ContainsKey(PluginPrivate.Variable));
    }

    [Fact]
    public async Task The_place_is_judged_by_origins_push_address_where_a_pushurl_differs_from_the_fetch_address()
    {
        GitIdentityActs.ReadOnly(Home, Serving().Id, on: true);
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

        var hand = await PlaceHands.For(Home)(Plugin(), new WorkPlace("app", "work", checkout), CancellationToken.None);

        Assert.Contains("read only on git.example.invalid", hand.Refusal, StringComparison.Ordinal);
    }
}
