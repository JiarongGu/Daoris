using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A pass's answer so far is kept where its turn ends without a whole one: cut at its brainstorm's own deadline, or a retry after a
/// limit that is cut or cools and finds no slot; the synthesis reads it, and a restart's recovery reads it from the turn that gave it.
/// </summary>
public sealed class BrainstormKeptAnswerTests : BrainstormBench
{
    private static void SoFar(BrainstormTurn turn, string idea)
    {
        var partial = JsonNode.Parse(Give(idea))!.AsObject();
        partial["partial"] = true;
        Directory.CreateDirectory(Path.GetDirectoryName(turn.Answer)!);
        File.WriteAllText(turn.Answer, partial.ToJsonString());
    }

    /// <summary>🔴 A design pass runs to its own longer deadline; cut there, it keeps its answer so far, which the synthesis reads.</summary>
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
            SoFar(turn, "Contrast first, at 4.5:1");
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

        Assert.True(armed, "a design pass's deadline is twice the policy's");
        Assert.Equal(("answered", BrainstormPassRecord.Partial, 1), Of(Pass(id, "g2")));
        Assert.Contains("Contrast first, at 4.5:1", handed, StringComparison.Ordinal);
        Assert.Contains("A neutral grey ladder", handed, StringComparison.Ordinal);
        Assert.Contains($"answered ({BrainstormPassRecord.Partial})", Files.DocumentText(id)!, StringComparison.Ordinal);
    }

    /// <summary>🔴 An answer so far given before a limit is the pass's, on its account, where the retry is cut with none.</summary>
    [Fact]
    public async Task An_answer_so_far_before_a_limit_is_kept_when_the_retry_is_cut()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(rule: Rule(pass: 10, request: 60), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g1"] = (turn, _) =>
        {
            SoFar(turn, "Contrast first, at 4.5:1");
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

        // A driver that stopped before the brainstorm ended reads the kept answer again from the turn that gave it.
        Files.WriteState(Files.ReadState(id)! with { State = BrainstormStates.Running });
        var (again, _) = Start();
        await LookAsync(again);

        Assert.Contains(Document(id)["rows"]!.AsArray(), row => row!["idea"]!.GetValue<string>() == "Contrast first, at 4.5:1");
    }

    /// <summary>🔴 An answer so far before a limit is the pass's where the retry cools, waits, and then finds no slot by the deadline.</summary>
    [Fact]
    public async Task An_answer_so_far_before_a_limit_is_kept_when_the_retry_cools_and_finds_no_slot()
    {
        var clock = new MovedClock();
        var (driver, script) = Start(cap: 1, rule: Rule(pass: 10, synthesis: 2, challenge: 2, request: 6), clock: clock);
        var id = await SentAsync(driver, "stub", "acp-stub");
        script.Behaviours["g1"] = (turn, _) =>
        {
            SoFar(turn, "Contrast first, at 4.5:1");
            return Task.FromResult(new BrainstormTurnRun(true, "work", "limit", $"limit  brainstorm turn {turn.Turn}"));
        };
        script.Behaviours["g1r"] = (_, _) => Task.FromResult(
            new BrainstormTurnRun(false, null, "cooling", "held  brainstorm turn g1r: cooling", "its account cools", clock.GetUtcNow() + TimeSpan.FromMilliseconds(100)));
        script.Behaviours["g2"] = Hang;

        var look = LookAsync(driver);
        await SpendAsync(clock, look);
        await look;

        Assert.Equal(("answered", BrainstormPassRecord.Partial), (Of(Pass(id, "g1")).State, Of(Pass(id, "g1")).Codes));
        Assert.Contains("Contrast first, at 4.5:1", Files.DocumentText(id)!, StringComparison.Ordinal);
    }
}
