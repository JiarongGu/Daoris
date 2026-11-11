using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The record of the terminal's tier, kept across a restart in <c>data/terminal-acts.json</c>: each change of the tier with who made it
/// and from which door, and each press a terminal made under <c>act</c> with what it named, when it was admitted and how it ended. A
/// press still running when Daoris stopped is read back interrupted; the verb acting alone records its own change.
/// </summary>
public sealed class TerminalActsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "terminal-acts", Guid.NewGuid().ToString("N")[..8]);
    private readonly Clock _clock = new(DateTimeOffset.Parse("2026-11-11T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    public TerminalActsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string Path0 => DaorisHome.Entry(_home, HomeEntry.TerminalActs);

    [Fact]
    public void It_is_a_data_file_of_its_own_after_the_salvage()
    {
        Assert.Same(HomeEntry.TerminalActs, HomeEntry.Named("terminal-acts"));
        Assert.Equal((HomeCategory.Data, "data/terminal-acts.json"), (HomeEntry.TerminalActs.Category, HomeEntry.TerminalActs.New));
        var table = HomeEntry.Table.ToList();
        Assert.Equal(table.IndexOf(HomeEntry.Salvage) + 1, table.IndexOf(HomeEntry.TerminalActs));
    }

    [Fact]
    public void A_change_and_a_press_are_kept_newest_first_and_read_back_after_a_restart()
    {
        var acts = new TerminalActs(Path0, _clock);
        acts.Changed(TerminalTiers.Act, TerminalTierBy.AskDaoris, "h1");
        _clock.Now += TimeSpan.FromMinutes(1);
        var id = acts.Began("DRIVER", "WORK_PAUSE", "quest", "q1");
        acts.Commanded(id, "daoris quest pause q1");
        acts.Ended(id, TerminalActs.Done, code: null);

        var read = new TerminalActs(Path0, _clock).Now();

        var change = Assert.Single(read.Changes);
        Assert.Equal((TerminalTiers.Act, TerminalTierBy.AskDaoris, "h1"), (change.Tier, change.By, change.Session));
        var press = Assert.Single(read.Presses);
        Assert.Equal(
            ("WORK_PAUSE", "quest", "q1", "daoris quest pause q1", TerminalActs.Done, _clock.Now),
            (press.Route, press.Scope, press.Target, press.Command, press.Outcome, press.At));
    }

    [Fact]
    public void A_press_still_running_when_Daoris_stopped_is_read_back_interrupted_and_a_failure_keeps_its_code()
    {
        var acts = new TerminalActs(Path0, _clock);
        acts.Began("DRIVER", "SET_HOLD", "repository", "engine");
        var failed = acts.Began("DRIVER", "WORK_PAUSE", "quest", "q2");
        acts.Ended(failed, TerminalActs.Failed, "driver-refused");

        var read = new TerminalActs(Path0, _clock).Now().Presses;

        Assert.Equal(
            [(TerminalActs.Failed, "driver-refused"), (TerminalActs.Interrupted, null)],
            read.Select(each => (each.Outcome, each.Code)).ToArray());
        Assert.Contains("\"interrupted\"", File.ReadAllText(Path0));
    }

    [Fact]
    public void The_newest_are_kept_and_a_file_that_does_not_read_is_an_empty_record()
    {
        var acts = new TerminalActs(Path0, _clock);
        for (var each = 0; each < TerminalActs.PressesKept + 5; each++) acts.Began("DRIVER", "WORK_PAUSE", "quest", $"q{each}");
        for (var each = 0; each < TerminalActs.ChangesKept + 3; each++) acts.Changed(each % 2 == 0 ? TerminalTiers.Act : TerminalTiers.Ask, TerminalTierBy.Window, null);

        var now = acts.Now();
        Assert.Equal((TerminalActs.PressesKept, TerminalActs.ChangesKept), (now.Presses.Count, now.Changes.Count));
        Assert.Equal($"q{TerminalActs.PressesKept + 4}", now.Presses[0].Target);

        File.WriteAllText(Path0, "{ not json");
        Assert.Empty(new TerminalActs(Path0, _clock).Now().Presses);
    }

    [Fact]
    public async Task The_verb_acting_alone_records_its_change_back_to_ask_as_the_terminal_s_alone()
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(config)!);
        File.WriteAllText(config, """{ "terminal": "act" }""");
        var world = new DriverChoicesWorld
        {
            Verbs = DriverChoiceVerbs.All,
            Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = new StringWriter(), Error = new StringWriter(), Verb = "daoris" },
            ConfigPath = config,
        };

        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["terminal", "ask"], world, CancellationToken.None));
        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["terminal", "ask"], world, CancellationToken.None));

        var change = Assert.Single(new TerminalActs(Path0, _clock).Now().Changes);
        Assert.Equal((TerminalTiers.Ask, TerminalTierBy.Alone), (change.Tier, change.By));
        using var file = JsonDocument.Parse(File.ReadAllText(Path0));
        Assert.Equal("alone", file.RootElement.GetProperty("changes")[0].GetProperty("by").GetString());
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
