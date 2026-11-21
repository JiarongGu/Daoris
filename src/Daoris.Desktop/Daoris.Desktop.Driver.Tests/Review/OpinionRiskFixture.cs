using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The risk policy a test writes into a home's <c>driver.json</c> beside what its fixture saved, for the second opinion's dispute (<c>work.accept</c>)</summary>
internal static class OpinionRiskFixture
{
    /// <summary>Classes <c>work.accept</c> at the machine, for a test that reads a dispute or commits nobody read as waiting; the seed passes it.</summary>
    public static void ClassAccept(string home, string riskClass = RiskClasses.Owner)
    {
        var path = DriverConfig.ResolvePath(home);
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        node[RiskPolicy.Key] = JsonNode.Parse($$"""{"machine":[{"kind":"{{RiskActs.WorkAccept}}","class":"{{riskClass}}"}]}""");
        File.WriteAllText(path, node.ToJsonString());
    }
}
