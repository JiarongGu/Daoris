using System.Text.Json;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's id is <c>&lt;family&gt;-&lt;name&gt;</c>: the kit asks for the family and keeps Daoris's own to this repository's examples,
/// and a terminal names a plugin running commands by its id, or by the id without its family where no other installed plugin's id
/// ends the same, an ambiguous short form refused with the candidates named.
/// </summary>
public sealed class PluginFamilyTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-plugin-family-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "home");

    private string Scratch => Path.Combine(_root, "scratch");

    public void Dispose()
    {
        GitSeed.Discard(_root);
    }

    [Theory]
    [InlineData("acme-quiet-hours", "acme", "quiet-hours")]
    [InlineData("daoris-azure-devops", "daoris", "azure-devops")]
    [InlineData("quiet", null, null)]
    [InlineData("-quiet", null, null)]
    [InlineData("quiet-", null, null)]
    public void The_family_is_the_word_before_the_id_s_first_dash(string id, string? family, string? tail)
    {
        Assert.Equal(family, PluginFamily.Of(id));
        Assert.Equal(tail, PluginFamily.Tail(id));
    }

    [Fact]
    public void The_kit_asks_for_the_family()
    {
        Directory.CreateDirectory(Scratch);
        var refused = Assert.Throws<DriverException>(() => PluginKit.Plan("quiet", [HookPoints.QuestConsider], Scratch));
        Assert.Contains("names no family", refused.Message, StringComparison.Ordinal);
        Assert.Contains("`<family>-<name>`", refused.Message, StringComparison.Ordinal);
        Assert.Equal("acme-quiet", PluginKit.Plan("acme-quiet", [HookPoints.QuestConsider], Scratch).Id);
    }

    [Fact]
    public void The_kit_keeps_daoris_s_own_family_to_this_repository_s_examples()
    {
        Directory.CreateDirectory(Scratch);
        var refused = Assert.Throws<DriverException>(() => PluginKit.Plan("daoris-quiet", [HookPoints.QuestConsider], Scratch));
        Assert.Contains("Daoris's own family", refused.Message, StringComparison.Ordinal);
        Assert.Contains("`<your-family>-quiet`", refused.Message, StringComparison.Ordinal);

        // Planned only: nothing is written into the repository's examples.
        var examples = Path.Combine(GitSeed.RepoRoot(), "examples", "plugins");
        Assert.Equal("daoris-quiet", PluginKit.Plan("daoris-quiet", [HookPoints.QuestConsider], examples).Id);
    }

    private void Install(string id, bool commands = true)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        var interfaces = commands ? """, "interfaces": { "commands": [{ "name": "status", "about": "Says what is kept." }] }""" : "";
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
            $$"""{ "id": "{{id}}", "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] }{{interfaces}} }""");
    }

    /// <summary>A plugin's process that says which plugin it is at each round.</summary>
    private sealed class Channel(string id) : IHookChannel
    {
        public IReadOnlyList<string> Points => [];

        public bool Alive => true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public void Listen(Action<JsonElement?>? steps) { }

        public Task WatchAsync(WorkWatchFrame frame, CancellationToken ct) => Task.CompletedTask;

        public Task<JsonElement> CommandAsync(object frame, CancellationToken ct) =>
            Task.FromResult(JsonDocument.Parse($$"""{ "said": ["{{id}} answered"], "exit": 0 }""").RootElement.Clone());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static PluginCommandRun.Starter Start => (plugin, _) => Task.FromResult<IHookChannel>(new Channel(plugin.Manifest.Id));

    private async Task<(int Exit, string Out, string Error)> Run(params string[] args)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        var exit = await PluginCommandConsole.RunAsync(Home, args, new PluginCommandConsole.Terminal(output, error, _ => null, _ => null), CancellationToken.None, Start);
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task A_plugin_s_commands_are_named_by_its_id_or_by_its_id_without_its_family()
    {
        Install("acme-tickets");
        Install("acme-github", commands: false);

        Assert.True(PluginCommandConsole.Names(Home, "acme-tickets"));
        Assert.True(PluginCommandConsole.Names(Home, "tickets"));
        Assert.False(PluginCommandConsole.Names(Home, "github"));
        Assert.False(PluginCommandConsole.Names(Home, "acme"));

        var (exit, said, _) = await Run("tickets", "status");
        Assert.Equal(0, exit);
        Assert.Contains("acme-tickets answered", said, StringComparison.Ordinal);
        Assert.Contains("acme-tickets answered", (await Run("acme-tickets", "status")).Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_short_form_two_plugins_share_is_refused_naming_both()
    {
        Install("acme-tickets");
        Install("other-tickets");

        Assert.True(PluginCommandConsole.Names(Home, "tickets"));
        var (exit, said, refused) = await Run("tickets", "status");
        Assert.Equal(2, exit);
        Assert.Empty(said);
        Assert.Contains("`acme-tickets`", refused, StringComparison.Ordinal);
        Assert.Contains("`other-tickets`", refused, StringComparison.Ordinal);
        Assert.Contains("other-tickets answered", (await Run("other-tickets", "status")).Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_id_named_whole_comes_before_another_s_short_form()
    {
        Install("tickets");
        Install("acme-tickets");

        Assert.Contains("tickets answered", (await Run("tickets", "status")).Out, StringComparison.Ordinal);
        Assert.DoesNotContain("acme-tickets", (await Run("tickets", "status")).Out, StringComparison.Ordinal);
    }
}
