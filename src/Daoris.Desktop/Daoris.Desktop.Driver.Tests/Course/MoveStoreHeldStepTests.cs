using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A store held below step 18 (the stamp raise at 16 waits for the start's confirmation) takes a move without the model and effort columns it does not have.</summary>
public sealed class MoveStoreHeldStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-store-held", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    [Fact]
    public void A_store_held_at_step_15_decides_a_move_and_reads_it_back_with_no_model_or_effort()
    {
        using (var held = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Where(step => step.Version <= 15)], Settled = () => StoreSettled.Yes }))
        {
            Assert.True(held.Migrate());
        }

        var store = new MoveStore(_home, () => StoreSettled.Not("the start is not confirmed"), _ => true, "run-a", StoreProcess.Shell);
        var decision = new MoveDecision("c1", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Press, HandOverBy.Person) { Agent = "talk", Account = "account-2" };

        var decided = store.Decide(decision with { Model = "gpt-5", Effort = "high" });

        Assert.NotNull(decided.Move);
        var read = store.Of("c1").Single();
        Assert.Equal(("talk", "account-2", null, null), (read.Agent, read.Account, read.Model, read.Effort));
    }
}
