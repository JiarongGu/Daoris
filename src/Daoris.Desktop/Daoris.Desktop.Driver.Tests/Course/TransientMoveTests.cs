using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation moved by its line's tries rule counts the move in <c>moves</c> only once it is a move that is made: not where the take would
/// refuse it (another model of the agent it already runs on, on the tool's own home), and not where a move already stands for it.
/// </summary>
public sealed class TransientMoveTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "transient-move", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public TransientMoveTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>A tries rule at two that moves by weight, and a bound of three moves.</summary>
    private void Ruled()
    {
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.True(staffing.ImportFile().How is StaffingImported.Done or StaffingImported.Already);
        var store = new SupervisorRuleStore(_home);
        SupervisorRule Rule(string count, int at, string does) =>
            new($"chat-{count}", count, SupervisorRules.Windows[count], at, does, null, SupervisorRules.Machine, [RoleWork.Chat]);
        Assert.True(store.Write(
            SupervisorRules.Machine, store.Of(SupervisorRules.Machine).Revision,
            [.. SupervisorRuleBook.Seeded(3), Rule(SupervisorRules.Tries, 2, SupervisorRules.Weighted), Rule(SupervisorRules.Moves, 3, SupervisorRules.Ask)],
            SupervisorRuleBook.SeedDoor, SupervisorRuleDoor.World(_home)).Stored);
    }

    private static DriverConfig Config() =>
        ActorChoices.With(DriverConfig.Empty, "chat", [new ActorChoice("acp-stub", "first"), new ActorChoice("acp-stub", "second")]);

    private (ChatRunner Runner, ChatLedger Ledger, ServiceClient Client) Runner(string? profile)
    {
        Ruled();
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "working", "acp-stub", _root, profile: profile);
        var client = ledger.Client();
        var adapters = ProviderRegistry.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses));
        var runner = new ChatRunner(client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events, harnesses: roster);
        return (runner, ledger, client);
    }

    private int Moves() => new StaffLedger(_home, () => StoreSettled.Yes).Moves.Of("c1").Moves;

    /// <summary>🔴 A move the take refuses (another model of the agent the conversation runs on, with no account of its own) is never counted.</summary>
    [Fact]
    public async Task A_move_to_another_model_of_its_own_agent_the_take_refuses_is_not_counted()
    {
        var (runner, ledger, client) = Runner(profile: null);
        using var r = runner;
        using var c = client;

        var course = await runner.Executors.Of<TransientMove>().MoveAsync("c1", Config(), null);

        Assert.IsType<LineCourse.Move>(course);
        Assert.Equal(0, Moves());
        Assert.Empty(ledger.Opened);
    }

    /// <summary>🔴 A move that already stands for the record makes no move, so counts none.</summary>
    [Fact]
    public async Task A_move_that_already_stands_for_the_record_is_not_counted_again()
    {
        var (runner, ledger, client) = Runner(profile: "account-1");
        using var r = runner;
        using var c = client;
        var transient = runner.Executors.Of<TransientMove>();
        var standing = await transient.Moves.BeginAsync(
            new MoveDecision("c1", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Transient, HandOverBy.Daoris), CancellationToken.None);
        Assert.NotNull(standing.Move);

        await transient.MoveAsync("c1", Config(), null);

        Assert.Equal(0, Moves());
        Assert.Empty(ledger.Opened);
    }
}
