using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The commit rule over the bridge (DOORS1b, D164's amendment points 1 and 3): the screens' half of `daoris driver commits`,
/// over the same file, for a repository or a workspace; each edit judged by the twin's table in its words; the state answering
/// what is set as rows; and the lines answering what each repository resolves to and from where, the default's source
/// `default` where nothing is set, which only the driver resolves.
/// </summary>
public sealed class DriverModuleCommitsTests : DriverModuleBridge
{
    [Fact]
    public async Task Each_edit_writes_the_same_file_the_terminal_edits_and_the_state_answers_it_as_rows()
    {
        // Nothing here reads the registry, so an edit needs no driver come up.
        await AnswerAsync(Module(), "SET_COMMITS", new { workspace = "aurora", set = new { mode = "at-end", convention = "  Imperative subject line.  " } });
        var state = await AnswerAsync(Module(), "SET_COMMITS", new { repository = "engine", set = new { mode = "ask" } });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("""{"mode":"ask"}""", CommitRules.ToJson(config.Commits["engine"]));
        Assert.Equal("""{"mode":"at-end","convention":"Imperative subject line."}""", CommitRules.ToJson(config.WorkspaceCommits["aurora"]));

        var own = Assert.Single(state.GetProperty("commits").EnumerateArray());
        Assert.Equal("engine", own.GetProperty("repository").GetString());
        Assert.Equal("ask", own.GetProperty("rule").GetProperty("mode").GetString());
        // None is left out, as every null the bridge carries.
        Assert.False(own.GetProperty("rule").TryGetProperty("convention", out var none) && none.ValueKind != JsonValueKind.Null);
        var shared = Assert.Single(state.GetProperty("workspaceCommits").EnumerateArray());
        Assert.Equal("aurora", shared.GetProperty("workspace").GetString());
        Assert.Equal("Imperative subject line.", shared.GetProperty("rule").GetProperty("convention").GetString());

        // The empty convention takes the standing one away, as the terminal's `--no-convention` does; the mode stands.
        var taken = await AnswerAsync(Module(), "SET_COMMITS", new { workspace = "aurora", set = new { convention = "" } });
        var rule = taken.GetProperty("workspaceCommits")[0].GetProperty("rule");
        Assert.Equal("at-end", rule.GetProperty("mode").GetString());
        Assert.False(rule.TryGetProperty("convention", out var gone) && gone.ValueKind != JsonValueKind.Null);

        var cleared = await AnswerAsync(Module(), "SET_COMMITS", new { repository = "engine", clear = true });
        Assert.Equal(0, cleared.GetProperty("commits").GetArrayLength());
    }

    [Fact]
    public async Task A_refusal_is_the_twins_sentence_and_nothing_is_written()
    {
        var mode = await RefusalAsync(Module(), "SET_COMMITS", new { repository = "engine", set = new { mode = "whenever" } });
        var neither = await RefusalAsync(Module(), "SET_COMMITS", new { set = new { mode = "ask" } });
        var broken = await RefusalAsync(Module(), "SET_COMMITS", new { repository = "engine", set = new { convention = "One line.\nAnother." } });
        var longer = await RefusalAsync(Module(), "SET_COMMITS", new { repository = "engine", set = new { convention = new string('a', 401) } });

        Assert.Contains("`mode` is `as-you-go`, `ask` or `at-end`", mode);
        Assert.Contains("a commit rule is set for a repository or a workspace — name exactly one.", neither);
        Assert.Contains("one paragraph with no line break, at most 400 characters", broken);
        Assert.Contains("one paragraph with no line break, at most 400 characters", longer);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Commits.Count > 0);
    }

    /// <summary>Each repository the registry holds, the rule standing for it and where it was set; nothing set is the default, from `default`.</summary>
    [Fact]
    public async Task The_lines_answer_what_each_repository_resolves_to_and_from_where()
    {
        var config = CommitRules.Apply(DriverConfig.Empty, new CommitEdit
        {
            Workspace = "aurora", Set = new CommitSet { Mode = "ask", ModeGiven = true, Convention = "Say why.", ConventionGiven = true },
        });
        CommitRules.Apply(config, new CommitEdit { Repository = "game", Set = new CommitSet { Mode = "at-end", ModeGiven = true } }).Save(DriverConfigPath);
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService())));

        var commits = (await AnswerAsync(module, "LINES")).GetProperty("commits").EnumerateArray().ToList();

        Assert.Equal(["engine", "game", "tools"], commits.Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(["workspace", "repository", "default"], commits.Select(row => row.GetProperty("source").GetString()));
        Assert.Equal(["ask", "at-end", "as-you-go"], commits.Select(row => row.GetProperty("rule").GetProperty("mode").GetString()));
        Assert.Equal("Say why.", commits[0].GetProperty("rule").GetProperty("convention").GetString());
        Assert.Equal("aurora", commits[0].GetProperty("workspace").GetString());
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
