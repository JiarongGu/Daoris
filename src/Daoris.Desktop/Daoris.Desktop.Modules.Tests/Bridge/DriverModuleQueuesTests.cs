using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The machine's queues over the bridge: <c>QUEUES</c> answers the rows of the one reader <c>daoris queue</c> prints, its lines the verb's
/// own over the same look and home, so the window and the terminal give one place for one item; <c>PRIORITY_SET</c> sets a class through
/// the operation <c>daoris priority</c> calls, its refusal in that operation's words.
/// </summary>
public sealed class DriverModuleQueuesTests : DriverModuleBridge
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private static byte[] Json(string text) => Encoding.UTF8.GetBytes(text);

    private static QuestView Open(string id, int minutesAgo) =>
        new(id, "Asker", "engine", $"Ask {id}", "", "Open") { Updated = Now.AddMinutes(-minutesAgo) };

    private static Consideration Waiting(string id, int minutesAgo, string @class, PriorityReading? reading = null) =>
        new(Open(id, minutesAgo), StartVerdict.AtCapacity, "The concurrency cap (1) is spent.")
        {
            Said = new SittingFact(SittingFact.CapSpent) { Cap = 1 }, Place = new QueuePlace(@class, QueueStage.NewStart, Now.AddMinutes(-minutesAgo)),
            Priority = reading ?? PriorityReading.None,
        };

    [Fact]
    public async Task The_route_s_lines_are_the_verb_s_alone_over_the_same_look_and_its_rows_place_each_item_once()
    {
        var loop = Loop();
        var urgent = new PriorityEvent(Now, PriorityBook.Quest, "late", "aurora", PriorityClasses.Urgent, "the fix", "the person") { Until = Now.AddHours(1) };
        IReadOnlyList<Consideration> plan = [Waiting("early", 30, PriorityClasses.Normal), Waiting("late", 5, PriorityClasses.Urgent, new(PriorityClasses.Urgent, PriorityBook.Quest, urgent))];
        loop.Look.Record(plan);
        var module = new DriverModule(Bus, loop);

        var answer = await AnswerAsync(module, "QUEUES", new { said = true });
        using var alone = new StringWriter();
        await QueueCommand.RunAsync([], new QueueWorld(Home, _ => Task.FromResult(plan)), alone);

        var said = answer.GetProperty("said").GetProperty("out").EnumerateArray().Select(line => line.GetString()).ToList();
        Assert.Equal(alone.ToString().TrimEnd().Split(Environment.NewLine), said);
        Assert.StartsWith("starts 1 of 2: quest #late, urgent (set by the person: the fix), a new start;", said[0], StringComparison.Ordinal);
        var rows = answer.GetProperty("rows").EnumerateArray().Select(QueueRows.Parse).ToList();
        Assert.Equal(["late", "early"], QueueRows.Placed(rows!).Select(spot => spot.Row.Item));
        Assert.Equal("1", rows[0]!.Values["cap"]);
    }

    [Fact]
    public async Task Before_the_first_look_and_the_service_the_read_says_the_driver_is_coming_up()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "QUEUES", new { }));
    }

    [Fact]
    public async Task The_press_sets_a_quest_s_class_through_the_command_s_operation_and_refuses_in_its_words()
    {
        using var service = new LoopbackHost();
        service.Serve("/api/registry", Json("[]"));
        service.Serve("/api/sessions", Json("[]"));
        service.Serve("/api/quests", Json("[]"));
        service.Serve("/api/asks?includeClosed=true", Json("[]"));
        service.Serve("/api/quests?includeClosed=true", Json("""[{"id":"q1","from":"ask #e27fc2","to":"engine","title":"t","body":"","status":"Open","workspace":"aurora"}]"""));
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient(service.Address, null));
        var module = new DriverModule(Bus, loop);

        var set = await AnswerAsync(module, "PRIORITY_SET", new { quest = "#q1", @class = "high", why = "the release" });

        Assert.Equal("quest #q1 is high, set by the person: the release.", set.GetProperty("said").GetString());
        var kept = Assert.Single(PriorityBook.Read(Home));
        Assert.Equal((PriorityBook.Quest, "q1", "aurora", "the person", "q1"), (kept.Target, kept.Id, kept.Workspace, kept.By, kept.Run));
        Assert.Contains("names no ask or quest", await RefusalAsync(module, "PRIORITY_SET", new { quest = "q9", @class = "low", why = "w" }));
        Assert.Contains("urgent, high, normal or low", await RefusalAsync(module, "PRIORITY_SET", new { quest = "q1", @class = "critical", why = "w" }));
        Assert.Contains("with why", await RefusalAsync(module, "PRIORITY_SET", new { quest = "q1", @class = "low", why = " " }));
        Assert.Single(PriorityBook.Read(Home));
    }
}
