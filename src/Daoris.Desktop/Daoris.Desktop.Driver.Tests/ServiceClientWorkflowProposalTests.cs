using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>An ask's intake's kind proposals, read as the service answers them (WORKFLOW1i-b; the workflow design §4.3).</summary>
public sealed class ServiceClientWorkflowProposalTests
{
    [Fact]
    public void An_ask_s_kind_proposals_are_read_with_their_reason_session_and_quest()
    {
        var ask = ServiceClient.ReadAskJson(
            """
            {"id":"a1","workspace":"work","sentence":"s","state":"Published","tier":"intake",
             "workflowProposals":[{"kind":"docs","reason":"it is only the readme","at":"2026-10-08T07:50:00+00:00","session":"s1","quest":"q1"},
                                  {"kind":"fix","at":"2026-10-08T07:55:00+00:00"},
                                  {"reason":"no kind","at":"2026-10-08T07:56:00+00:00"}]}
            """);

        Assert.Equal(
            [new AskWorkflowProposalView("docs", "it is only the readme", new DateTimeOffset(2026, 10, 8, 7, 50, 0, TimeSpan.Zero), "s1", "q1")],
            ask.WorkflowProposals);
    }

    [Fact]
    public void An_ask_no_intake_proposed_a_kind_on_reads_none()
    {
        Assert.Empty(ServiceClient.ReadAskJson("""{"id":"a1","workspace":"w","sentence":"s","state":"Open","tier":"named"}""").WorkflowProposals);
    }
}
