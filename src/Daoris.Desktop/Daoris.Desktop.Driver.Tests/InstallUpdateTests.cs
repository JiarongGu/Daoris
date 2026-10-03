using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// UPDATE1 (D139 §2, §3): the request both doors write under the home, the mode that holds for what is staged, the
/// install a home belongs to, and the config a draining look plans with.
/// </summary>
public sealed class InstallUpdateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-update-" + Guid.NewGuid().ToString("N")[..8]);

    public InstallUpdateTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_request_written_reads_back_as_written_LF_and_under_the_home()
    {
        var at = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        InstallUpdate.Write(_root, new UpdateRequest(UpdateMode.NotNow, "20261003-abc", at));

        Assert.Equal(new UpdateRequest(UpdateMode.NotNow, "20261003-abc", at), InstallUpdate.Read(_root));
        var text = File.ReadAllText(Path.Combine(_root, "update.json"));
        Assert.DoesNotContain("\r", text);
        Assert.Contains("\"mode\": \"not-now\"", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("""{ "mode": "later" }""")]
    [InlineData("""{ "mode": 3 }""")]
    public void A_request_that_is_absent_or_does_not_read_is_none_so_the_default_holds(string? text)
    {
        if (text is not null) File.WriteAllText(Path.Combine(_root, "update.json"), text);

        Assert.Null(InstallUpdate.Read(_root));
        Assert.Equal(UpdateMode.WhenIdle, InstallUpdate.ModeFor(InstallUpdate.Read(_root), "b1"));
    }

    [Fact]
    public void A_mode_this_build_does_not_know_is_refused_and_nothing_is_written()
    {
        var refused = Assert.Throws<ArgumentException>(() => InstallUpdate.Write(_root, new UpdateRequest("later", null, null)));

        Assert.Contains("when-idle, now, not-now", refused.Message);
        Assert.False(File.Exists(Path.Combine(_root, "update.json")));
    }

    /// <summary>The mode for the build staged now: a word about another build holds for none, so a newer stage drains again.</summary>
    [Theory]
    [InlineData(null, null, "b1", "when-idle")]
    [InlineData("not-now", "b1", "b1", "not-now")]
    [InlineData("not-now", "b1", "b2", "when-idle")]
    [InlineData("not-now", null, "b2", "not-now")]
    [InlineData("now", "b1", "b1", "now")]
    [InlineData("now", "b1", "b2", "when-idle")]
    [InlineData("now", null, "b2", "now")]
    [InlineData("when-idle", "b1", "b1", "when-idle")]
    public void The_mode_that_holds_is_the_request_s_only_for_the_build_it_was_said_of(
        string? mode, string? build, string staged, string expected)
    {
        var request = mode is null ? null : new UpdateRequest(mode, build, null);

        Assert.Equal(expected, InstallUpdate.ModeFor(request, staged));
    }

    [Fact]
    public void Cleared_the_request_is_gone_and_clearing_none_is_no_error()
    {
        InstallUpdate.Write(_root, new UpdateRequest(UpdateMode.Now, "b1", null));
        InstallUpdate.Clear(_root);
        InstallUpdate.Clear(_root);

        Assert.Null(InstallUpdate.Read(_root));
    }

    [Fact]
    public void A_home_belongs_to_the_install_above_it_only_when_it_is_that_install_s_data()
    {
        var install = Path.Combine(_root, "install");
        Directory.CreateDirectory(Path.Combine(install, "data"));
        File.WriteAllText(Path.Combine(install, "INSTALLED.md"), "# Daoris — installed desktop\n");
        Directory.CreateDirectory(Path.Combine(_root, "scratch-home"));

        Assert.Equal(Path.GetFullPath(install), InstallUpdate.InstallOf(Path.Combine(install, "data")));
        Assert.Null(InstallUpdate.InstallOf(Path.Combine(_root, "scratch-home")));
        Assert.Null(InstallUpdate.InstallOf(Path.Combine(_root, "data")));
    }

    [Fact]
    public void A_draining_look_holds_every_drivable_repository_and_answers_no_ask_and_keeps_the_cap()
    {
        var config = DriverConfig.Empty with { Drivable = ["engine", "tools"], Holds = ["Tools"], Cap = 4 };
        config = config.WithIntake("claude-code");

        var drained = InstallUpdate.Drained(config);

        Assert.Equal(["Tools", "engine"], drained.Holds);
        Assert.Null(drained.IntakeAdapter);
        Assert.Equal(4, drained.Cap);
        Assert.Equal(config.Drivable, drained.Drivable);
    }

    [Fact]
    public void Only_a_hold_the_drain_made_is_said_as_the_update_s()
    {
        var config = DriverConfig.Empty with { Drivable = ["engine", "tools", "web"], Holds = ["tools"] };
        Consideration Of(string id, string to, StartVerdict verdict, string reason) => new(Quest(id, to), verdict, reason);
        var considered = new[]
        {
            Of("q1", "engine", StartVerdict.Held, "`engine` is held by the person."),
            Of("q2", "tools", StartVerdict.Held, "`tools` is held by the person."),
            Of("q3", "web", StartVerdict.Paused, "you paused `#q3`."),
            Of("q4", "other", StartVerdict.NotDrivable, "`other` has not been opted into driving on this machine."),
        };

        var said = InstallUpdate.HeldFor(considered, config);

        Assert.Equal(StartVerdict.Blocked, said[0].Verdict);
        Assert.Equal(InstallUpdate.HoldReason, said[0].Reason);
        Assert.True(InstallUpdate.IsHeldForUpdate(said[0]));
        Assert.Equal(considered[1..], said.Skip(1));
        Assert.All(said.Skip(1), consideration => Assert.False(InstallUpdate.IsHeldForUpdate(consideration)));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(2, 3, false)]
    public void The_work_allows_the_update_only_when_no_driven_session_runs_and_no_turn_is_in_flight(int driven, int turns, bool idle) =>
        Assert.Equal(idle, InstallUpdate.Idle(driven, turns));

    private static QuestView Quest(string id, string to) => new(id, "game", to, "A quest", "", "Open");
}
