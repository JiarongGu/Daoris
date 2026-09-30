using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Reading and writing across repositories over the bridge (`DriverModule.Across.cs`, READ1, D107): the
/// screen's half of `daoris driver across`, over the same `driver.json`.
/// </summary>
public sealed class DriverModuleAcrossTests : DriverModuleBridge
{
    /// <summary>
    /// A repository's reading or a workspace's, set and cleared as `daoris driver across … read` does it. The
    /// names ride as rows, not as an object's keys, so no key policy on the bridge can respell one.
    /// </summary>
    [Fact]
    public async Task Setting_reading_across_writes_the_same_file_the_terminal_edits()
    {
        var module = Module();
        await AnswerAsync(module, "SET_READ_ACROSS", new { workspace = "aurora", read = false });
        var state = await AnswerAsync(module, "SET_READ_ACROSS", new { repository = "Engine", read = true });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.True(config.ReadAcross["engine"]);
        Assert.False(config.WorkspaceReadAcross["aurora"]);
        Assert.Equal("Engine", state.GetProperty("readAcross")[0].GetProperty("repository").GetString());
        Assert.True(state.GetProperty("readAcross")[0].GetProperty("read").GetBoolean());
        Assert.Equal("aurora", state.GetProperty("workspaceReadAcross")[0].GetProperty("workspace").GetString());
        Assert.False(state.GetProperty("workspaceReadAcross")[0].GetProperty("read").GetBoolean());

        // No `read` is the clear: the repository takes its workspace's again.
        var cleared = await AnswerAsync(module, "SET_READ_ACROSS", new { repository = "engine" });
        Assert.Equal(0, cleared.GetProperty("readAcross").GetArrayLength());
    }

    /// <summary>A relationship declared and taken back, as `daoris driver across … write-to` does it.</summary>
    [Fact]
    public async Task A_relationship_is_declared_and_taken_back_over_the_same_file()
    {
        var module = Module();
        await AnswerAsync(module, "SET_WRITE_ACROSS", new { repository = "plugins", to = "engine", allow = true });
        var state = await AnswerAsync(module, "SET_WRITE_ACROSS", new { repository = "plugins", to = "game", allow = true });

        Assert.Equal(["engine", "game"], DriverConfig.Load(DriverConfigPath).WriteAcross["plugins"]);
        var row = state.GetProperty("writeAcross")[0];
        Assert.Equal("plugins", row.GetProperty("repository").GetString());
        Assert.Equal(["engine", "game"], row.GetProperty("to").EnumerateArray().Select(e => e.GetString()));

        await AnswerAsync(module, "SET_WRITE_ACROSS", new { repository = "plugins", to = "engine", allow = false });
        var none = await AnswerAsync(module, "SET_WRITE_ACROSS", new { repository = "plugins", to = "game", allow = false });
        Assert.Equal(0, none.GetProperty("writeAcross").GetArrayLength());
    }

    [Fact]
    public async Task A_setting_with_no_owner_or_a_repository_writing_into_itself_is_refused_in_a_sentence()
    {
        var neither = await RefusalAsync(Module(), "SET_READ_ACROSS", new { read = false });
        Assert.Contains("a `repository` or a `workspace`", neither);
        var both = await RefusalAsync(Module(), "SET_READ_ACROSS", new { repository = "engine", workspace = "aurora", read = false });
        Assert.Contains("a `repository` or a `workspace`", both);

        var self = await RefusalAsync(Module(), "SET_WRITE_ACROSS", new { repository = "plugins", to = "Plugins", allow = true });
        Assert.Contains("writes in its own tree already", self);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).WriteAcross.Count > 0);
    }

    /// <summary>How each repository stands is read off the registry, so it waits for the driver like the lines do.</summary>
    [Fact]
    public async Task Asking_how_each_repository_stands_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "ACROSS", new { });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
    }
}
