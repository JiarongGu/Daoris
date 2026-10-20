using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Which checks a repository's sessions run under Current, over the bridge (FABRIC-PIPELINE1b, D192 point 4): <c>SET_SESSION_CHECKS</c>,
/// Setup's half of <c>daoris-driver checks</c>, over the same key in the same file (D50); the state answering each repository's rule as
/// a row, so no key policy on the bridge respells a repository's name; a value that is no choice refused in
/// <see cref="SessionChecks"/>'s words with nothing written. Edits to <c>driver.json</c> only.
/// </summary>
public sealed class DriverModuleSessionChecksTests : DriverModuleBridge
{
    /// <summary>A rule is kept under the spelling first written, the state answers it as a row, and null clears it.</summary>
    [Fact]
    public async Task A_rule_is_kept_and_answered_as_a_row_and_null_clears_it()
    {
        var module = Module();
        Assert.Empty((await AnswerAsync(module, "STATE")).GetProperty("sessionChecks").EnumerateArray());

        await AnswerAsync(module, "SET_SESSION_CHECKS", new { repository = " Work-App ", checks = "LIGHT" });
        var state = await AnswerAsync(module, "SET_SESSION_CHECKS", new { repository = "shop", checks = "heavy-at-end" });

        var rows = state.GetProperty("sessionChecks").EnumerateArray().ToList();
        Assert.Equal(["Work-App", "shop"], rows.Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(["light", "heavy-at-end"], rows.Select(row => row.GetProperty("checks").GetString()));
        Assert.Equal(SessionChecks.Light, SessionChecks.For(DriverConfig.Load(DriverConfigPath), "work-app"));

        var cleared = await AnswerAsync(module, "SET_SESSION_CHECKS", new { repository = "WORK-APP", checks = (string?)null });
        Assert.Equal(["shop"], cleared.GetProperty("sessionChecks").EnumerateArray().Select(row => row.GetProperty("repository").GetString()));
        var none = await AnswerAsync(module, "SET_SESSION_CHECKS", new { repository = "shop" });
        Assert.Empty(none.GetProperty("sessionChecks").EnumerateArray());
        Assert.Empty(DriverConfig.Load(DriverConfigPath).SessionChecks);
    }

    /// <summary>A value that is no choice, or no repository, is refused in the door's words, and the file is as it was.</summary>
    [Fact]
    public async Task A_value_that_is_no_choice_is_refused_in_the_doors_words_and_nothing_is_written()
    {
        var module = Module();
        await AnswerAsync(module, "SET_SESSION_CHECKS", new { repository = "engine", checks = "light" });
        var before = File.ReadAllText(DriverConfigPath);

        var heavy = await RefusalAsync(module, "SET_SESSION_CHECKS", new { repository = "engine", checks = "heavy" });
        var nobody = await RefusalAsync(module, "SET_SESSION_CHECKS", new { repository = " ", checks = "light" });

        Assert.Contains("`heavy` is not a session's checks", heavy);
        Assert.Contains("name one", nobody);
        Assert.Equal(before, File.ReadAllText(DriverConfigPath));
    }

    /// <summary>🔴 The route and the terminal agree (D50): what one sets the other reads, through the same key.</summary>
    [Fact]
    public async Task The_route_and_the_terminal_set_the_same_key()
    {
        var module = Module();
        string Terminal(params string[] args)
        {
            var output = new StringWriter();
            Assert.Equal(0, SessionChecksCommand.Run(SessionChecksCommand.Read(args, out _)!, DriverConfigPath, output));
            return output.ToString();
        }

        Terminal("shop", "heavy-at-end");
        var row = Assert.Single((await AnswerAsync(module, "STATE")).GetProperty("sessionChecks").EnumerateArray());
        Assert.Equal(("shop", "heavy-at-end"), (row.GetProperty("repository").GetString(), row.GetProperty("checks").GetString()));

        await AnswerAsync(module, "SET_SESSION_CHECKS", new { repository = "engine", checks = "light" });
        Assert.Contains("`engine`'s sessions run light", Terminal("engine"));
    }
}
