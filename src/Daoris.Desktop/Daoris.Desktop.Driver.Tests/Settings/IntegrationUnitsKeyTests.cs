using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The digest of each unit of the declaration a repository's owner accepted, as <c>driver.json</c> keeps it beside the acceptance's
/// blob: read as a stored shape, written only where set, and ended with the acceptance it comes from.
/// </summary>
public sealed class IntegrationUnitsKeyTests
{
    private const string Blob = "0123456789abcdef0123456789abcdef01234567";
    private const string Other = "89abcdef0123456789abcdef0123456789abcdef";

    private static readonly Dictionary<string, string> Units = new() { ["gate:build"] = new string('a', 64), ["order"] = "gate:build\ngate:tools" };

    [Fact]
    public void A_record_is_read_back_as_it_was_written_and_written_only_where_set()
    {
        var config = DriverConfig.Empty.WithIntegrationGates("engine", Blob).WithIntegrationUnits("engine", new AcceptedUnits(Blob, Units));

        var read = DriverConfig.Parse(config.ToJson());

        var record = read.IntegrationUnits["engine"];
        Assert.Equal(Blob, record.Blob);
        Assert.Equal(Units, record.Units);
        Assert.DoesNotContain("integrationUnits", DriverConfig.Empty.ToJson());
        Assert.DoesNotContain("integrationUnits", config.WithIntegrationUnits("engine", null).ToJson());
    }

    /// <summary>A record is the units of the blob it names: the acceptance moved to another blob, or cleared, ends it.</summary>
    [Fact]
    public void The_record_ends_with_the_acceptance_it_was_computed_from()
    {
        var config = DriverConfig.Empty.WithIntegrationGates("engine", Blob).WithIntegrationUnits("engine", new AcceptedUnits(Blob, Units));

        Assert.Single(config.WithIntegrationGates("engine", Blob).IntegrationUnits);
        Assert.Empty(config.WithIntegrationGates("engine", Other).IntegrationUnits);
        Assert.Empty(config.WithIntegrationGates("engine", "none").IntegrationUnits);
        Assert.Empty(config.WithIntegrationGates("engine", null).IntegrationUnits);
        Assert.Empty(DeclarationAcceptance.Withdraw(TestHomes.Root, config, "engine", null).IntegrationUnits);
    }

    [Fact]
    public void A_record_of_another_blob_than_the_acceptance_or_that_is_no_blob_is_refused_by_the_setter()
    {
        var config = DriverConfig.Empty.WithIntegrationGates("engine", Blob);

        Assert.Throws<DriverException>(() => config.WithIntegrationUnits("engine", new AcceptedUnits(Other, Units)));
        Assert.Throws<DriverException>(() => config.WithIntegrationUnits("engine", new AcceptedUnits("HEAD", Units)));
        Assert.Throws<DriverException>(() => config.WithIntegrationUnits("rover", new AcceptedUnits(Blob, Units)));
    }

    [Fact]
    public void A_map_keeps_the_first_readable_record_of_a_name_and_passes_over_a_shape_it_does_not_know()
    {
        const string Text = """
            {"integrationUnits":{
              " a ":{"blob":"HEAD","units":{"x":"1"}},
              "A":{"blob":"BLOB","units":{"gate:x":"1","order":"gate:x"}},
              "a ":{"blob":"OTHER","units":{"gate:y":"2"}},
              "b":7,
              "c":{"blob":"BLOB","units":{"gate:x":3}},
              "d":{"blob":"none","units":{}},
              "e":{"blob":"BLOB"},
              "":{"blob":"BLOB","units":{}}}}
            """;
        using var document = JsonDocument.Parse(Text.Replace("BLOB", Blob, StringComparison.Ordinal).Replace("OTHER", Other, StringComparison.Ordinal));

        var map = IntegrationUnitsKey.Map(document.RootElement);

        Assert.Equal(["A"], map.Keys);
        Assert.Equal(Blob, map["a"].Blob);
        Assert.Equal(["gate:x", "order"], map["a"].Units.Keys.Order(StringComparer.Ordinal));
    }
}
