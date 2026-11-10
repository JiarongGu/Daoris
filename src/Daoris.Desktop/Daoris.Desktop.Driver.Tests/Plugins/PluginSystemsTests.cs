using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The systems an adapter plugin declares: each a place reached only through its console, by its id, its name and the precondition
/// its writes are guarded by. The rules' one table; a problem refuses the plugin, as a step's does.
/// </summary>
public sealed class PluginSystemsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-systems-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private const string Hooks = """, "hooks": { "command": ["node", "hooks.mjs"], "points": ["plan/read", "apply/perform"] }""";

    private void Plugin(string declared)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.console");
        Directory.CreateDirectory(folder);
        var hooks = declared.Contains("\"hooks\"", StringComparison.Ordinal) ? "" : Hooks;
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), $"{{ \"id\": \"acme.console\"{hooks}{declared} }}");
    }

    /// <summary>[case, what the manifest declares after its id, the refusal's words or null, the systems read as <c>id name preconditions</c> joined by <c>; </c>].</summary>
    [Theory]
    [InlineData("none", "", null, "")]
    [InlineData("one, the least", """, "systems": [{ "id": "store" }] """, null, "store store etag")]
    [InlineData("two, in their order", """, "systems": [{ "id": "store", "name": "The store", "preconditions": "serial" }, { "id": "tickets", "preconditions": "none" }] """, null, "store The store serial; tickets tickets none")]
    [InlineData("a change set the system runs whole", """, "systems": [{ "id": "stack", "preconditions": "changeset" }] """, null, "stack stack changeset")]
    [InlineData("not an array", """, "systems": { "id": "store" } """, "`systems` must be an array of the systems the plugin adapts.", "")]
    [InlineData("no id", """, "systems": [{ "name": "The store" }] """, "a declared system needs an `id` — lowercase letters, digits and dashes, at most 40.", "")]
    [InlineData("an id in capitals", """, "systems": [{ "id": "Store" }] """, "a declared system needs an `id` — lowercase letters, digits and dashes, at most 40.", "")]
    [InlineData("twice", """, "systems": [{ "id": "store" }, { "id": "store" }] """, "system `store` is declared twice.", "")]
    [InlineData("a precondition it has no word for", """, "systems": [{ "id": "store", "preconditions": "lock" }] """, "system `store`'s `preconditions` must be `etag`, `serial`, `changeset` or `none`: what each write is guarded by.", "")]
    [InlineData("hooks naming neither point", """, "hooks": { "command": ["node", "hooks.mjs"], "points": ["work/state"] }, "systems": [{ "id": "store" }] """, "system `store` is planned at `plan/read` and applied at `apply/perform`, so the manifest's `hooks.points` must name both.", "")]
    public void A_manifest_s_systems_are_read_or_refuse_the_plugin(string @case, string declared, string? problem, string systems)
    {
        Plugin(declared);
        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        Assert.True(problem == entry.Problem, $"{@case}: {entry.Problem}");
        Assert.Equal(systems, string.Join("; ", entry.Manifest.Systems.Select(system => $"{system.Id} {system.Name} {system.Preconditions}")));
    }

    [Fact]
    public void The_stand_in_system_example_declares_its_store_guarded_and_unguarded()
    {
        var example = Path.Combine(WorkspaceRoot.Folder, "examples", "plugins", "stand-in-system");
        var (manifest, problem) = PluginCatalog.ReadFolder(example);
        Assert.Null(problem);
        Assert.Equal(["stand-in etag", "stand-in-unguarded none"], manifest.Systems.Select(system => $"{system.Id} {system.Preconditions}"));
    }
}
