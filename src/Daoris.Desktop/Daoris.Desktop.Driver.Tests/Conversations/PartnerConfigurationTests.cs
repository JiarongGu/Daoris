using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199 point 4; the co-agent design §3.3, §7.4): the partner's role as its start hands it. Its rules, for an agent that takes
/// them, offer the reading tools, its lead's line and its answer, and deny every other connector tool and a push; its effective
/// configuration names its posture, its copy alone, no network named and the digest of what Daoris passes, alike for every piece's copy.
/// </summary>
public sealed class PartnerConfigurationTests
{
    private const string Connector = $"mcp__{KnowledgeConnector.ServerName}__";

    private static readonly RuleLists Person = new(
        ["Bash(npm test:*)", Connector + "quest_publish", Connector + "knowledge_search"], [], ["Bash(rm -rf:*)"]);

    /// <summary>The offered and the denied together are every tool the connector has, each once (the service holds that list to its host).</summary>
    [Fact]
    public void A_partners_rules_classify_every_connector_tool_once()
    {
        Assert.Empty(PartnerPermissions.Offered.Intersect(PartnerPermissions.Acting));
        Assert.Equal(
            BrainstormPermissions.ConnectorTools.Order(StringComparer.Ordinal),
            PartnerPermissions.Offered.Concat(PartnerPermissions.Acting).Order(StringComparer.Ordinal));
    }

    /// <summary>🔴 The person's allow of an acting tool is taken out and denied; the reading, the line and the answer are offered; a push denied.</summary>
    [Fact]
    public void A_partners_rules_deny_the_acting_tools_and_a_push_over_the_persons()
    {
        var shaped = PartnerPermissions.Shape(Person);

        Assert.DoesNotContain(Connector + "quest_publish", shaped.Allow);
        Assert.Contains(Connector + "quest_publish", shaped.Deny);
        Assert.Contains(Connector + "helper_ask", shaped.Deny);
        Assert.Contains(Connector + "partner_answer", shaped.Allow);
        Assert.Contains(Connector + "line_read", shaped.Allow);
        Assert.Contains("Bash(npm test:*)", shaped.Allow);
        Assert.Contains("Bash(rm -rf:*)", shaped.Deny);
        Assert.Contains("Bash(git push:*)", shaped.Deny);
        Assert.Equal(shaped.Deny.Count, shaped.Deny.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The configuration of an agent that takes no rules (Codex): its own posture, its copy alone, the harness's network default, and a
    /// digest that is the same for every piece's copy, since nothing it is handed names the copy.
    /// </summary>
    [Fact]
    public void An_agent_that_takes_no_rules_digests_alike_for_every_copy()
    {
        var codex = AdapterSet.Built().Resolve("codex-acp");
        var one = PartnerConfigurations.Of(codex, "0.162.0", "D:/home/trees/default/reports/p-pc000001", PartnerPermissions.Shape(Person), ["x"], guarded: true);
        var two = PartnerConfigurations.Of(codex, "0.162.0", "D:/home/trees/default/reports/p-pc000002", PartnerPermissions.Shape(RuleLists.Empty), [], guarded: false);

        Assert.Equal(("codex-acp", "agent", PartnerConfigurations.CopyAlone, PartnerConfigurations.HarnessDefault), (one.Agent, one.Posture, one.WritableRoots, one.Network));
        Assert.Equal(one, two);
        Assert.Matches("^[0-9a-f]{64}$", one.SettingsDigest);
    }

    /// <summary>
    /// For an agent that takes rules, the digest reads the rules as handed: the copy's path in either slash read as its shape, any order
    /// alike, and a rule, a hard denial, the guard or the posture changed reads as another configuration.
    /// </summary>
    [Fact]
    public void The_digest_is_the_rules_handed_with_the_copy_read_as_its_shape()
    {
        RuleLists Guarded(string copy, params string[] more) => new([$"Read({copy}/**)", .. more], [], ["Bash(git push:*)"]);
        var first = PartnerConfigurations.Digest("acceptEdits", "D:/trees/p-a", Guarded("D:/trees/p-a", "Bash(npm test:*)", "Edit"), ["no"], true);

        Assert.Equal(first, PartnerConfigurations.Digest("acceptEdits", "D:\\trees\\p-b", Guarded("D:/trees/p-b", "Edit", "Bash(npm test:*)"), ["no"], true));
        Assert.NotEqual(first, PartnerConfigurations.Digest("acceptEdits", "D:/trees/p-a", Guarded("D:/trees/p-a", "Edit"), ["no"], true));
        Assert.NotEqual(first, PartnerConfigurations.Digest("acceptEdits", "D:/trees/p-a", Guarded("D:/trees/p-a", "Bash(npm test:*)", "Edit"), [], true));
        Assert.NotEqual(first, PartnerConfigurations.Digest("acceptEdits", "D:/trees/p-a", Guarded("D:/trees/p-a", "Bash(npm test:*)", "Edit"), ["no"], false));
        Assert.NotEqual(first, PartnerConfigurations.Digest("default", "D:/trees/p-a", Guarded("D:/trees/p-a", "Bash(npm test:*)", "Edit"), ["no"], true));
    }
}
