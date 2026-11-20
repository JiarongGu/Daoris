using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>Answers given past their reading, read again: a chat run's line by its turn state, its record working between turns.</summary>
public sealed class OpinionRereadTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-reread-" + Guid.NewGuid().ToString("N")[..8]);

    public OpinionRereadTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class Turns(bool? ended) : IChatSendBacks
    {
        public Task<SentBack> SendBackAsync(ChatSendBackAsk ask, CancellationToken ct = default) => throw new InvalidOperationException("nothing is sent back here");

        public Task<SentBack> GoOnWithFindingsAsync(ChatSendBackAsk ask, CancellationToken ct = default) => throw new InvalidOperationException("nothing goes on here");

        public bool? TurnEnded(string sessionId) => ended;
    }

    /// <summary>🔴 Read once the chat's turn has ended; while one runs, it stays with the session.</summary>
    [Theory]
    [InlineData(true, OpinionDeliveryStates.Answered, OpinionViews.Rejected)]
    [InlineData(false, OpinionDeliveryStates.WithSession, OpinionViews.Unresolved)]
    public async Task A_chat_runs_answers_given_past_its_reading_are_read_once_its_turn_has_ended(bool ended, string state, string counts)
    {
        var tree = Path.Combine(_home, "s-c1");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "README.md"), "a tree that stands");
        var git = new StandInGit(tree);
        git.Commit("base");
        var tip = git.Commit("the work read", ("src/report.ts", "v1"));
        var host = new OpinionStandIn().Chat("c1", "working", tree).Opinion("op1", "c1", weights: ["must"], tip: tip);
        host.HandedTo("op1", "c1");
        var deliveries = new OpinionDeliveries(_home);
        deliveries.Answered("op1", new OpinionReading("c1", tip, DateTimeOffset.UnixEpoch,
            [new OpinionAnswerRead(1, "must", OpinionViews.Unresolved) { Why = OpinionAnswerWhy.NotAnswered }]));
        deliveries.Rechecked("op1", null, OpinionRechecks.NoFix);
        host.Answer("op1", 1, "rejected", evidence: "the check shows it holds");
        var driver = new Daoris.Driver.Driver(host.Client(), DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp") }, ProviderRegistry.Built(), _home)
        {
            OpinionGit = git.Read,
            Passes = (_, _, _) => throw new InvalidOperationException("no recheck is asked here"),
            SendsBack = new Turns(ended),
        };
        var asked = new OpinionPassAsk(OpinionRules.Landing, "c1", "reports", tree, OpinionStandIn.Base, tip, new OpinionRule(["codex-acp"], [OpinionRules.Landing]))
        {
            Workspace = "work", Run = "chat-c1", Turns = 0,
        };

        var delivered = await driver.DeliverAsync("op1", asked);

        Assert.Equal(state, delivered.State);
        Assert.Equal(counts, deliveries.Read("op1").Answered!.Findings[0].Counts);
    }
}
