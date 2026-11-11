using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A command round's frame and answer, whole: the frame carries the run's id, the arguments by name and typed, the place, the viewer's
/// language and, for an agent, the session; the answer may say what the view does next (<c>then</c>) and hand an agent structured
/// <c>data</c>. The terminal's words become the arguments by name, flags or in declared order.
/// </summary>
public sealed class CommandWireTests
{
    private static readonly IReadOnlyList<PluginCommandArg> Declared =
    [
        new("stage", "text", AuthoredText.Of("Stage"), Required: true, []),
        new("count", "number", AuthoredText.Of("Count"), Required: false, []),
        new("dry", "boolean", AuthoredText.Of("Dry run"), Required: false, []),
        new("kind", "choice", AuthoredText.Of("Kind"), Required: false, ["fast", "slow"]),
    ];

    [Fact]
    public void The_frame_carries_the_run_the_arguments_by_name_the_place_and_the_language()
    {
        var call = new CommandCall("start-stage", CommandCall.NewRun(), new Dictionary<string, object?> { ["stage"] = "qa", ["count"] = 2.0 },
            new Dictionary<string, string>(), CommandWire.Agent, new PluginPlace(Repository: "engine", Workspace: "home"), "zh", Session: "s1a2b3c4");

        var frame = JsonSerializer.SerializeToElement(CommandWire.Frame(call));

        Assert.Matches("^[0-9a-f]{32}$", frame.GetProperty("run").GetString()!);
        Assert.Equal("qa", frame.GetProperty("args").GetProperty("stage").GetString());
        Assert.Equal(2, frame.GetProperty("args").GetProperty("count").GetDouble());
        Assert.Equal("repository", frame.GetProperty("place").GetProperty("kind").GetString());
        Assert.Equal("engine", frame.GetProperty("place").GetProperty("id").GetString());
        Assert.Equal("zh", frame.GetProperty("language").GetString());
        Assert.Equal("agent", frame.GetProperty("from").GetString());
        Assert.Equal("s1a2b3c4", frame.GetProperty("session").GetString());

        var machine = JsonSerializer.SerializeToElement(CommandWire.Frame(call with { Place = PluginPlace.Machine, From = CommandWire.Terminal, Session = null }));
        Assert.Equal("machine", machine.GetProperty("place").GetString());
        Assert.False(machine.TryGetProperty("session", out _));
    }

    [Theory]
    [InlineData("""{ "then": "back" }""", null)]
    [InlineData("""{ "then": { "go": { "view": "pull-requests", "args": { "id": "7" } } } }""", null)]
    [InlineData("""{ "then": "away" }""", "`then` is")]
    [InlineData("""{ "then": { "go": { "view": "Not A View" } } }""", "`then` is")]
    [InlineData("""{ "data": { "open": 3 } }""", null)]
    [InlineData("""{ "data": [1, 2] }""", "`data` is not an object")]
    public void Then_and_data_are_read_by_their_shape(string answer, string? refused)
    {
        var read = () => CommandWire.Read("acme-desk", JsonDocument.Parse(answer).RootElement);
        if (refused is null) Assert.Equal(0, read().Exit);
        else Assert.Contains(refused, Assert.Throws<DriverException>(read).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Data_is_bounded_at_32_KiB()
    {
        var large = JsonSerializer.Serialize(new { data = new { text = new string('x', CommandWire.DataBytes) } });

        Assert.Contains("32 KiB", Assert.Throws<DriverException>(() => CommandWire.Read("acme-desk", JsonDocument.Parse(large).RootElement)).Message, StringComparison.Ordinal);
        var kept = CommandWire.Read("acme-desk", JsonDocument.Parse("""{ "data": { "open": 3 }, "then": "stay" }""").RootElement);
        Assert.Equal(3, kept.Data!.Value.GetProperty("open").GetInt32());
        Assert.Equal("stay", kept.Then!.Value.GetString());
    }

    [Fact]
    public void The_terminal_s_words_are_the_arguments_by_flag_or_in_declared_order_each_typed()
    {
        var args = CommandArgs.FromWords("start-stage", Declared, ["qa", "--count", "3", "--dry", "--kind", "slow"]);

        Assert.Equal("qa", args.Values["stage"]);
        Assert.Equal(3.0, args.Values["count"]);
        Assert.Equal(true, args.Values["dry"]);
        Assert.Equal("slow", args.Values["kind"]);
        Assert.Empty(args.Missing);
        Assert.Equal(["stage"], CommandArgs.FromWords("start-stage", Declared, []).Missing.Select(each => each.Name));
        Assert.Throws<DriverException>(() => CommandArgs.FromWords("start-stage", Declared, ["qa", "--count", "many"]));
        Assert.Throws<DriverException>(() => CommandArgs.FromWords("start-stage", Declared, ["qa", "--kind", "medium"]));
        Assert.Throws<DriverException>(() => CommandArgs.FromWords("start-stage", Declared, ["qa", "--colour", "red"]));
    }
}
