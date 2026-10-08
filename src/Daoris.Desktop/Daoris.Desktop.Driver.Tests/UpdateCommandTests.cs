using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// UPDATE1 (D139 §3, D50): `daoris-driver update`, the terminal's door to the banner's verbs — what is staged and how the
/// last swap ended, and <c>--when-idle</c>, <c>--now</c> and <c>--cancel</c> written to the same request the screen writes.
/// </summary>
public sealed class UpdateCommandTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 30, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-update-cmd-" + Guid.NewGuid().ToString("N")[..8]);

    private string Install => Path.Combine(_root, "install");

    private string Home => Path.Combine(Install, "data");

    public UpdateCommandTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(Install, "INSTALLED.md"), "# Daoris — installed desktop\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void With_nothing_staged_it_says_so_and_a_verb_is_refused_with_how_to_stage_writing_nothing()
    {
        var (code, said) = Run();
        Assert.Equal(0, code);
        Assert.Contains("nothing is staged", said);

        (code, said) = Run("--now");
        Assert.Equal(1, code);
        Assert.Contains("--stage", said);
        Assert.Null(InstallUpdate.Read(Home));
    }

    [Fact]
    public void With_a_build_staged_it_names_the_build_and_says_it_is_installed_when_idle_by_default()
    {
        Stage("b1");

        var (code, said) = Run();

        Assert.Equal(0, code);
        Assert.Contains("0.0.1 (abc1234)", said);
        Assert.Contains("b1", said);
        Assert.Contains("when idle", said);
    }

    [Theory]
    [InlineData("--when-idle", "when-idle")]
    [InlineData("--now", "now")]
    [InlineData("--cancel", "not-now")]
    public void A_verb_writes_the_request_for_the_staged_build_as_the_screen_does_and_logs_the_terminal_s_door(string verb, string mode)
    {
        Stage("b1");
        using var log = new MachineLog(Home, "driver", () => Now);

        var (code, said) = Run(log, verb);

        Assert.Equal(0, code);
        Assert.Equal(new UpdateRequest(mode, "b1", Now), InstallUpdate.Read(Home));
        Assert.Contains(mode == "not-now" ? "not now" : mode == "now" ? "now" : "when idle", said);
        var line = LogLines().Single(entry => entry["event"]?.GetValue<string>() == "update.requested");
        Assert.Equal(mode, line["data"]!["mode"]!.GetValue<string>());
        Assert.Equal("terminal", line["data"]!["door"]!.GetValue<string>());
        Assert.Equal("b1", line["data"]!["build"]!.GetValue<string>());
    }

    [Fact]
    public void Not_now_said_of_one_build_reads_as_when_idle_for_the_next()
    {
        Stage("b1");
        Run("--cancel");
        Assert.Contains("not now", Run().Said);

        Stage("b2");
        Assert.Contains("when idle", Run().Said);
    }

    [Fact]
    public void The_last_swap_is_said_in_its_own_words()
    {
        StagedBuild.WriteJournal(Install, new SwapRecord(
            SwapPhase.RolledBack, "b0", "0.0.1", "abc1234", Now, [], Reason: "exited",
            Detail: "the new build's application ended before it came up; the build before it was put back."));

        var said = Run().Said;

        Assert.Contains("rolled back", said);
        Assert.Contains("b0", said);
        Assert.Contains("ended before it came up", said);
    }

    /// <summary>SWAP2: what the swap found held, and for how long, said beside how it ended; nothing of holds when it met none.</summary>
    [Fact]
    public void The_last_swap_names_what_it_found_held_and_for_how_long()
    {
        StagedBuild.WriteJournal(Install, new SwapRecord(
            SwapPhase.RolledBack, "b0", "0.0.1", "abc1234", Now, [], Reason: "busy",
            Detail: "a file the swap had to move was held: update/staged/app for 120.0 s, and it did not give way; nothing was changed.",
            Holds: [new SwapHold("app", "update/previous/app", 14_200, 72, true), new SwapHold("update/staged/app", "app", 120_000, 601, false)]));

        var said = Run().Said;

        Assert.Contains("rolled back", said);
        Assert.Contains("  Held as it swapped: app for 14.2 s; update/staged/app for 120.0 s, and it did not give way.", said);

        StagedBuild.WriteJournal(Install, new SwapRecord(SwapPhase.Installed, "b1", "0.0.1", "abc1234", Now, [], Confirmed: true));
        Assert.DoesNotContain("Held", Run().Said);
    }

    [Fact]
    public void A_home_that_is_no_install_s_data_is_pointed_at_one_with_install_or_the_request_holds_for_whatever_is_staged()
    {
        var scratch = Path.Combine(_root, "scratch-home");
        Directory.CreateDirectory(scratch);
        Stage("b1");

        var status = UpdateCommand.Run([], scratch, () => Now, null, out var said);
        Assert.Equal(2, status);
        Assert.Contains("--install", said);

        Assert.Equal(0, UpdateCommand.Run(["--now", "--install", Install], scratch, () => Now, null, out said));
        Assert.Equal(new UpdateRequest(UpdateMode.Now, "b1", Now), InstallUpdate.Read(scratch));

        Assert.Equal(0, UpdateCommand.Run(["--cancel"], scratch, () => Now, null, out said));
        Assert.Equal(new UpdateRequest(UpdateMode.NotNow, null, Now), InstallUpdate.Read(scratch));
        Assert.Contains("whatever is staged", said);
    }

    [Theory]
    [InlineData("--later")]
    [InlineData("--now", "--cancel")]
    [InlineData("--install")]
    public void A_word_it_does_not_answer_is_the_usage_and_exit_2(params string[] args)
    {
        Stage("b1");

        var (code, said) = Run(args);

        Assert.Equal(2, code);
        Assert.Contains("daoris-driver update", said);
        Assert.Null(InstallUpdate.Read(Home));
    }

    private (int Code, string Said) Run(params string[] args) => Run(null, args);

    private (int Code, string Said) Run(MachineLog? log, params string[] args)
    {
        var code = UpdateCommand.Run(args, Home, () => Now, log, out var said);
        return (code, said);
    }

    /// <summary>The log's lines, read beside the writer that still holds its file, as a person tails it.</summary>
    private IEnumerable<JsonObject> LogLines() =>
        Directory.EnumerateFiles(Path.Combine(Home, MachineLog.Folder), "*.jsonl")
            .SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonNode.Parse(line)!.AsObject());

    private void Stage(string id)
    {
        var staged = Path.Combine(Install, "update", "staged");
        if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);
        Directory.CreateDirectory(staged);
        File.WriteAllText(Path.Combine(staged, "build.json"), new JsonObject
        {
            ["schema"] = 1,
            ["id"] = id,
            ["version"] = "0.0.1",
            ["commit"] = "abc1234",
            ["at"] = "2026-10-03T12:00:00Z",
            ["files"] = new JsonArray(),
        }.ToJsonString());
    }
}
