using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A pass's answer so far, kept where its turn is cut or its retry after a limit ends with none.</summary>
public sealed partial class BrainstormBoundsTests
{
    /// <summary>
    /// 🔴 A design pass runs to its own longer deadline, and one cut there keeps the answer it gave so far, said as partial, which the
    /// synthesis reads beside the whole one.
    /// </summary>
    [Fact]
    public async Task A_pass_cut_at_its_brainstorm_s_own_deadline_keeps_its_answer_so_far_and_the_synthesis_reads_it()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 10, request: 60), clock: clock);
        var id = Files.WriteRequest("What should the palette be for?", null, ["stub", "acp-stub"], At, pass: DesignPasses.Design);
        await LookAsync(driver);
        Files.WriteSent(id, Files.ReadState(id)!.Packet!, At);
        script.Answers["g1"] = Give("A neutral grey ladder");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Behaviours["g2"] = (turn, ct) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(turn.Answer)!);
            var partial = JsonNode.Parse(Give("Contrast first, at 4.5:1"))!.AsObject();
            partial["partial"] = true;
            File.WriteAllText(turn.Answer, partial.ToJsonString());
            entered.TrySetResult();
            return Hang(turn, ct);
        };
        string? handed = null;
        script.Behaviours["s1"] = (turn, _) =>
        {
            handed = File.ReadAllText(Path.Combine(turn.Room, BrainstormInstruction.ProposalsFile));
            return Task.FromResult(new BrainstormTurnRun(true, "work", null, "completed  brainstorm turn s1"));
        };

        var look = LookAsync(driver);
        await entered.Task.WaitAsync(Poll.Patience);
        var armed = clock.Pending.Contains(clock.GetUtcNow() + 20 * Minute);
        clock.Advance(20 * Minute);
        await SpendAsync(clock, look);

        Assert.True(armed, "a design pass's deadline is its brainstorm's own, twice the policy's minutes");
        Assert.Equal(("answered", BrainstormPassRecord.Partial, 1), Of(Pass(id, "g2")));
        Assert.Contains("Contrast first, at 4.5:1", handed, StringComparison.Ordinal);
        Assert.Contains("A neutral grey ladder", handed, StringComparison.Ordinal);
        Assert.Contains($"answered ({BrainstormPassRecord.Partial})", Files.DocumentText(id)!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A turn that gave its answer so far and then met its limit keeps it: the retry on the next account hangs past the pass's
    /// minutes, and the first turn's answer is the pass's, on the account that gave it.
    /// </summary>
    [Fact]
    public async Task An_answer_so_far_before_a_limit_is_kept_when_the_retry_is_cut()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 10, request: 60), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g1"] = (turn, _) =>
        {
            var partial = JsonNode.Parse(Give("Contrast first, at 4.5:1"))!.AsObject();
            partial["partial"] = true;
            Directory.CreateDirectory(Path.GetDirectoryName(turn.Answer)!);
            File.WriteAllText(turn.Answer, partial.ToJsonString());
            return Task.FromResult(new BrainstormTurnRun(true, "work", "limit", $"limit  brainstorm turn {turn.Turn}"));
        };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Behaviours["g1r"] = (turn, ct) =>
        {
            entered.TrySetResult();
            return Hang(turn, ct);
        };
        script.Answers["g2"] = Give("Cache the report");
        script.Answers["s1"] = Synthesize("P1", "P2");

        var look = LookAsync(driver);
        await entered.Task.WaitAsync(Poll.Patience);
        await SpendAsync(clock, look);

        var pass = Pass(id, "g1");
        Assert.Equal(("answered", BrainstormPassRecord.Partial, 2), Of(pass));
        Assert.Equal("work", pass["account"]!.GetValue<string>());
        Assert.Contains("Contrast first, at 4.5:1", Files.DocumentText(id)!, StringComparison.Ordinal);
    }
}
