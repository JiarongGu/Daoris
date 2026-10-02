using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A repository's standing answer over the bridge (KNOWUSE1b, D135 §3): the screen's half of `daoris driver standing`, over
/// the same file, answered in the state as rows so no key policy on the bridge respells a repository's name.
/// </summary>
public sealed class DriverModuleStandingTests : DriverModuleBridge
{
    [Fact]
    public async Task Setting_a_standing_answer_writes_the_same_file_the_terminal_edits()
    {
        var module = Module();

        var state = await AnswerAsync(module, "SET_STANDING", new { repository = "Work-App", says = "  dev writes allowed; prod only on a yes  " });

        var kept = DriverConfig.Load(DriverConfigPath).StandingFor("work-app");
        Assert.Equal("dev writes allowed; prod only on a yes", kept!.Says);
        Assert.NotNull(kept.At);
        var row = Assert.Single(state.GetProperty("standing").EnumerateArray());
        Assert.Equal("Work-App", row.GetProperty("repository").GetString());
        Assert.Equal("dev writes allowed; prod only on a yes", row.GetProperty("says").GetString());
        Assert.Equal(JsonValueKind.String, row.GetProperty("at").ValueKind);

        var replaced = await AnswerAsync(module, "SET_STANDING", new { repository = "work-app", says = "dev only" });
        Assert.Equal("Work-App", replaced.GetProperty("standing")[0].GetProperty("repository").GetString());
        Assert.Equal("dev only", replaced.GetProperty("standing")[0].GetProperty("says").GetString());

        var cleared = await AnswerAsync(module, "SET_STANDING", new { repository = "work-app" });
        Assert.Equal(0, cleared.GetProperty("standing").GetArrayLength());
        Assert.Null(DriverConfig.Load(DriverConfigPath).StandingFor("work-app"));
    }

    [Fact]
    public async Task Blank_words_or_words_past_the_bound_are_refused_in_the_drivers_sentence_and_nothing_is_written()
    {
        var blank = await RefusalAsync(Module(), "SET_STANDING", new { repository = "app", says = "   " });
        var past = await RefusalAsync(Module(), "SET_STANDING", new { repository = "app", says = new string('x', DriverConfig.StandingLimit + 1) });

        Assert.Contains(DriverConfig.StandingRefusal, blank);
        Assert.Contains(DriverConfig.StandingRefusal, past);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Standing.Count > 0);
    }

    [Fact]
    public async Task A_machine_that_kept_none_answers_none()
    {
        var state = await AnswerAsync(Module(), "STATE");

        Assert.Equal(0, state.GetProperty("standing").GetArrayLength());
    }
}
