using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row b, part 2 (the agent-provider design §10, §4.6): the conformance suite's spawn cases, which plant sentinel credential and
/// home variables in the process's environment and read what each door's spawn inherits of them. Registering a provider keeps in every other
/// provider's spawn each variable that provider declares (row b's, run); a key only in its own spawn, two providers sharing one variable and a
/// key nobody else declares stripped from another's spawn are s-vi's, written here and held pending until s-vi strips them.
/// </summary>
/// <remarks>
/// It sets the process's environment, which every class beside it reads and every child they start inherits, so it runs alone, after the rest
/// (<see cref="Alone"/>). It starts no process: a spawn is read as prepared, never run.
/// </remarks>
[Collection(Alone.Name)]
public sealed class ProviderConformanceEnvironmentTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _scratch = ConformanceScratch.New("environment");

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    public static TheoryData<string, string, string, string> Planted => Conformance.Rows(ConformanceHalf.Environment);

    [Theory]
    [MemberData(nameof(Planted))]
    public async Task Each_door_s_spawn_keeps_what_its_provider_declares_or_holds_the_case_pending(string source, string provider, string door, string @case)
    {
        if (await Conformance.RunAsync(source, provider, door, @case, _scratch) is { } held) output.WriteLine(held);
    }
}
