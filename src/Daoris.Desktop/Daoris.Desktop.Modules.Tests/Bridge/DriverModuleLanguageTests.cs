using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The session language over the bridge (LANG1c, D142 point 7): the screens' half of `daoris driver language`, over the same
/// file, for a repository or a workspace; the state answering what is set as rows, so no key policy on the bridge respells a
/// name, and the closed table each option is named from; and the lines answering what each repository resolves to and from
/// where, which only the driver resolves.
/// </summary>
public sealed class DriverModuleLanguageTests : DriverModuleBridge
{
    [Fact]
    public async Task Setting_a_language_writes_the_same_file_the_terminal_edits()
    {
        var module = Module();
        await AnswerAsync(module, "SET_LANGUAGE", new { workspace = "aurora", language = "en" });
        var state = await AnswerAsync(module, "SET_LANGUAGE", new { repository = "Engine", language = "ZH" });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("zh", config.Languages["engine"]);
        Assert.Equal("en", config.WorkspaceLanguages["aurora"]);
        var row = Assert.Single(state.GetProperty("languages").EnumerateArray());
        Assert.Equal("Engine", row.GetProperty("repository").GetString());
        Assert.Equal("zh", row.GetProperty("language").GetString());
        Assert.Equal("aurora", state.GetProperty("workspaceLanguages")[0].GetProperty("workspace").GetString());

        var cleared = await AnswerAsync(module, "SET_LANGUAGE", new { repository = "engine" });
        Assert.Equal(0, cleared.GetProperty("languages").GetArrayLength());
        Assert.Equal(1, cleared.GetProperty("workspaceLanguages").GetArrayLength());
    }

    [Fact]
    public async Task A_code_the_table_does_not_hold_or_a_change_naming_neither_or_both_is_refused_and_nothing_is_written()
    {
        var french = await RefusalAsync(Module(), "SET_LANGUAGE", new { repository = "engine", language = "fr" });
        var neither = await RefusalAsync(Module(), "SET_LANGUAGE", new { language = "zh" });
        var both = await RefusalAsync(Module(), "SET_LANGUAGE", new { repository = "engine", workspace = "aurora", language = "zh" });

        Assert.Contains(SessionLanguages.Refusal("fr"), french);
        Assert.Contains("a `repository` or a `workspace`", neither);
        Assert.Contains("a `repository` or a `workspace`", both);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Languages.Count > 0);
    }

    /// <summary>The table the screens name each option from: the driver's, code and name, in its order. Nothing set answers none.</summary>
    [Fact]
    public async Task The_state_answers_the_table_and_a_machine_that_set_none_answers_none()
    {
        var state = await AnswerAsync(Module(), "STATE");

        Assert.Equal(0, state.GetProperty("languages").GetArrayLength());
        Assert.Equal(0, state.GetProperty("workspaceLanguages").GetArrayLength());
        Assert.Equal(
            SessionLanguages.Table.Select(row => $"{row.Code}={row.Name}"),
            state.GetProperty("languageTable").EnumerateArray().Select(row => $"{row.GetProperty("code").GetString()}={row.GetProperty("name").GetString()}"));
    }

    /// <summary>Each repository the registry holds, what its sessions are asked to write in and where that was set; none is absent.</summary>
    [Fact]
    public async Task The_lines_answer_what_each_repository_resolves_to_and_from_where()
    {
        DriverConfig.Empty.WithLanguage("engine", "en").WithWorkspaceLanguage("aurora", "zh").Save(DriverConfigPath);
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService())));

        var languages = (await AnswerAsync(module, "LINES")).GetProperty("languages").EnumerateArray().ToList();

        Assert.Equal(["engine", "game", "tools"], languages.Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(["en", "zh", null], languages.Select(Code));
        Assert.Equal(["repository", "workspace", null], languages.Select(Source));
        Assert.Equal("Simplified Chinese (简体中文)", languages[1].GetProperty("name").GetString());
        Assert.Equal("aurora", languages[1].GetProperty("workspace").GetString());
        Assert.Equal("default", languages[2].GetProperty("workspace").GetString());
    }

    private static string? Code(JsonElement row) => row.TryGetProperty("language", out var code) ? code.GetString() : null;

    private static string? Source(JsonElement row) => row.TryGetProperty("source", out var source) ? source.GetString() : null;

    /// <summary>The service a driver reads its snapshot from, standing in: three registered repositories, one in no workspace.</summary>
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
