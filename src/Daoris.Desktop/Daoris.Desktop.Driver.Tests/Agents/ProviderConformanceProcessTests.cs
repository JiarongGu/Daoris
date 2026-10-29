using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row b, part 2 (the agent-provider design §10): the conformance suite's Process half, a stand-in's program run live, as real
/// model-free processes, one case after another within the class. Its rows are the registry's <c>StandIn</c> providers' doors, never a list of
/// names: a start, a turn, a resume of its own conversation, a missing one and another active writer, a transient error tried again, access
/// and fatal on their paths, a sign-in through the roster's sign-in and login question, and a listing with a command configured; every other
/// live case of §10 is held pending in the stand-in's own <c>pending.json</c> with its row.
/// </summary>
/// <remarks>
/// Until part 3 lays each stand-in's program out in its folder, the program a case runs is a scenario the case writes into its scratch,
/// answering as the rehearsals' stand-in programs answer; nothing here names a stand-in by its id. No model, no account, no variable of the
/// process's environment set. Each class's scratch is under <c>_fixtures/</c>.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ProviderConformanceProcessTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("live");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    public static TheoryData<string, string, string, string> Live => Conformance.Rows(ConformanceHalf.Live);

    [Theory]
    [MemberData(nameof(Live))]
    public async Task Each_stand_in_s_program_answers_live_or_holds_the_case_pending(string source, string provider, string door, string @case)
    {
        if (await Conformance.RunAsync(source, provider, door, @case, _scratch) is { } held) output.WriteLine(held);
    }
}
