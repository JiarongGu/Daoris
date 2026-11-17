using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("Daoris.Desktop.Driver.Tests.CensusWarmFramework", "Daoris.Desktop.Driver.Tests")]

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the censuses share, made once a run before any census case's clock starts: the say door's scan of the driver, its host and the
/// modules (<see cref="SayDoorSources"/>), the comment-stripped text of each source it and the shipping projects hold
/// (<see cref="SourceTexts.Code"/>), the driver's compile, the reference map over every shipping source (<see cref="ReferenceMap"/>),
/// and under a scope the compiles of the projects it reaches. A case is timed from its own start, so a case that asks for one of them
/// first would pay for its making, and each that asks beside it the rest of the wait, its queries' work queued behind the making's, and
/// the ratchet would read that wait as the case's own. xUnit 2 has no assembly fixture, so the assembly's runner
/// (<see cref="CensusWarmFramework"/>) makes them once the run has started and before its first class, in a run that holds a census
/// case. The binding stays the semantic classes' fixtures' (<see cref="DesktopCompilationsWarm"/>, <see cref="DriverTreeWarm"/>), whose
/// time is no case's either: made before the first class, its minute would pass with no case started, which risks the run's hang bound,
/// so it begins as the first class does and goes on beside the cases (<see cref="BeginBinding"/>).
/// </summary>
internal static class CensusWarm
{
    private static readonly Lazy<bool> Made = new(() =>
    {
        Parallel.Invoke(
            () => SayDoorSources.All.AsParallel().ForAll(source => _ = SourceTexts.Code(source.Text)),
            () =>
            {
                string[] shipping = [.. DesktopCompilations.Shipping.SelectMany(DesktopCompilations.Sources).Distinct(StringComparer.OrdinalIgnoreCase)
                    .AsParallel().Select(SourceTexts.Of)];
                shipping.AsParallel().ForAll(text => _ = SourceTexts.Code(text));
                // A scope's reach reads the map over every shipping source; with none, the reach's own cases do.
                if (CensusScope.Paths is { } scope) _ = DesktopCompilations.Judged(scope);
                else _ = ReferenceMap.Of(shipping);
            },
            () => _ = DesktopCompilations.Of(SourceFiles.DriverProject));
        return true;
    });

    /// <summary>
    /// How long the run waits for the warm-up before its first class: half the run's hang bound (60 s, <c>VSTestBlameHangTimeout</c> in
    /// <c>Directory.Build.props</c>), whose clock runs from the run's start until a case starts, the rest left for the host's start and its
    /// discovery. Past it the warm-up goes on beside the cases, each waiting for what it reads, as before the warm-up.
    /// </summary>
    internal static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>Whether the run stopped waiting for the warm-up at its <see cref="Patience"/>, so its cases began beside it.</summary>
    internal static bool Late { get; set; }

    /// <summary>Makes the shared reading once; a part that fails is kept by its own reader, so the case that reads it says why.</summary>
    internal static void Make() => _ = Made.Value;

    /// <summary>Whether a run of cases with these <paramref name="traits"/> holds a census case.</summary>
    internal static bool Wanted(IEnumerable<IReadOnlyDictionary<string, List<string>>> traits) =>
        traits.Any(each => each.TryGetValue(Category.Name, out var values) && values.Contains(Category.Census));

    /// <summary>Whether a run of these <paramref name="classes"/> (full names) holds a semantic census, whose fixture binds.</summary>
    internal static bool Binds(IEnumerable<string> classes) =>
        classes.Select(name => name[(name.LastIndexOf('.') + 1)..]).Intersect(CensusReach.Semantic(typeof(CensusWarm).Assembly), StringComparer.Ordinal).Any();

    /// <summary>
    /// Begins the semantic fixtures' binding (<see cref="DesktopCompilationsWarm"/>, <see cref="DriverTreeWarm"/>) beside the cases, each
    /// fixture waiting for it as before: begun with the run rather than at a semantic class's turn, it ends while the other classes' cases
    /// still run, so the hang bound, which a minute of fixtures with no case starting or ending trips, never sees one. A failure is kept
    /// by the fixture's own reader.
    /// </summary>
    internal static void BeginBinding()
    {
        _ = Task.Run(DesktopCompilationsWarm.Bind);
        _ = Task.Run(() => DriverTree.Of(typeof(StructureTests)));
    }
}

/// <summary>xUnit's own framework, its assembly runner making the censuses' shared reading (<see cref="CensusWarm"/>) before the first class runs.</summary>
public sealed class CensusWarmFramework(IMessageSink diagnostics) : XunitTestFramework(diagnostics)
{
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName) =>
        new Executor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);

    private sealed class Executor(AssemblyName assemblyName, ISourceInformationProvider sources, IMessageSink diagnostics)
        : XunitTestFrameworkExecutor(assemblyName, sources, diagnostics)
    {
        protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        {
            using var runner = new Runner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions);
            await runner.RunAsync();
        }
    }

    private sealed class Runner(ITestAssembly assembly, IEnumerable<IXunitTestCase> cases, IMessageSink diagnostics, IMessageSink execution, ITestFrameworkExecutionOptions options)
        : XunitTestAssemblyRunner(assembly, cases, diagnostics, execution, options)
    {
        protected override async Task AfterTestAssemblyStartingAsync()
        {
            await base.AfterTestAssemblyStartingAsync();
            if (!CensusWarm.Wanted(TestCases.Select(each => each.Traits))) return;
            try
            {
                await Task.Run(CensusWarm.Make).WaitAsync(CensusWarm.Patience);
            }
            catch (TimeoutException)
            {
                CensusWarm.Late = true;
                DiagnosticMessageSink.OnMessage(new DiagnosticMessage($"the censuses' shared reading took longer than {CensusWarm.Patience.TotalSeconds} s and goes on beside the cases"));
            }
            catch (Exception failed)
            {
                DiagnosticMessageSink.OnMessage(new DiagnosticMessage($"the censuses' shared reading failed, and each case that reads it says why: {failed.Message}"));
            }

            if (CensusWarm.Binds(TestCases.Select(each => each.TestMethod.TestClass.Class.Name).Distinct())) CensusWarm.BeginBinding();
        }
    }
}
