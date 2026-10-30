using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What an agent Daoris starts may do, a pushed slice (FOUNDATION1s part B, D182): the rules as <c>RULES</c> answers them, judged in the
/// shell's loop and pushed only when they move, so a window reads them from memory and no tick or focus asks the route.
/// </summary>
public sealed class RulesStateSliceTests : DriverModuleBridge
{
    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == RulesStateSlice.Name),
    ];

    [Fact]
    public async Task The_rules_are_judged_as_their_route_answers_them_and_pushed_only_when_they_move()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var slice = loop.Slices.Find(RulesStateSlice.Name)!;

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();
        Assert.Equal(StateSlice.Ready, snapshot.Status);
        var rules = Assert.Single(snapshot.Partitions);
        Assert.Equal(RulesStateSlice.Partition, rules.Partition);
        var pushed = JsonSerializer.SerializeToElement(rules.Data, IpcJson.Options);
        var answered = await AnswerAsync(module, "RULES");
        Assert.Equal(answered.GetProperty("defaults").GetArrayLength(), pushed.GetProperty("defaults").GetArrayLength());
        Assert.Equal("machine", pushed.GetProperty("scopes")[0].GetProperty("scope").GetString());

        var first = Pushes().Count;
        await slice.JudgeAsync();
        Assert.Equal(first, Pushes().Count);

        await AnswerAsync(module, "RULE_ACTION", new { action = "add", list = "deny", rule = "Bash(rm:*)" });
        await slice.JudgeAsync();
        var moved = Assert.Single(Pushes().Skip(first));
        Assert.Contains("Bash(rm:*)", JsonSerializer.Serialize(moved.Data, IpcJson.Options), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["rules"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions","driver"]}""", false)]
    [InlineData("DRIVER_TICK", "{}", false)]
    public void What_wakes_the_rules_slice(string type, string payload, bool wakes)
    {
        var wake = RulesStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }
}
