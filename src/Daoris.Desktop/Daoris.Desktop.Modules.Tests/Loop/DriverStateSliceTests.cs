using System.Text.Json;
using System.Text.Json.Nodes;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The driver's state, the push path's first slice (PUSH1a, D182): one owner in the shell's loop judging what <c>STATE</c> answers
/// into three allow-listed partitions, pushed when they change, and read by a window's first snapshot from memory.
/// </summary>
public sealed class DriverStateSliceTests : DriverModuleBridge
{
    private static JsonObject Data(SliceSnapshot snapshot, string partition) =>
        JsonSerializer.SerializeToNode(snapshot.Partitions.Single(each => each.Partition == partition).Data, IpcJson.Options)!.AsObject();

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == DriverStateSlice.Name),
    ];

    [Fact]
    public async Task The_driver_s_state_is_pushed_in_three_partitions_of_allow_listed_fields_and_every_field_STATE_answers_is_classed()
    {
        var loop = Loop();
        await loop.DriverSlice.JudgeAsync();
        var snapshot = loop.DriverSlice.Snapshot();

        Assert.Equal(DriverStateSlice.Name, snapshot.Slice);
        Assert.Equal(["choices", "running", "shell"], snapshot.Partitions.Select(each => each.Partition).Order());
        foreach (var (partition, fields) in DriverStateSlice.Partitions)
        {
            Assert.All(Data(snapshot, partition).Select(each => each.Key), field => Assert.Contains(field, fields));
        }

        // A field STATE comes to answer is pushed or named as the pull's alone, never left to chance.
        var state = await AnswerAsync(new DriverModule(Bus, loop), "STATE");
        var classed = DriverStateSlice.Partitions.SelectMany(each => each.Value).Concat(DriverStateSlice.PulledOnly).ToHashSet();
        Assert.All(state.EnumerateObject().Select(each => each.Name), field => Assert.Contains(field, classed));
        Assert.Equal(state.GetProperty("drivable").GetRawText(), Data(snapshot, "choices")["drivable"]!.ToJsonString());
    }

    [Fact]
    public async Task A_hostile_field_in_the_driver_s_file_never_reaches_a_partition()
    {
        File.WriteAllText(DriverConfigPath, """
            {
              "drivable": ["engine"],
              "holds": [],
              "token": "sk-hostile-top",
              "commands": { "engine": ["deploy --key sk-hostile-command"] },
              "landings": { "engine": { "form": "merge", "secret": "sk-hostile-nested" } },
              "lines": { "engine": "main" }
            }
            """);
        var loop = Loop();

        await loop.DriverSlice.JudgeAsync();

        var pushed = JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options);
        Assert.Contains("engine", pushed);
        Assert.DoesNotContain("sk-hostile", pushed);
        // The file the choices are read from is a path the page does not read: the pull alone answers it.
        Assert.DoesNotContain("driver.json", pushed);
        Assert.DoesNotContain("configPath", pushed);
    }

    [Fact]
    public async Task A_second_window_s_snapshot_is_read_from_memory_and_judges_nothing()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);

        var before = await AnswerAsync(module, "STATE_SLICE", new { slice = "driver" });
        Assert.Equal(StateSlice.NotJudged, before.GetProperty("status").GetString());
        Assert.Empty(before.GetProperty("partitions").EnumerateArray());

        await loop.DriverSlice.JudgeAsync();
        var first = await AnswerAsync(module, "STATE_SLICE", new { slice = "driver" });
        var second = await AnswerAsync(module, "STATE_SLICE", new { slice = "driver" });

        Assert.Equal(1, loop.DriverSlice.Judgements);
        Assert.Equal(first.GetRawText(), second.GetRawText());
        Assert.Equal(loop.DriverSlice.Epoch, first.GetProperty("epoch").GetString());
        Assert.All(first.GetProperty("partitions").EnumerateArray(), each => Assert.Equal(1, each.GetProperty("seq").GetInt64()));
    }

    [Fact]
    public async Task A_slice_this_Daoris_does_not_push_answers_nothing_so_the_page_keeps_its_pull()
    {
        var answer = await AskAsync(Module(), "STATE_SLICE", new { slice = "sweeps" });

        Assert.True(answer.Success);
        Assert.Null(answer.Data);
    }

    [Fact]
    public async Task A_standing_choice_changed_wakes_the_slice_which_pushes_its_choices_alone()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        using var stop = new CancellationTokenSource();
        var running = loop.Slices.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count == 3);

        await AnswerAsync(module, "SET_NOTIFY", new { notify = false });
        // What the dispatcher's publisher says once the press has answered.
        StatePublisher.Publish(Bus, StateScopes.Driver);

        await UntilAsync(() => Pushes().Count == 4);
        var pushed = Pushes()[^1];
        Assert.Equal(("choices", 2L), (pushed.Partition, pushed.Seq));
        Assert.False(JsonSerializer.SerializeToNode(pushed.Data, IpcJson.Options)!["notify"]!.GetValue<bool>());

        await stop.CancelAsync();
        await running;
    }

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["driver"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions"],"sessions":["s1"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["harnesses"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["quests","asks"]}""", false)]
    [InlineData("SESSION_ENDED", "{}", true)]
    [InlineData("DRIVER_READY", "{}", true)]
    public void What_wakes_the_driver_s_slice(string type, string payload, bool wakes)
    {
        var wake = DriverStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }
}
