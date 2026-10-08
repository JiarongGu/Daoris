using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The second-opinion rule over the bridge (XAGENT1a, D155 point 3, the second-agent design §2.5): the screens' half of
/// `daoris driver opinion`, over the same file, for a repository or a workspace; each edit judged by the twin's table in its
/// words; the state answering what is set as rows, each rule naming the reviewers of the working agent's own family; and the
/// lines answering what each repository resolves to and from where, which only the driver resolves. Declared only.
/// </summary>
public sealed class DriverModuleOpinionTests : DriverModuleBridge
{
    [Fact]
    public async Task Each_edit_writes_the_same_file_the_terminal_edits_and_the_state_answers_it_as_rows()
    {
        // Nothing here reads the registry, so an edit needs no driver come up.
        await AnswerAsync(Module(), "SET_OPINION", new { workspace = "aurora", set = new { reviewers = new[] { "codex-acp", "claude-code-acp" }, required = true } });
        await AnswerAsync(Module(), "SET_OPINION", new { repository = "Tools", none = true });
        var state = await AnswerAsync(Module(), "SET_OPINION", new
        {
            repository = "engine", set = new { reviewers = new[] { "dsh" }, on = new[] { "steps", "landing" }, verify = true, minutes = 30, recheck = false },
        });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("""{"on":["landing","steps"],"reviewers":["dsh"],"verify":true,"minutes":30,"recheck":false}""", OpinionRules.ToJson(config.Opinions["engine"]));
        Assert.True(config.Opinions["tools"].IsNone);
        Assert.Equal("""{"on":["landing"],"reviewers":["codex-acp","claude-code-acp"],"required":true}""", OpinionRules.ToJson(config.WorkspaceOpinions["aurora"]));

        var rows = state.GetProperty("opinions").EnumerateArray().ToList();
        Assert.Equal(["Tools", "engine"], rows.Select(row => row.GetProperty("repository").GetString()));
        Assert.True(rows[0].GetProperty("rule").GetProperty("none").GetBoolean());
        var engine = rows[1].GetProperty("rule");
        Assert.Equal(["landing", "steps"], engine.GetProperty("on").EnumerateArray().Select(each => each.GetString()));
        Assert.True(engine.GetProperty("verify").GetBoolean());
        Assert.False(engine.GetProperty("recheck").GetBoolean());
        Assert.Equal(30, engine.GetProperty("minutes").GetInt32());

        // The machine drives with `claude-code` by default, whose family both Claude Code doors are.
        var shared = state.GetProperty("workspaceOpinions")[0];
        Assert.Equal("aurora", shared.GetProperty("workspace").GetString());
        Assert.Equal(["claude-code-acp"], shared.GetProperty("rule").GetProperty("sameAgent").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(20, shared.GetProperty("rule").GetProperty("minutes").GetInt32());

        var cleared = await AnswerAsync(Module(), "SET_OPINION", new { repository = "engine", clear = true });
        Assert.Single(cleared.GetProperty("opinions").EnumerateArray());
    }

    [Fact]
    public async Task A_refusal_is_the_twins_sentence_and_nothing_is_written()
    {
        var occasion = await RefusalAsync(Module(), "SET_OPINION", new { repository = "engine", set = new { reviewers = new[] { "dsh" }, on = new[] { "merge" } } });
        var neither = await RefusalAsync(Module(), "SET_OPINION", new { clear = true });
        var none = await RefusalAsync(Module(), "SET_OPINION", new { workspace = "aurora", none = true });
        var unnamed = await RefusalAsync(Module(), "SET_OPINION", new { repository = "engine", set = new { required = true } });
        var minutes = await RefusalAsync(Module(), "SET_OPINION", new { repository = "engine", set = new { reviewers = new[] { "dsh" }, minutes = "half" } });

        Assert.Contains("`merge` is not an occasion — `landing`, `steps` or both.", occasion);
        Assert.Contains("a second-opinion rule is set for a repository or a workspace — name exactly one.", neither);
        Assert.Contains("`none` is a repository's", none);
        Assert.Contains("`engine` has no second-opinion rule of its own — name its reviewers first.", unnamed);
        Assert.Contains("`minutes` is a whole number from 5 to 120", minutes);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Opinions.Count > 0);
    }

    /// <summary>Each repository the registry holds, the rule standing for it and where it was set; nothing set is absent.</summary>
    [Fact]
    public async Task The_lines_answer_what_each_repository_resolves_to_and_from_where()
    {
        var config = OpinionRules.Apply(DriverConfig.Empty, new OpinionEdit { Workspace = "aurora", Set = new OpinionSet { Reviewers = ["codex-acp"], Required = true } });
        OpinionRules.Apply(config, new OpinionEdit { Repository = "game", None = true }).Save(DriverConfigPath);
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService())));

        var opinions = (await AnswerAsync(module, "LINES")).GetProperty("opinions").EnumerateArray().ToList();

        Assert.Equal(["engine", "game", "tools"], opinions.Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(["workspace", "repository", null], opinions.Select(row => row.TryGetProperty("source", out var source) ? source.GetString() : null));
        Assert.True(opinions[0].GetProperty("rule").GetProperty("required").GetBoolean());
        Assert.Empty(opinions[0].GetProperty("rule").GetProperty("sameAgent").EnumerateArray());
        Assert.True(opinions[1].GetProperty("rule").GetProperty("none").GetBoolean());
        Assert.False(opinions[2].TryGetProperty("rule", out var nothing) && nothing.ValueKind != JsonValueKind.Null);
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
