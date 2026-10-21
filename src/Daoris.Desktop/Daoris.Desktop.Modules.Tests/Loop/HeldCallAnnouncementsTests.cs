using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A tool call a session holds for the person's answer is announced as a park is (NOTIFY2d, D196, the notification design §5, §9):
/// once, where the attention slice first sees its row, on <c>SESSION_ATTENTION</c> in the shape the shell's notifier and the page's
/// toast read, and only while the notify switch is on. Seen with the switch off, it is seen: turned back on, it is not said late.
/// </summary>
public sealed class HeldCallAnnouncementsTests : DriverModuleBridge
{
    private bool _notify = true;

    private JsonElement[] Said() => [.. Raised
        .Where(message => message.Type == "SESSION_ATTENTION")
        .Select(message => JsonSerializer.SerializeToElement(message.Payload))];

    private static JsonObject Session(string id, string repository) =>
        JsonNode.Parse($$"""{"id":"{{id}}","state":"working","repository":"{{repository}}","workspace":"default","updated":"2026-10-22T09:00:00Z"}""")!.AsObject();

    private static JsonObject Held(string session, string request, string? title = "npm test") =>
        JsonNode.Parse($$"""
            {"session":"{{session}}","request":"{{request}}","call":"c{{request}}",{{(title is null ? "" : $"\"title\":\"{title}\",")}}"kind":"execute",
             "since":"2026-10-22T09:30:00Z","until":"2026-10-22T09:45:00Z","choices":[{"id":"a","name":"Allow","kind":"allow_once"}]}
            """)!.AsObject();

    private (StateSlice Slice, Func<AttentionFacts> Facts, Action<AttentionFacts> Set) Slice(params JsonObject[] held)
    {
        var facts = new AttentionFacts { Sessions = [Session("s1a2b3c4", "engine"), Session("s2b3c4d5", "game")], Permissions = [.. held] };
        var slice = AttentionStateSlice.Of(() => facts, Bus, [], held: new HeldCallAnnouncements(Bus, () => _notify));
        return (slice, () => facts, now => facts = now);
    }

    [Fact]
    public async Task A_held_call_is_announced_once_where_its_row_is_first_seen()
    {
        var (slice, facts, set) = Slice(Held("s1a2b3c4", "900"));

        await slice.JudgeAsync();
        await slice.JudgeAsync();

        var told = Assert.Single(Said());
        Assert.Equal(
            (HeldCallAnnouncements.AttentionKind, "s1a2b3c4#900", "s1a2b3c4", "engine", "npm test"),
            (told.GetProperty("Kind").GetString(), told.GetProperty("Id").GetString(), told.GetProperty("Session").GetString(),
                told.GetProperty("Repository").GetString(), told.GetProperty("Detail").GetString()));
        // Its English headline for a reader that reads it, the English catalogue's (`SessionBalloon`).
        Assert.Equal("engine: a session holds a tool call for you", told.GetProperty("Headline").GetString());

        // A second held beside it is said alone; the first, still held, is not said again.
        set(facts() with { Permissions = [.. facts().Permissions, Held("s2b3c4d5", "7", title: null)] });
        await slice.JudgeAsync();

        Assert.Equal(["s1a2b3c4#900", "s2b3c4d5#7"], Said().Select(each => each.GetProperty("Id").GetString()));
        Assert.Equal(JsonValueKind.Null, Said()[^1].GetProperty("Detail").ValueKind);
    }

    /// <summary>The switch off says nothing, and what was held meanwhile is seen: turned back on, only what arrives after is said.</summary>
    [Fact]
    public async Task With_the_switch_off_nothing_is_said_and_nothing_is_said_late()
    {
        _notify = false;
        var (slice, facts, set) = Slice(Held("s1a2b3c4", "900"));

        await slice.JudgeAsync();
        Assert.Empty(Said());

        _notify = true;
        await slice.JudgeAsync();
        Assert.Empty(Said());

        set(facts() with { Permissions = [.. facts().Permissions, Held("s1a2b3c4", "901")] });
        await slice.JudgeAsync();
        Assert.Equal(["s1a2b3c4#901"], Said().Select(each => each.GetProperty("Id").GetString()));
    }

    /// <summary>A call answered or released is forgotten with its row, so what is kept is bounded by what is held.</summary>
    [Fact]
    public async Task A_call_no_longer_held_is_forgotten_and_one_held_again_is_said_again()
    {
        var (slice, facts, set) = Slice(Held("s1a2b3c4", "900"));
        await slice.JudgeAsync();

        set(facts() with { Permissions = [] });
        await slice.JudgeAsync();
        set(facts() with { Permissions = [Held("s1a2b3c4", "900")] });
        await slice.JudgeAsync();

        Assert.Equal(["s1a2b3c4#900", "s1a2b3c4#900"], Said().Select(each => each.GetProperty("Id").GetString()));
    }

    /// <summary>No other row is announced here: a park, a helper's ask, a terminal's press each has its own voice or none.</summary>
    [Fact]
    public async Task Only_a_held_call_is_announced_here()
    {
        var facts = new AttentionFacts { Sessions = [JsonNode.Parse("""{"id":"p1","state":"awaiting-person","repository":"engine"}""")!.AsObject()] };
        var slice = AttentionStateSlice.Of(() => facts, Bus, [], held: new HeldCallAnnouncements(Bus, () => true));

        await slice.JudgeAsync();

        Assert.Empty(Said());
    }

    /// <summary>The loop's switch, read at each announcement as a conversation's answer reads it: a choice that does not read errs toward saying it.</summary>
    [Fact]
    public void The_loop_reads_the_notify_switch_at_each_announcement()
    {
        var loop = Loop();
        Assert.True(loop.Notifying());

        DriverConfig.Empty.WithNotify(false).Save(DriverConfigPath);
        Assert.False(loop.Notifying());

        File.WriteAllText(DriverConfigPath, "{ not json");
        Assert.True(loop.Notifying());
    }
}
