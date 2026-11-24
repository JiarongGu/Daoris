using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A repository's check budgets over the bridge: <c>SET_CHECK_BUDGETS</c> and <c>CHECK_BUDGETS</c> write and read the home's file
/// through <see cref="CheckBudgetsDoor"/>, the judge <c>daoris settings checks</c> calls, so a value the one refuses the other refuses.
/// </summary>
public sealed class DriverModuleCheckBudgetsTests : DriverModuleBridge
{
    [Fact]
    public async Task A_pin_is_set_through_the_route_and_read_with_its_derived_timeout_and_the_declared_gates_named()
    {
        var module = Module();

        var set = await AnswerAsync(module, "SET_CHECK_BUDGETS", new { repository = "engine", member = "pin", name = "merge", value = 33, why = "slow disk" });
        var read = await AnswerAsync(module, "CHECK_BUDGETS", new { repository = "engine", gates = new[] { "merge", "full" }, lane = "full" });

        Assert.Equal(33, set.GetProperty("gates").GetProperty("merge").GetProperty("budget").GetProperty("minutes").GetDouble());
        var merge = read.GetProperty("gates").GetProperty("merge");
        Assert.Equal(("pin", "slow disk"), (merge.GetProperty("budget").GetProperty("source").GetString(), merge.GetProperty("budget").GetProperty("why").GetString()));
        var full = read.GetProperty("gates").GetProperty("full").GetProperty("budget");
        Assert.Equal((10, "default"), (full.GetProperty("minutes").GetInt32(), full.GetProperty("source").GetString()));
        Assert.Equal(33, CheckBudgets.TimeoutOf(CheckBudgets.Of(Home, "engine").Gate("merge"), "merge").Minutes);
    }

    [Fact]
    public async Task The_route_refuses_in_the_judges_words_and_writes_nothing()
    {
        var module = Module();

        var route = await RefusalAsync(module, "SET_CHECK_BUDGETS", new { repository = "engine", member = "pin", name = "merge", value = 40 });
        var judge = Assert.Throws<DriverException>(() => CheckBudgetsDoor.Apply(Home, new(
            "engine", "pin", "merge", System.Text.Json.JsonDocument.Parse("40").RootElement.Clone(), false, null)));

        Assert.Contains("--why", route);
        Assert.Contains(judge.Message, route);
        Assert.False(File.Exists(CheckBudgets.PathOf(Home)));
    }

    [Fact]
    public async Task A_file_that_does_not_read_is_refused_by_name_for_the_read_and_the_press()
    {
        var module = Module();
        Directory.CreateDirectory(Path.GetDirectoryName(CheckBudgets.PathOf(Home))!);
        File.WriteAllText(CheckBudgets.PathOf(Home), "{ not json");

        Assert.Contains("check-budgets", await RefusalAsync(module, "CHECK_BUDGETS", new { repository = "engine" }));
        Assert.Contains("check-budgets", await RefusalAsync(module, "SET_CHECK_BUDGETS", new { repository = "engine", member = "devkit", value = "0.0.1" }));
        Assert.Equal("{ not json", File.ReadAllText(CheckBudgets.PathOf(Home)));
    }
}
