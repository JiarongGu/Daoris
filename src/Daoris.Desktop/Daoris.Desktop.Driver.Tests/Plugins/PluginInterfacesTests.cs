using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The interfaces a plugin declares it implements, the only ways Daoris reaches it besides its points: its commands, run in its own
/// process, and its private storage, a folder handed to that process alone. The rules' one table; a problem refuses the plugin, as a
/// step's does.
/// </summary>
public sealed class PluginInterfacesTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-interfaces-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private const string Hooks = """, "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] }""";

    private void Plugin(string declared)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.tickets");
        Directory.CreateDirectory(folder);
        var hooks = declared.Contains("\"hooks\"", StringComparison.Ordinal) || declared.Contains("\"nohooks\"", StringComparison.Ordinal) ? "" : Hooks;
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), $"{{ \"id\": \"acme.tickets\"{hooks}{declared} }}");
    }

    /// <summary>[case, what the manifest declares after its id, the refusal's words or null, what is read: each command as <c>name|interactive|tool</c>, then <c>storage</c> where declared].</summary>
    [Theory]
    [InlineData("none", "", null, "")]
    [InlineData("a command, the least", """, "interfaces": { "commands": [{ "name": "status", "about": "Says what is kept." }] } """, null, "status|False|False")]
    [InlineData("commands and storage", """, "interfaces": { "commands": [{ "name": "login", "about": "Keeps a token.", "interactive": true }, { "name": "status", "about": "Says what is kept.", "tool": true }], "storage": { "for": "The token login keeps." } } """, null, "login|True|False; status|False|True; storage")]
    [InlineData("not an object", """, "interfaces": ["commands"] """, "`interfaces` must be an object naming the interfaces the plugin implements: `commands`, `storage`.", "")]
    [InlineData("one this build does not know", """, "interfaces": { "pull-request": {} } """, "interface `pull-request` is not one this build knows: `commands`, `storage`.", "")]
    [InlineData("commands not an array", """, "interfaces": { "commands": { "name": "login" } } """, "`interfaces.commands` must be an array of the commands the plugin runs.", "")]
    [InlineData("no name", """, "interfaces": { "commands": [{ "about": "x" }] } """, "a declared command needs a `name` — lowercase letters, digits and dashes, at most 40.", "")]
    [InlineData("twice", """, "interfaces": { "commands": [{ "name": "login", "about": "x" }, { "name": "login", "about": "y" }] } """, "command `login` is declared twice.", "")]
    [InlineData("no about", """, "interfaces": { "commands": [{ "name": "login" }] } """, "command `login` needs an `about`: what it does, in a sentence.", "")]
    [InlineData("interactive not a flag", """, "interfaces": { "commands": [{ "name": "login", "about": "x", "interactive": "yes" }] } """, "command `login`'s `interactive` must be true or false: whether it asks the person.", "")]
    [InlineData("an agent's tool that asks the person", """, "interfaces": { "commands": [{ "name": "login", "about": "x", "interactive": true, "tool": true }] } """, "command `login` asks the person, so it is never an agent's tool.", "")]
    [InlineData("an adoption", """, "interfaces": { "commands": [{ "name": "login", "about": "x", "interactive": true, "adopts": { "hosts": ["tickets.example"], "answer": "token" } }] } """, null, "login|True|False")]
    [InlineData("an adoption with no answer", """, "interfaces": { "commands": [{ "name": "login", "about": "x", "adopts": { "hosts": ["tickets.example"] } }] } """, "command `login`'s `adopts` must name its `hosts` and the `answer` the token is sent as.", "")]
    [InlineData("storage with no for", """, "interfaces": { "storage": {} } """, "`interfaces.storage` needs a `for`: what the plugin keeps there.", "")]
    [InlineData("no process to reach", """, "nohooks": true, "interfaces": { "storage": { "for": "x" } } """, "interface `storage` is reached through the plugin's process, so the manifest must declare `hooks`.", "")]
    public void A_manifest_s_interfaces_are_read_or_refuse_the_plugin(string @case, string declared, string? problem, string read)
    {
        Plugin(declared);
        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        Assert.True(problem == entry.Problem, $"{@case}: {entry.Problem}");
        var interfaces = entry.Manifest.Interfaces;
        IEnumerable<string> said = [.. interfaces.Commands.Select(command => $"{command.Name}|{command.Interactive}|{command.Tool}"), .. interfaces.Storage is null ? [] : new[] { "storage" }];
        Assert.Equal(read, string.Join("; ", said));
    }

    [Theory]
    [InlineData("azure-devops-pull-request")]
    [InlineData("github-pull-request")]
    public void Each_pull_request_example_signs_in_through_its_own_commands_and_storage(string example)
    {
        var (manifest, problem) = PluginCatalog.ReadFolder(Path.Combine(WorkspaceRoot.Folder, "examples", "plugins", example));
        Assert.Null(problem);
        Assert.Equal(["login", "logout", "status"], manifest.Interfaces.Commands.Select(command => command.Name));
        Assert.True(manifest.Interfaces.Commands[0].Interactive);
        Assert.NotNull(manifest.Interfaces.Storage);
    }
}
