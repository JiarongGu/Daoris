using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A run step waiting on a role the person holds is one row of <i>What needs you</i>, keyed by its run and node as the other waits are, in
/// the run's workspace, carrying its wait, the role and holder, the presses that settle it and the page where its act is taken; a run with
/// none is no row.
/// </summary>
public sealed class HumanTaskRowsTests
{
    private static JsonObject Written(HeldStep step) =>
        System.Text.Json.JsonSerializer.SerializeToNode(step, Shenora.Core.Ipc.IpcJson.Options)!.AsObject();

    [Fact]
    public void A_step_waiting_on_a_role_the_person_holds_is_one_row_keyed_by_its_run_and_node()
    {
        var rows = Rows(new()
        {
            Steps =
            [
                Written(new HeldStep("q7", "sign", "engine", "aurora", PersonWaits.Approval, "approver", [NodeWordActs.Approve, NodeWordActs.Reject], "q7", null)),
                Written(new HeldStep("q7", "release", "engine", "", PersonWaits.Task, "worker", [NodeWordActs.Done], "q7", null)),
            ],
        });

        Is("""
            {"kind":"step","id":"q7/sign","since":null,"workspace":"aurora","where":"engine",
             "step":{"run":"q7","node":"sign","why":"approval","role":"approver","holder":"person","presses":["approve","reject"],"work":"q7"}}
            """, Assert.Single(rows, row => row["id"]!.GetValue<string>() == "q7/sign"));
        Assert.Equal(["step:q7/release", "step:q7/sign"], Keys(rows).Order(StringComparer.Ordinal));
        Assert.Empty(Rows(new() { Steps = [] }));

        // Every field of the row is one the slice pushes to the page.
        foreach (var row in rows) Assert.Equal(Normal(row), Normal(AttentionStateSlice.Picked(row)));
    }

    [Fact]
    public void A_waiting_step_row_says_since_when_it_waits()
    {
        var since = new DateTimeOffset(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);
        var written = Written(new HeldStep("q7", "sign", "engine", "aurora", PersonWaits.Task, "worker", [NodeWordActs.Done], "q7", since));
        var row = Assert.Single(Rows(new() { Steps = [written] }));
        Assert.Equal(since, DateTimeOffset.Parse(row["since"]!.GetValue<string>()));
    }

    [Fact]
    public void A_release_waiting_on_an_approver_the_person_holds_carries_the_act_in_its_plugins_words_and_the_releases_page()
    {
        const string Page = "https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=90";
        var written = Written(new HeldStep("q7", "release", "engine", "aurora", PersonWaits.Actor, "approver", [], "q7", null)
        {
            Words = "Approve Production in Azure DevOps: asked of Release Approvers.", Url = Page,
        });

        var row = Assert.Single(Rows(new() { Steps = [written] }));

        Is($$$"""
            {"kind":"step","id":"q7/release","since":null,"workspace":"aurora","where":"engine",
             "step":{"run":"q7","node":"release","why":"actor","role":"approver","holder":"person","presses":[],
                     "words":"Approve Production in Azure DevOps: asked of Release Approvers.","url":"{{{Page}}}","work":"q7"}}
            """, row);
        Assert.Equal(Normal(row), Normal(AttentionStateSlice.Picked(row)));
    }
}
