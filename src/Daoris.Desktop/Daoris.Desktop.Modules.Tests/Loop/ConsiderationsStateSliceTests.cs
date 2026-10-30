using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Why each open quest is sitting, the holds for the agent's trust and the starts held on accounts, a pushed slice (FOUNDATION1s, D182):
/// one owner in the shell's loop judging what its last look kept, pushed when it moves, so a tick writes none of it into a window.
/// </summary>
public sealed class ConsiderationsStateSliceTests : DriverModuleBridge
{
    private static JsonArray Rows(SliceSnapshot snapshot, string partition) =>
        JsonSerializer.SerializeToNode(snapshot.Partitions.Single(each => each.Partition == partition).Data, IpcJson.Options)!.AsArray();

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == ConsiderationsStateSlice.Name),
    ];

    [Fact]
    public async Task Nothing_is_judged_before_the_first_look_and_each_kind_is_a_partition_after_it()
    {
        var loop = Loop();
        var slice = loop.Slices.Find(ConsiderationsStateSlice.Name)!;

        await slice.JudgeAsync();
        Assert.Equal(StateSlice.NotJudged, slice.Snapshot().Status);

        loop.RecordConsidered([new { Quest = "q1", Verdict = "Blocked" }]);
        loop.Trust.Record([new TrustHold("X:/engine", "X:/profile/.claude.json", Quest: "q1")]);
        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();

        Assert.Equal((ConsiderationsStateSlice.Name, StateSlice.Ready), (snapshot.Slice, snapshot.Status));
        Assert.Equal(["considered", "untrusted", "waits"], snapshot.Partitions.Select(each => each.Partition).Order());
        Assert.Equal("q1", Rows(snapshot, "considered")[0]!["quest"]!.GetValue<string>());
        var hold = Rows(snapshot, "untrusted")[0]!.AsObject();
        Assert.Equal(["folder", "quest", "trustFile"], hold.Select(each => each.Key).Order());
        Assert.Empty(Rows(snapshot, "waits"));
    }

    [Fact]
    public async Task A_judgement_that_changes_nothing_pushes_nothing_and_one_that_moves_pushes_that_partition_alone()
    {
        var loop = Loop();
        var slice = loop.Slices.Find(ConsiderationsStateSlice.Name)!;
        loop.RecordConsidered([]);
        await slice.JudgeAsync();
        var first = Pushes().Count;

        await slice.JudgeAsync();
        Assert.Equal(first, Pushes().Count);

        loop.Trust.Record([new TrustHold("X:/engine", "X:/profile/.claude.json", Ask: "a1")]);
        await slice.JudgeAsync();
        var moved = Pushes().Skip(first).ToList();
        Assert.Equal(["untrusted"], moved.Select(push => push.Partition));
    }

    /// <summary>A trust press is shown at once: the hold is gone from the slice as the press answers, with no look between.</summary>
    [Fact]
    public async Task A_verified_trust_press_takes_the_hold_off_the_slice_with_no_look_between()
    {
        var loop = Loop();
        var file = Path.Combine(Home, "profile", ".claude.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{"numStartups":7,"projects":{}}""");
        var folder = Path.Combine(Home, "engine");
        var slice = loop.Slices.Find(ConsiderationsStateSlice.Name)!;
        loop.RecordConsidered([]);
        loop.Trust.Record([new TrustHold(folder, file, Quest: "q1"), new TrustHold(Path.Combine(Home, "other"), file, Quest: "q2")]);
        await slice.JudgeAsync();
        Assert.Equal(2, Rows(slice.Snapshot(), "untrusted").Count);

        await AnswerAsync(new DriverModule(Bus, loop), "TRUST_FOLDER", new { folder, trustFile = file });
        await slice.JudgeAsync();

        var left = Rows(slice.Snapshot(), "untrusted");
        Assert.Equal(["q2"], left.Select(each => each!["quest"]!.GetValue<string>()));
    }
}
