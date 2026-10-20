using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The hand-over rule over the bridge (MIXAGENT1c, D163 point 3): the screens' half of `daoris driver handover`, over the same file,
/// for a repository or a workspace; each edit judged by the twin's table in its words; the state answering what is set as rows; and
/// the lines answering what each repository resolves to and from where, `default` where nothing is set.
/// </summary>
public sealed class DriverModuleHandOverRuleTests : DriverModuleBridge
{
    [Fact]
    public async Task Each_edit_writes_the_same_file_the_terminal_edits_and_the_state_answers_it_as_rows()
    {
        await AnswerAsync(Module(), "SET_HAND_OVER", new { workspace = "aurora", set = new { mode = "on" } });
        var state = await AnswerAsync(Module(), "SET_HAND_OVER", new { repository = "Engine", set = new { mode = "off" } });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("""{"mode":"off"}""", HandOverRules.ToJson(config.HandOver["engine"]));
        Assert.Equal("""{"mode":"on"}""", HandOverRules.ToJson(config.WorkspaceHandOver["aurora"]));

        var own = Assert.Single(state.GetProperty("handOver").EnumerateArray());
        Assert.Equal(("Engine", "off"), (own.GetProperty("repository").GetString(), own.GetProperty("rule").GetProperty("mode").GetString()));
        var shared = Assert.Single(state.GetProperty("workspaceHandOver").EnumerateArray());
        Assert.Equal(("aurora", "on"), (shared.GetProperty("workspace").GetString(), shared.GetProperty("rule").GetProperty("mode").GetString()));

        var cleared = await AnswerAsync(Module(), "SET_HAND_OVER", new { repository = "engine", clear = true });
        Assert.Equal(0, cleared.GetProperty("handOver").GetArrayLength());
    }

    [Fact]
    public async Task A_refusal_is_the_twins_sentence_and_nothing_is_written()
    {
        var mode = await RefusalAsync(Module(), "SET_HAND_OVER", new { repository = "engine", set = new { mode = "sometimes" } });
        var neither = await RefusalAsync(Module(), "SET_HAND_OVER", new { set = new { mode = "on" } });
        var both = await RefusalAsync(Module(), "SET_HAND_OVER", new { repository = "engine", set = new { mode = "on" }, clear = true });

        Assert.Contains("`mode` is `ask`, `on` or `off`", mode);
        Assert.Contains("a hand-over rule is set for a repository or a workspace — name exactly one.", neither);
        Assert.Contains("one change at a time", both);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).HandOver.Count > 0);
    }

    /// <summary>Each repository the registry holds, the rule standing for it and where it was set; nothing set is the default, from `default`.</summary>
    [Fact]
    public async Task The_lines_answer_what_each_repository_resolves_to_and_from_where()
    {
        var config = HandOverRules.Apply(DriverConfig.Empty, new HandOverEdit { Workspace = "aurora", Set = new HandOverSet { Mode = "on", ModeGiven = true } });
        HandOverRules.Apply(config, new HandOverEdit { Repository = "game", Set = new HandOverSet { Mode = "off", ModeGiven = true } }).Save(DriverConfigPath);
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService())));

        var rows = (await AnswerAsync(module, "LINES")).GetProperty("handOver").EnumerateArray().ToList();

        Assert.Equal(["engine", "game", "tools"], rows.Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(["workspace", "repository", "default"], rows.Select(row => row.GetProperty("source").GetString()));
        Assert.Equal(["on", "off", "ask"], rows.Select(row => row.GetProperty("rule").GetProperty("mode").GetString()));
        Assert.Equal("aurora", rows[0].GetProperty("workspace").GetString());
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
