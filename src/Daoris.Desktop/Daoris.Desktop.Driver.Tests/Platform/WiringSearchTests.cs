namespace Daoris.Driver.Tests.Platform;

/// <summary>
/// What the wiring census reads as a source (<see cref="WiringVariables.Searched"/>): code, never a test's scratch. The CLI package's
/// tests write the rehearsal's stub agent under their <c>_fixtures/</c> while CI's driver gate runs beside them, and the census once
/// read it as a source (2026-10-24). Apart from <c>SystemWiringTests</c>, which deletes scratch of its own.
/// </summary>
public sealed class WiringSearchTests
{
    [Theory]
    [InlineData("Daoris.Web/src/app/main.ts", true)]
    [InlineData("Daoris.Web/_fixtures/deployment-rehearsal-acp-stub/acp-agent.mjs", false)]
    [InlineData("Daoris.Web/fixtures/stub.mjs", false)]
    public void The_search_reads_code_and_never_a_tests_scratch(string relative, bool searched) =>
        Assert.Equal(searched, WiringVariables.Searched(relative));
}
