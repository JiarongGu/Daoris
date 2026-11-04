using Daoris.Contracts.Parts;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The chat runner builds the executors its modules register, phase by phase in their order, each from the runner as its wiring; an
/// order the marks cannot make, or an executor the runner cannot build, is refused as the runner is made and never at a start.
/// </summary>
public sealed class ChatRunnerPartsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-chat-parts-" + Guid.NewGuid().ToString("N")[..8]);

    public ChatRunnerPartsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void The_runner_builds_the_registered_executors_in_their_order_each_from_its_wiring()
    {
        using var runner = new ChatRunner(
            new ChatLedger().Client(), AdapterSet.Built(), _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)));

        var order = runner.Executors.Order;
        Assert.Equal(["help", "start-from", "briefed-move", "help-access", "replacement"], order.Select(part => part.Name));
        Assert.Equal(
            [ExecutorPhase.Start, ExecutorPhase.Start, ExecutorPhase.Move, ExecutorPhase.Move, ExecutorPhase.Move],
            order.Select(part => (ExecutorPhase)part.Phase));

        // What each phase holds, in the order the runner built it.
        Assert.Collection(
            runner.Executors.In(ExecutorPhase.Start),
            executor => Assert.IsType<HelpStart>(executor),
            executor => Assert.IsType<WordsStart>(executor));
        Assert.Collection(
            runner.Executors.In(ExecutorPhase.Move),
            executor => Assert.IsType<BriefedMove>(executor),
            executor => Assert.IsType<HelpAccessSwap>(executor),
            executor => Assert.IsType<ChatReplacement>(executor));
    }

    [Fact]
    public void A_start_comes_before_a_move_each_after_the_ones_it_names_and_a_cycle_is_refused_by_its_members()
    {
        var order = PartOrder.Of(
        [
            Declared("replacement", ExecutorPhase.Move),
            Declared("start-from", ExecutorPhase.Start, "help"),
            Declared("help", ExecutorPhase.Start),
        ]);
        Assert.Equal(["help", "start-from", "replacement"], order.Select(part => part.Name));

        var cycle = Assert.Throws<PartOrderException>(() => PartOrder.Of(
            [Declared("help", ExecutorPhase.Start, "start-from"), Declared("start-from", ExecutorPhase.Start, "help"), Declared("replacement", ExecutorPhase.Move)]));
        Assert.Equal("the parts help → start-from → help come after each other, so none of them can run first", cycle.Message);
    }

    [Fact]
    public void A_kind_declared_twice_a_start_after_a_move_and_an_executor_its_runner_cannot_build_are_refused()
    {
        Assert.Equal(
            "the part help is declared twice, by HelpStart and WordsStart",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("help", ExecutorPhase.Start, type: typeof(HelpStart)), Declared("help", ExecutorPhase.Move)])).Message);
        Assert.Equal(
            "the part start-from runs in Start and comes after replacement, which runs later, in Move",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("start-from", ExecutorPhase.Start, "replacement"), Declared("replacement", ExecutorPhase.Move)])).Message);

        // A constructor asking for wiring the runner does not hand fails as the runner is made, naming the executor.
        var unbuilt = Assert.Throws<PartOrderException>(() => Parts.Build<IExecutor>(
            [Declared("start-from", ExecutorPhase.Start)], type => (IExecutor)Activator.CreateInstance(type, new object())!));
        Assert.StartsWith("the part start-from (WordsStart) could not be built from its root's wiring: ", unbuilt.Message);
    }

    private static PartDeclaration Declared(string name, ExecutorPhase phase, params string[] after) =>
        Declared(name, phase, typeof(WordsStart), after);

    private static PartDeclaration Declared(string name, ExecutorPhase phase, Type type, params string[] after) =>
        new(name, phase, after, type);
}
