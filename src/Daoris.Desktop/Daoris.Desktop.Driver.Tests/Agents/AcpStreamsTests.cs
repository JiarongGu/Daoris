using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the protocol door says of each child, as the readings <see cref="AcpStreams"/> writes to the
/// sink it is handed. The recording sink here names no lineage type: it is all <see cref="AcpStreams"/> sees.
/// </summary>
public sealed class AcpStreamsTests
{
    [Fact]
    public void An_announcement_is_said_once_in_its_wire_s_words_with_its_place()
    {
        var told = new Told();
        var streams = Streams(told);
        var task = new string('t', 210) + "\nthe rest of the task";

        Take(streams, "s-1", Spawned("a1", new string('n', 130), task));
        Take(streams, "s-1", Spawned("a1", "again", "again"));
        Take(streams, "a1", Spawned("a2", "Nested", "look"));
        Take(streams, "x-9", Spawned("a3", null, null));
        Take(streams, null, Spawned("a4", "Rooted", "  \nsecond"));

        Assert.Equal(
            [
                new WireReading.ChildSeen("a1", ChildSignal.Announced) { Depth = 1, Name = new string('n', 130), Task = task },
                new WireReading.ChildSeen("a2", ChildSignal.Announced) { Parent = "a1", Name = "Nested", Task = "look" },
                new WireReading.ChildSeen("a3", ChildSignal.Announced),
                new WireReading.ChildSeen("a4", ChildSignal.Announced) { Depth = 1, Name = "Rooted", Task = "  \nsecond" },
            ],
            told.Spawns);
    }

    [Fact]
    public void An_end_is_told_only_from_the_wire_s_state_update_with_the_word_it_said()
    {
        var told = new Told();
        var streams = Streams(told);
        Take(streams, "s-1", Spawned("a1", "One", "one"));
        Take(streams, "s-1", Spawned("a2", "Two", "two"));

        Take(streams, "s-1", Ended("a1", "completed"));
        Take(streams, "s-1", Ended("a1", "failed"));
        Take(streams, "s-1", Ended("a2", null));
        Assert.False(Taken(streams, "s-1", Ended("a9", "completed")));

        Assert.Equal([("a1", (string?)"completed"), ("a2", null)], told.Ends);
    }

    [Fact]
    public async Task The_session_s_end_tells_the_lineage_no_end_of_a_child_still_open()
    {
        var told = new Told();
        var streams = Streams(told);
        Take(streams, "s-1", Spawned("a1", "Long reader", "read everything"));

        await streams.EndAllAsync();

        Assert.Single(told.Spawns);
        Assert.Empty(told.Ends);
    }

    private static AcpStreams Streams(IReadingSink children) => new(
        new SessionStreams(new SessionOutput(), "d-1"), new AcpConsole(_ => { }, AcpConsole.Quiet), _ => { }, AcpConsole.Quiet,
        children: children);

    private static void Take(AcpStreams streams, string? from, string update) => Assert.True(Taken(streams, from, update));

    private static bool Taken(AcpStreams streams, string? from, string update)
    {
        using var parsed = JsonDocument.Parse(update);
        return streams.Take(from, "s-1", parsed.RootElement.Clone());
    }

    private static string Spawned(string id, string? name, string? task) => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["sessionUpdate"] = "subagent_spawned", ["subagentSessionId"] = id, ["name"] = name, ["task"] = task,
    });

    private static string Ended(string id, string? state) => state is null
        ? JsonSerializer.Serialize(new { sessionUpdate = "subagent_state_update", subagentSessionId = id })
        : JsonSerializer.Serialize(new { sessionUpdate = "subagent_state_update", subagentSessionId = id, state });

    private sealed class Told : IReadingSink
    {
        public List<WireReading.ChildSeen> Spawns { get; } = [];
        public List<(string, string?)> Ends { get; } = [];
        public bool Watched => false;

        public void Read(WireReading reading)
        {
            if (reading is WireReading.ChildSeen seen) Spawns.Add(seen);
            else if (reading is WireReading.ChildEnded { Signal: ChildSignal.Announced } ended) Ends.Add((ended.Child, ended.Word));
            else throw new InvalidOperationException($"the streams said {reading.GetType().Name}, which is no child's");
        }
    }
}
