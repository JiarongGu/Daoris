using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The integration declaration each repository's owner accepted, as <c>driver.json</c> keeps it (INTEGRATE1d, D215 point 2): the
/// settings' own reading, and the setter the Accept door will call, which keeps only what the landed route's acceptance would.
/// </summary>
public sealed class IntegrationGatesKeyTests
{
    private const string Blob = "0123456789abcdef0123456789abcdef01234567";

    [Theory]
    [InlineData(Blob, true)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", true)]
    [InlineData("none", true)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF01234567", false)]
    [InlineData("0123456", false)]
    [InlineData("HEAD", false)]
    [InlineData(" none", false)]
    [InlineData("", false)]
    public void An_acceptance_is_a_whole_blob_id_or_none_as_the_landed_route_keeps_one(string value, bool kept)
    {
        Assert.Equal(kept, IntegrationGatesKey.IsAcceptance(value));
        Assert.Equal(WorkflowKey.IsGates(value), IntegrationGatesKey.IsAcceptance(value));
    }

    [Fact]
    public void A_map_keeps_the_first_readable_acceptance_of_a_name_in_its_own_spelling()
    {
        using var document = JsonDocument.Parse("""{"integrationGates":{" a ":"HEAD","A":"BLOB","a ":"none","b":7,"":"none"}}""".Replace("BLOB", Blob, StringComparison.Ordinal));

        var map = IntegrationGatesKey.Map(document.RootElement);

        Assert.Equal(["A"], map.Keys);
        Assert.Equal(Blob, map["a"]);
    }

    [Fact]
    public void The_setter_refuses_what_it_would_not_read_and_keeps_the_repository_s_spelling()
    {
        var config = DriverConfig.Empty.WithIntegrationGates(" Engine ", Blob);

        Assert.Equal(["Engine"], config.IntegrationGates.Keys);
        Assert.Equal("none", config.WithIntegrationGates("ENGINE", "none").IntegrationGates["Engine"]);
        Assert.Empty(config.WithIntegrationGates("engine", null).IntegrationGates);
        Assert.Equal(IntegrationGatesKey.Problem, Assert.Throws<DriverException>(() => config.WithIntegrationGates("engine", "HEAD")).Message);
        Assert.Throws<DriverException>(() => config.WithIntegrationGates("  ", Blob));
        Assert.Contains("integrationGates", DriverConfig.Parse(config.ToJson()).ToJson());
        Assert.Equal(Blob, DriverConfig.Parse(config.ToJson()).IntegrationGates["engine"]);
    }
}
