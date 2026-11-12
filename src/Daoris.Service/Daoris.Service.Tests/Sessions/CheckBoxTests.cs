using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The ask for a check is the contract between this box and the driver's broker, which share no code: the file as written here is the file
/// the driver's <c>CheckFiles</c> reads (its suite holds the same shape from its side).
/// </summary>
public sealed class CheckBoxTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "check-box-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static Session Worker(string id = "s1") =>
        new(id, "q1", "reports", "claude-code", SessionState.Working, null, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    [Fact]
    public void A_working_sessions_ask_is_written_as_the_file_the_driver_reads()
    {
        var box = new CheckBox(_home);

        var (gate, _) = box.Ask(Worker(), "s1", "tests", null, At);
        var (command, said) = box.Ask(Worker(), "s1", null, ["dotnet", "test", "--filter", "X"], At);

        using var byGate = JsonDocument.Parse(File.ReadAllText(CheckBox.PathOf(_home, gate!)));
        Assert.Equal(("s1", "tests", "agent"), (byGate.RootElement.GetProperty("session").GetString(), byGate.RootElement.GetProperty("gate").GetString(), byGate.RootElement.GetProperty("by").GetString()));
        Assert.False(byGate.RootElement.TryGetProperty("command", out _));
        using var byCommand = JsonDocument.Parse(File.ReadAllText(CheckBox.PathOf(_home, command!)));
        Assert.Equal(["dotnet", "test", "--filter", "X"], byCommand.RootElement.GetProperty("command").EnumerateArray().Select(word => word.GetString()));
        Assert.Contains("end your turn", said, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_ask_that_names_no_session_a_helper_or_both_a_gate_and_a_command_is_refused_and_writes_nothing()
    {
        var box = new CheckBox(_home);
        var helper = Worker("h1") with { Serves = "s1" };

        Assert.Null(box.Ask(null, "s9", "tests", null, At).Id);
        Assert.Null(box.Ask(Worker(), null, "tests", null, At).Id);
        Assert.Null(box.Ask(helper, "h1", "tests", null, At).Id);
        Assert.Null(box.Ask(Worker(), "s1", "tests", ["dotnet", "test"], At).Id);
        Assert.Null(box.Ask(Worker(), "s1", "two words", null, At).Id);
        Assert.Null(new CheckBox(null).Ask(Worker(), "s1", "tests", null, At).Id);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Checks)));
    }
}
