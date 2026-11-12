using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The growth switch over the bridge: the Settings rows' half of `daoris settings growth`, over the same file, for a workspace or a
/// repository; each edit judged by the rule in its words; the state answering what is set as rows; and the lines answering what each repository
/// resolves to and from where, `mode` from `default` where nothing is set, which is what a new workspace stands at.
/// </summary>
public sealed class DriverModuleGrowthTests : DriverModuleBridge
{
    [Fact]
    public async Task Each_edit_writes_the_same_file_the_terminal_edits_and_the_state_answers_it_as_rows()
    {
        await AnswerAsync(Module(), "SET_GROWTH", new { workspace = "aurora", value = "off" });
        var state = await AnswerAsync(Module(), "SET_GROWTH", new { repository = "Engine", workspace = "aurora", value = "asked" });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("asked", config.Growth["engine"]);
        Assert.Equal("off", config.WorkspaceGrowth["aurora"]);
        var own = Assert.Single(state.GetProperty("growth").EnumerateArray());
        Assert.Equal(("Engine", "asked"), (own.GetProperty("repository").GetString(), own.GetProperty("value").GetString()));
        var shared = Assert.Single(state.GetProperty("workspaceGrowth").EnumerateArray());
        Assert.Equal(("aurora", "off"), (shared.GetProperty("workspace").GetString(), shared.GetProperty("value").GetString()));
    }

    [Fact]
    public async Task Setting_a_workspace_back_to_mode_takes_its_row_away_and_writes_nothing()
    {
        await AnswerAsync(Module(), "SET_GROWTH", new { workspace = "aurora", value = "asked" });

        var state = await AnswerAsync(Module(), "SET_GROWTH", new { workspace = "aurora", value = "mode" });

        Assert.Equal(0, state.GetProperty("workspaceGrowth").GetArrayLength());
        Assert.DoesNotContain("workspaceGrowth", File.ReadAllText(DriverConfigPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refusal_is_the_rules_sentence_and_nothing_is_written()
    {
        var value = await RefusalAsync(Module(), "SET_GROWTH", new { workspace = "aurora", value = "sometimes" });
        var neither = await RefusalAsync(Module(), "SET_GROWTH", new { value = "off" });
        var none = await RefusalAsync(Module(), "SET_GROWTH", new { workspace = "aurora" });

        Assert.Contains("`growth` is `mode` (as the access mode says), `asked` (a Confirm card even in full access) or `off` (refused even in full access).", value);
        Assert.Contains("name the workspace, or a repository and its workspace", neither);
        Assert.Contains("`growth` is `mode`", none);
        Assert.False(File.Exists(DriverConfigPath) && (DriverConfig.Load(DriverConfigPath).Growth.Count > 0 || DriverConfig.Load(DriverConfigPath).WorkspaceGrowth.Count > 0));
    }

    /// <summary>Each repository the registry holds, the value standing for it and where it is set; nothing set is `mode`, from `default`.</summary>
    [Fact]
    public async Task The_lines_answer_what_each_repository_resolves_to_and_from_where_and_a_new_workspace_stands_at_mode()
    {
        var config = GrowthRules.Apply(DriverConfig.Empty, new GrowthEdit { Workspace = "aurora", Value = "off" });
        GrowthRules.Apply(config, new GrowthEdit { Repository = "game", Workspace = "aurora", Value = "asked" }).Save(DriverConfigPath);
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new StandInService()))));

        var rows = (await AnswerAsync(module, "LINES")).GetProperty("growth").EnumerateArray().ToList();

        Assert.Equal(["engine", "game", "tools"], rows.Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(["workspace", "repository", "default"], rows.Select(row => row.GetProperty("source").GetString()));
        Assert.Equal(["off", "asked", "mode"], rows.Select(row => row.GetProperty("value").GetString()));
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
