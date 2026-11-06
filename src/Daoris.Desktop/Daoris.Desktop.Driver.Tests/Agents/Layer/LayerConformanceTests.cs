using Daoris.Driver;
using Lyntai.Agents;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The conformance suite's fourth view (the agent-lyntai design §6.2, §6.3): the registry seen through its layer view, its providers those
/// with a door the view binds, and its run kind the binding's, so a case on a bound kind runs on the path a move hands production and a
/// pending line holds it there as it holds it on the other three. Its in-process rows run through the suite's one engine. On the tests'
/// provider bound to the stand-in's native door for a driven start, never a production door.
/// </summary>
public sealed class LayerConformanceTests(ITestOutputHelper output) : IDisposable
{
    private static readonly Lazy<ConformanceSubject> Made = new(Make);

    private readonly string _scratch = ConformanceScratch.New("layer-view");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    internal static ConformanceSubject Subject => Made.Value;

    public static TheoryData<string, string, string, string> InProcess => Conformance.Rows(ConformanceHalf.InProcess, [Subject]);

    [Theory]
    [MemberData(nameof(InProcess))]
    public async Task Each_bound_door_answers_the_contract_or_holds_the_case_pending_on_its_run_kind(
        string source, string provider, string door, string @case)
    {
        Assert.Equal(ConformanceSubject.LayerSource, source);
        if (await Conformance.RunAsync(Subject, provider, door, @case, _scratch) is { } held) output.WriteLine(held);
    }

    [Fact]
    public void The_fourth_view_runs_over_each_provider_with_a_bound_door_on_the_binding_s_run_kind()
    {
        var rows = Conformance.Runs([Subject]).ToList();

        Assert.Equal(StartKind.Driven, Subject.Run);
        Assert.Equal(["stub"], rows.Select(row => row.Provider.Id.Value).Distinct());
        Assert.Contains(rows, row => row.Door == "stub");
        Assert.All(ConformanceSubjects.Every, subject => Assert.Null(subject.Run));
    }

    private static ConformanceSubject Make()
    {
        var built = ProviderRegistry.Built();
        var view = LayerView.Over(
            built,
            [
                new LayerBinding(
                    LayerTestProvider.Registration(["node", "scenario.mjs"], "stub"), AgentDoorKinds.Cli, new HashSet<StartKind> { StartKind.Driven }),
            ]);
        var bound = built.Providers.Where(provider => provider.Doors.Any(door => view.Binds(door.Id.Value, StartKind.Driven))).ToList();
        return new ConformanceSubject(ConformanceSubject.LayerSource, built, built, bound) { Run = StartKind.Driven };
    }
}
