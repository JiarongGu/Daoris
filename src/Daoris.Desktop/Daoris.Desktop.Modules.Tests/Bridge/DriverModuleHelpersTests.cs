using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The helpers rule over the bridge (DOORS1b, D163 point 3, D164's amendment point 3): the screens' half of `daoris driver
/// helpers`, over the same file, for a repository or a workspace; each edit judged by the twin's table in its words; the state
/// answering what is set as rows; and the lines answering what each repository resolves to and from where, the default's
/// source `default` where nothing is set, which only the driver resolves.
/// </summary>
public sealed class DriverModuleHelpersTests : DriverModuleBridge
{
    [Fact]
    public async Task Each_edit_writes_the_same_file_the_terminal_edits_and_the_state_answers_it_as_rows()
    {
        // Nothing here reads the registry, so an edit needs no driver come up.
        await AnswerAsync(Module(), "SET_HELPERS", new { workspace = "aurora", set = new { mode = "on", agents = new[] { "codex-acp", "dsh" } } });
        var state = await AnswerAsync(Module(), "SET_HELPERS", new { repository = "Engine", set = new { mode = "off" } });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("""{"mode":"off","agents":[]}""", HelperRules.ToJson(config.Helpers["engine"]));
        Assert.Equal("""{"mode":"on","agents":["codex-acp","dsh"]}""", HelperRules.ToJson(config.WorkspaceHelpers["aurora"]));

        var own = Assert.Single(state.GetProperty("helpers").EnumerateArray());
        Assert.Equal("Engine", own.GetProperty("repository").GetString());
        Assert.Equal("off", own.GetProperty("rule").GetProperty("mode").GetString());
        Assert.Empty(own.GetProperty("rule").GetProperty("agents").EnumerateArray());
        var shared = Assert.Single(state.GetProperty("workspaceHelpers").EnumerateArray());
        Assert.Equal("aurora", shared.GetProperty("workspace").GetString());
        Assert.Equal(["codex-acp", "dsh"], shared.GetProperty("rule").GetProperty("agents").EnumerateArray().Select(each => each.GetString()));

        // A set names only what it changes: the agents stand under a new mode.
        var moded = await AnswerAsync(Module(), "SET_HELPERS", new { workspace = "aurora", set = new { mode = "ask" } });
        Assert.Equal(["codex-acp", "dsh"], moded.GetProperty("workspaceHelpers")[0].GetProperty("rule").GetProperty("agents").EnumerateArray().Select(each => each.GetString()));

        var cleared = await AnswerAsync(Module(), "SET_HELPERS", new { repository = "engine", clear = true });
        Assert.Equal(0, cleared.GetProperty("helpers").GetArrayLength());
    }

    [Fact]
    public async Task A_refusal_is_the_twins_sentence_and_nothing_is_written()
    {
        var mode = await RefusalAsync(Module(), "SET_HELPERS", new { repository = "engine", set = new { mode = "sometimes" } });
        var neither = await RefusalAsync(Module(), "SET_HELPERS", new { set = new { mode = "on" } });
        var twice = await RefusalAsync(Module(), "SET_HELPERS", new { repository = "engine", set = new { agents = new[] { "dsh", "DSH" } } });
        var both = await RefusalAsync(Module(), "SET_HELPERS", new { repository = "engine", set = new { mode = "on" }, clear = true });

        Assert.Contains("`mode` is `ask`, `on` or `off`", mode);
        Assert.Contains("a helpers rule is set for a repository or a workspace — name exactly one.", neither);
        Assert.Contains("`DSH` is named twice", twice);
        Assert.Contains("one change at a time", both);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Helpers.Count > 0);
    }

    /// <summary>Each repository the registry holds, the rule standing for it and where it was set; nothing set is the default, from `default`.</summary>
    [Fact]
    public async Task The_lines_answer_what_each_repository_resolves_to_and_from_where()
    {
        var config = HelperRules.Apply(DriverConfig.Empty, new HelperEdit { Workspace = "aurora", Set = new HelperSet { Mode = "on", Agents = ["codex-acp"] } });
        HelperRules.Apply(config, new HelperEdit { Repository = "game", Set = new HelperSet { Mode = "off" } }).Save(DriverConfigPath);
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService())));

        var helpers = (await AnswerAsync(module, "LINES")).GetProperty("helpers").EnumerateArray().ToList();

        Assert.Equal(["engine", "game", "tools"], helpers.Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(["workspace", "repository", "default"], helpers.Select(row => row.GetProperty("source").GetString()));
        // HELPERDEFAULT1: the default is `on`, its agents those set up here, which the driver reads as it judges each ask.
        Assert.Equal(["on", "off", "on"], helpers.Select(row => row.GetProperty("rule").GetProperty("mode").GetString()));
        Assert.Equal(["codex-acp"], helpers[0].GetProperty("rule").GetProperty("agents").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal("aurora", helpers[0].GetProperty("workspace").GetString());
        Assert.Empty(helpers[2].GetProperty("rule").GetProperty("agents").EnumerateArray());
    }

    /// <summary>A stand-in service holding three repositories, two in one workspace and one in none.</summary>
    private sealed class StandInService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.AbsolutePath == "/api/registry"
                ? """[{"repository":"game","workspace":"aurora"},{"repository":"engine","workspace":"aurora"},{"repository":"tools"}]"""
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
