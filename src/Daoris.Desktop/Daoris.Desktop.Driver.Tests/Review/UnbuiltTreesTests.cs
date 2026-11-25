using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A regenerate that fails because the work does not build with the line is the work's failure: its compiler's errors are read from its
/// output, each once and relative to the verification tree, the warnings around them left out; an output with none is no build failure.
/// </summary>
public sealed class UnbuiltTreesTests
{
    private const string Tree = @"D:\home\workflows\lines\trees\a516e709f3775c64";

    [Fact]
    public void A_build_s_compiler_errors_are_read_once_each_relative_to_the_tree_and_its_warnings_left_out()
    {
        var output = string.Join('\n',
            $@"{Tree}\src\Driver\Context\AgentContextWiring.cs(7,6): warning CA2255: The 'ModuleInitializer' attribute is only intended [{Tree}\src\Driver\Driver.csproj]",
            $@"{Tree}\src\Driver\Review\Heavy\LoadFailures.cs(14,91): error CS0117: 'CheckAdmission' does not contain a definition for 'Need' [{Tree}\src\Driver\Driver.csproj]",
            $@"{Tree}\src\Driver\Review\Heavy\LoadFailures.cs(76,66): error CS0117: 'CheckAdmission' does not contain a definition for 'Need' [{Tree}\src\Driver\Driver.csproj]",
            "    119 Warning(s)",
            "    2 Error(s)",
            $@"{Tree}\src\Driver\Review\Heavy\LoadFailures.cs(14,91): error CS0117: 'CheckAdmission' does not contain a definition for 'Need' [{Tree}\src\Driver\Driver.csproj]",
            "src/web/api.ts(3,1): error TS2339: Property 'need' does not exist on type 'Admission'.",
            "src/web/load.ts:9:4 - error TS18048: 'admission' is possibly 'undefined'.",
            $@"{Tree}\src\Legacy\Module1.vb(12,5): error BC30451: 'Need' is not declared. [{Tree}\src\Legacy\Legacy.vbproj]",
            "contracts: the generator failed; its own words are above");

        Assert.Equal(
            [
                @"src\Driver\Review\Heavy\LoadFailures.cs(14,91): error CS0117: 'CheckAdmission' does not contain a definition for 'Need'",
                @"src\Driver\Review\Heavy\LoadFailures.cs(76,66): error CS0117: 'CheckAdmission' does not contain a definition for 'Need'",
                "src/web/api.ts(3,1): error TS2339: Property 'need' does not exist on type 'Admission'.",
                "src/web/load.ts:9:4 - error TS18048: 'admission' is possibly 'undefined'.",
                @"src\Legacy\Module1.vb(12,5): error BC30451: 'Need' is not declared.",
            ],
            UnbuiltTrees.Errors(output, Tree));
    }

    [Theory]
    [InlineData("npm ERR! code ECONNRESET\nnpm ERR! network aborted")]
    [InlineData(@"C:\Program Files\dotnet\sdk\Microsoft.Common.targets(5,5): error MSB3027: Could not copy ""obj\Driver.dll"": the file is locked by: ""Daoris (1234)""")]
    [InlineData(@"D:\repo\Driver.csproj : error NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json.")]
    [InlineData("contracts would not build: tsc is not recognized")]
    [InlineData(@"CSC : error CS2012: Cannot open 'D:\repo\obj\Debug\net10.0\Driver.dll' for writing -- 'The process cannot access the file because it is being used by another process.' [D:\repo\Driver.csproj]")]
    [InlineData(@"CSC : error CS0016: Could not write to output file 'D:\repo\obj\Debug\net10.0\Driver.pdb' -- 'Access is denied.'")]
    public void An_output_with_no_compiler_error_is_no_build_failure_of_the_work(string output) => Assert.Empty(UnbuiltTrees.Errors(output, Tree));

    [Fact]
    public void Only_the_first_errors_are_kept()
    {
        var output = string.Join('\n', Enumerable.Range(1, UnbuiltTrees.Most + 5).Select(line => $"a.cs({line},1): error CS1002: ; expected"));

        Assert.Equal(UnbuiltTrees.Most, UnbuiltTrees.Errors(output, Tree).Count);
    }
}
