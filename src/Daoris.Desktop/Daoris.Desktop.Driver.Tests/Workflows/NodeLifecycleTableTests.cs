using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The one table of node moves, row for row.</summary>
public sealed class NodeLifecycleTableTests
{
    /// <summary>The table is §E2's, row for row, expanded over the live and the bound states, with a skip's withdrawal and lapse.</summary>
    [Fact]
    public void The_table_is_the_designs_row_for_row()
    {
        string Row(NodeTransition row) => $"{row.From}>{row.To}:{row.By}";
        var rows = NodeLifecycle.Table.Select(Row).ToHashSet();

        string[] expected =
        [
            "blocked>ready:daoris", "ready>blocked:daoris", "ready>active:daoris",
            "active>reached:authority", "active>failed:authority", "active>rejected:person", "active>unknown:daoris",
            "unknown>active:daoris", "unknown>reached:authority", "unknown>failed:authority",
            // The person settles what nothing else establishes.
            "unknown>reached:person", "unknown>failed:person",
            "failed>ready:daoris", "failed>ready:person", "rejected>ready:person", "stopped>ready:person",
            "ready>skipped:person", "blocked>skipped:person", "active>skipped:person",
            // A declined request is gone on without, a request node alone.
            "failed>skipped:person",
            // A failed delivery step whose branch the line holds has nothing left to deliver.
            "failed>skipped:daoris",
            // An opinion's gate passes it over where nothing is asked of it.
            "active>skipped:authority",
            "blocked>skipped:daoris", "ready>skipped:daoris", "invalidated>skipped:daoris",
            "skipped>blocked:person", "skipped>invalidated:daoris",
            "blocked>stopped:person", "ready>stopped:person", "active>stopped:person", "unknown>stopped:person", "invalidated>stopped:person",
            "active>invalidated:daoris", "reached>invalidated:daoris", "failed>invalidated:daoris", "rejected>invalidated:daoris",
            "unknown>invalidated:daoris",
            "invalidated>blocked:daoris", "invalidated>ready:daoris",
        ];

        Assert.Equal(expected.Order(), rows.Order());
        Assert.Equal(expected.Length, NodeLifecycle.Table.Count);
        Assert.All(NodeLifecycle.Table, row => Assert.False(string.IsNullOrWhiteSpace(row.When)));
    }
}
