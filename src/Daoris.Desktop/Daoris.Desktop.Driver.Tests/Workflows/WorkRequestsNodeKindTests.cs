using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1o part o1 (the integration design §17.3, §17.6): the <c>work.requests</c> kind's subject, its title and the two places a version
/// names kinds apart from the graph rules, which hold it (<c>graph-problems.json</c>, <c>workflow-graph-versions.json</c>). Its authority, the
/// refusal of a <c>requests</c> option on <c>claimed</c> and the older build's reading are <see cref="NodeLifecycleTests"/>'s, its lowering
/// <see cref="WorkflowGateTests"/>'s. Nothing observes the kind yet (part o2).
/// </summary>
public sealed class WorkRequestsNodeKindTests
{
    private static readonly NodeDeclaration Requests = new("requests", NodeKinds.WorkRequests, 1, NodeReach.Observed);

    /// <summary>§17.6 point 2: the subject is the run itself, whose sessions' published quests the node reads, with no revision of its own.</summary>
    [Fact]
    public void The_subject_of_work_requests_is_the_run()
    {
        Assert.Equal(RunSubject.Run, RunSubjects.Of(Requests, new HashSet<string>()));
        Assert.Equal(RunSubject.Run, RunSubjects.Of(Requests, new HashSet<string> { "requests" }));
    }

    /// <summary>The run's view names the step, "The work's requests", where the driver says a node's title in the terminal.</summary>
    [Fact]
    public void The_run_names_the_step_the_work_s_requests()
    {
        var step = new WorkflowStep("requests", NodeKinds.WorkRequests, WorkflowParticipation.Automatic, WorkflowExecutor.Daoris, null,
            new WorkflowSource("workflow", WorkflowLevels.Repository), [], WorkflowRuntime.Built, null);

        Assert.Equal("The work's requests", WorkflowRunWords.Title(step));
    }

    /// <summary>
    /// §17.2 point 4: the kind is a graph node only. A version of steps naming it reads as it reads any name that is no kind of step, and the
    /// list of the steps' kinds says no more than it did.
    /// </summary>
    [Fact]
    public void A_steps_version_naming_work_requests_is_no_kind_of_step()
    {
        using var steps = JsonDocument.Parse("""[{"id":"requests","kind":"work.requests"}]""");
        var read = WorkflowNamed.ReadSteps(steps.RootElement, 1);

        Assert.Null(read.Steps);
        Assert.Equal("v1, step `requests`: `work.requests` is no kind of step — one of `work`, `opinion`, `look`, `landing`, `pull-request`, `go-ahead`, `check`, `stage` or `all`.", read.Problem);
    }
}
