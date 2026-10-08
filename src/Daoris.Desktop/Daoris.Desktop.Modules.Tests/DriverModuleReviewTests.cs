using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The review rule over the bridge (REVIEWENV1a, D154 point 2, design §1.7): the screens' half of `daoris driver review`, over
/// the same file, for a repository or a workspace; each edit judged by the twin's table in its words; an environment's
/// procedure looked for in the checkouts it reaches; the state answering what is set as rows; and the lines answering what
/// each repository resolves to and from where, which only the driver resolves.
/// </summary>
public sealed class DriverModuleReviewTests : DriverModuleBridge
{
    /// <summary>The checkouts the stand-in registry names, under the test's own home, which goes with it.</summary>
    private string Checkouts => Path.Combine(Home, "checkouts");

    [Fact]
    public async Task Each_edit_writes_the_same_file_the_terminal_edits_and_the_state_answers_it_as_rows()
    {
        var module = await ComeUpAsync();
        await AnswerAsync(module, "SET_REVIEW", new { workspace = "aurora", put = new { name = "dev", kind = "deployed", procedure = "README.md" } });
        await AnswerAsync(module, "SET_REVIEW", new { repository = "Tools", none = true });
        var state = await AnswerAsync(module, "SET_REVIEW", new
        {
            repository = "engine", put = new { name = "local", kind = "local", procedure = "README.md", address = "http://localhost:4200/" }, required = true,
        });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal(
            """{"required":true,"environments":[{"name":"local","kind":"local","procedure":"README.md","address":"http://localhost:4200"}]}""",
            ReviewRules.ToJson(config.Reviews["engine"]));
        Assert.True(config.Reviews["tools"].IsNone);
        Assert.Equal("""{"environments":[{"name":"dev","kind":"deployed","procedure":"README.md"}]}""", ReviewRules.ToJson(config.WorkspaceReviews["aurora"]));

        var rows = state.GetProperty("reviews").EnumerateArray().ToList();
        Assert.Equal(["Tools", "engine"], rows.Select(row => row.GetProperty("repository").GetString()));
        Assert.True(rows[0].GetProperty("rule").GetProperty("none").GetBoolean());
        var engine = rows[1].GetProperty("rule");
        Assert.True(engine.GetProperty("required").GetBoolean());
        Assert.Equal("http://localhost:4200", engine.GetProperty("environments")[0].GetProperty("address").GetString());
        Assert.Equal("aurora", state.GetProperty("workspaceReviews")[0].GetProperty("workspace").GetString());
        Assert.False(state.GetProperty("reviewed").GetProperty("unchecked").GetBoolean());

        var dropped = await AnswerAsync(module, "SET_REVIEW", new { repository = "engine", clear = true });
        Assert.Single(dropped.GetProperty("reviews").EnumerateArray());
        Assert.False(dropped.TryGetProperty("reviewed", out var said) && said.ValueKind != JsonValueKind.Null, "only a put says what its look found");
    }

    [Fact]
    public async Task A_refusal_is_the_twins_sentence_and_nothing_is_written()
    {
        var production = await RefusalAsync(Module(), "SET_REVIEW", new { repository = "engine", put = new { name = "prod", kind = "deployed", procedure = "README.md" } });
        var neither = await RefusalAsync(Module(), "SET_REVIEW", new { clear = true });
        var none = await RefusalAsync(Module(), "SET_REVIEW", new { workspace = "aurora", none = true });

        Assert.Contains("`prod` reads as production, and production is never a review environment", production);
        Assert.Contains("a review rule is set for a repository or a workspace — name exactly one.", neither);
        Assert.Contains("`none` is a repository's", none);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Reviews.Count > 0);
    }

    /// <summary>A put reads the registry, so it waits for the driver like the review does; a change that reads nothing does not.</summary>
    [Fact]
    public async Task A_put_before_the_driver_is_up_is_refused_as_still_coming_up()
    {
        var waiting = await RefusalAsync(Module(), "SET_REVIEW", new { repository = "engine", put = new { name = "dev", kind = "deployed", procedure = "README.md" } });

        Assert.Contains("still coming up", waiting);
        await AnswerAsync(Module(), "SET_REVIEW", new { repository = "engine", none = true });
        Assert.True(DriverConfig.Load(DriverConfigPath).Reviews["engine"].IsNone);
    }

    [Fact]
    public async Task A_procedure_a_repositorys_checkout_does_not_hold_is_refused_and_a_workspaces_names_each_lacking()
    {
        var module = await ComeUpAsync();

        var refused = await RefusalAsync(module, "SET_REVIEW", new { repository = "game", put = new { name = "dev", kind = "deployed", procedure = "README.md" } });
        Assert.Contains(ReviewRules.NotHeld("game", "README.md"), refused);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Reviews.Count > 0);

        var shared = await AnswerAsync(module, "SET_REVIEW", new { workspace = "aurora", put = new { name = "dev", kind = "deployed", procedure = "README.md" } });
        Assert.Equal(["game"], shared.GetProperty("reviewed").GetProperty("lacking").EnumerateArray().Select(each => each.GetString()));

        var nowhere = await AnswerAsync(module, "SET_REVIEW", new { repository = "tools", put = new { name = "dev", kind = "deployed", procedure = "README.md" } });
        Assert.True(nowhere.GetProperty("reviewed").GetProperty("unchecked").GetBoolean());
    }

    /// <summary>Each repository the registry holds, the rule standing for it and where it was set; nothing set is absent.</summary>
    [Fact]
    public async Task The_lines_answer_what_each_repository_resolves_to_and_from_where()
    {
        var config = ReviewRules.Apply(DriverConfig.Empty, new ReviewEdit { Workspace = "aurora", Put = new ReviewSpelled("dev", "deployed", "README.md"), Required = true });
        ReviewRules.Apply(config, new ReviewEdit { Repository = "game", None = true }).Save(DriverConfigPath);
        var module = await ComeUpAsync();

        var reviews = (await AnswerAsync(module, "LINES")).GetProperty("reviews").EnumerateArray().ToList();

        Assert.Equal(["engine", "game", "tools"], reviews.Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(["workspace", "repository", null], reviews.Select(row => row.TryGetProperty("source", out var source) ? source.GetString() : null));
        Assert.True(reviews[0].GetProperty("rule").GetProperty("required").GetBoolean());
        Assert.True(reviews[1].GetProperty("rule").GetProperty("none").GetBoolean());
        Assert.False(reviews[2].TryGetProperty("rule", out var none) && none.ValueKind != JsonValueKind.Null);
    }

    /// <summary>A module whose driver is up, over a stand-in service holding three repositories: two checkouts here, one in no workspace.</summary>
    private async Task<DriverModule> ComeUpAsync()
    {
        Directory.CreateDirectory(Path.Combine(Checkouts, "engine"));
        Directory.CreateDirectory(Path.Combine(Checkouts, "game"));
        File.WriteAllText(Path.Combine(Checkouts, "engine", "README.md"), "# Run it against dev\n");
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService(Checkouts))));
        return module;
    }

    private sealed class StandInService(string checkouts) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string Root(string name) => JsonSerializer.Serialize(Path.Combine(checkouts, name));
            var body = request.RequestUri!.AbsolutePath == "/api/registry"
                ? $$"""[{"repository":"game","workspace":"aurora","root":{{Root("game")}}},{"repository":"engine","workspace":"aurora","root":{{Root("engine")}}},{"repository":"tools"}]"""
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
