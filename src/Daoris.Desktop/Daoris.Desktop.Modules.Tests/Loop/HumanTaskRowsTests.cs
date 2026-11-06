using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// NODEKIT1 HUMAN: a run waiting on a person's task is one row of <i>What needs you</i>, keyed by its run and node as the other waits are, in the
/// run's workspace, carrying the presses it waits for; a run with none is no row. Seen failing first.
/// </summary>
public sealed class HumanTaskRowsTests
{
    /// <summary>A run waiting on a person's task, as <see cref="AttentionStateSlice.FactsAsync"/> writes one.</summary>
    private static JsonObject Waiting(string over = "{}") => With(
        """{"run":"q7","node":"sign","repository":"engine","workspace":"aurora","acts":["approve","reject"],"work":"q7"}""", over);

    [Fact]
    public void A_run_waiting_on_a_persons_task_is_one_row_keyed_by_its_run_and_node()
    {
        var rows = Rows(new() { Tasks = [Waiting(), Waiting("""{"node":"release","acts":["done"],"workspace":null}""")] });

        Is("""
            {"kind":"task","id":"q7/sign","since":null,"workspace":"aurora","where":"engine",
             "task":{"run":"q7","node":"sign","acts":["approve","reject"],"work":"q7"}}
            """, Assert.Single(rows, row => row["id"]!.GetValue<string>() == "q7/sign"));
        Assert.Equal(["task:q7/release", "task:q7/sign"], Keys(rows).Order(StringComparer.Ordinal));
        Assert.Equal("default", Assert.Single(rows, row => row["id"]!.GetValue<string>() == "q7/release")["workspace"]!.GetValue<string>());
        Assert.Empty(Rows(new() { Tasks = [] }));
    }

    [Fact]
    public void The_runtimes_task_is_written_as_its_row_reads_it()
    {
        var written = System.Text.Json.JsonSerializer.SerializeToNode(
            new HumanTask("q7", "sign", "engine", "aurora", [NodeWordActs.Approve, NodeWordActs.Reject], "q7"), Shenora.Core.Ipc.IpcJson.Options)!.AsObject();
        Is(Waiting().ToJsonString(), written);
    }
}
