using System.Reflection;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Plugins;

/// <summary>
/// The frame census: every answer kind a plugin sends at <c>work/step</c> reaches every reader that counts answers. The kinds
/// are <see cref="WorkStates"/>' constants, read by reflection, so a kind added there is counted here before any reader is; each reader is
/// probed by what it does with a frame of the kind, never by what it says of itself: the wire reads it, the trial (<see cref="PluginCycles"/>)
/// counts a completion apart from a progress, and the run-said table says each wait a progress makes. Seen failing first on <c>progress</c>,
/// whose waits the run-said table did not say.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class WorkFrameCensusTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 16, 9, 51, 0, TimeSpan.Zero);

    /// <summary>Every kind <see cref="WorkStates"/> names: the census's source, so no kind is left to a list kept by hand.</summary>
    public static TheoryData<string> Kinds() => [.. Named()];

    private static IReadOnlyList<string> Named() =>
        [.. typeof(WorkStates).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!)];

    private static WorkStepFrame Bound(string kind) =>
        new(Plugin + "/release", new WorkOutcome(kind, kind == WorkStates.Failed ? "unavailable" : null), At, "e-" + kind, Invocation: "i-1");

    private const string Plugin = "daoris-azure-devops";

    [Fact]
    public void The_kinds_are_every_one_a_plugin_sends()
    {
        Assert.Equal(WorkFrameKinds.All.Order(StringComparer.Ordinal), Named().Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void The_wire_reads_each_kind_bound_to_its_attempt(string kind)
    {
        var written = WorkWire.Step(Bound(kind));
        using var document = JsonDocument.Parse(written["params"]!.ToJsonString());

        var read = WorkWire.ReadStep(document.RootElement, out var field);

        Assert.True(read is not null, $"a bound `{kind}` frame does not read, broken at `{field}`");
        Assert.Equal(kind, read!.Outcome.State);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void The_trial_counts_a_completion_apart_from_a_progress(string kind)
    {
        Assert.Equal(WorkFrameKinds.Completes(kind), PluginCycles.Completes(Bound(kind), "i-1"));
        Assert.Equal(kind != WorkStates.Progress, WorkFrameKinds.Completes(kind));
    }

    /// <summary>Each wait a frame that completes nothing makes on a run's step is a row of the run-said table, said in its words.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void The_run_said_table_says_each_wait_a_kind_makes(string kind)
    {
        if (WorkFrameKinds.Completes(kind)) return;
        var codes = new[] { new StepProgress("approver", "Approve Production.", null, null, At), new StepProgress(null, "Deploying.", null, null, At) }
            .Select(progress => StepWaiting.WaitOf(progress).Code).Distinct(StringComparer.Ordinal).ToList();

        var said = WaitRows().Select(row => row.GetProperty("code").GetString()).ToHashSet(StringComparer.Ordinal);

        foreach (var code in codes) Assert.True(said.Contains(code), $"a `{kind}` frame waits as `{code}`, and the run-said table has no `waits` row for it");
    }

    /// <summary>The table's <c>waits</c> rows, said word for word by <see cref="StepWaiting.Said"/>.</summary>
    [Fact]
    public void A_wait_row_is_said_in_the_tables_words()
    {
        foreach (var row in WaitRows())
        {
            var values = row.EnumerateObject().Where(each => each.Name is not ("code" or "said") && each.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(each => each.Name, each => each.Value.GetString()!, StringComparer.Ordinal);
            var wait = new WorkflowRunWait(row.GetProperty("code").GetString()!, values, At);

            Assert.Equal(row.GetProperty("said").GetString(), StepWaiting.Said(wait));
        }
    }

    private static IReadOnlyList<JsonElement> WaitRows()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "workflow-run-said.json")));
        return document.RootElement.TryGetProperty("waits", out var waits) ? [.. waits.EnumerateArray().Select(row => row.Clone())] : [];
    }
}
