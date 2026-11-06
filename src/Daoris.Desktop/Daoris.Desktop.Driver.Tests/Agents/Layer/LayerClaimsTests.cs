using Daoris.Driver;
using Lyntai.Agents;
using LyntaiDoor = Lyntai.Agents.AgentDoor;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The claims' translation (the agent-lyntai design §4.3): a layer claim is read into a <see cref="Known{T}"/>, never declared beside it.
/// Supported reads true with its source, unsupported false, unknown stated absent; the source maps one to one, the evidence is carried
/// verbatim, and what it applies to names the door and the claim's one version. Proven on a door the tests define.
/// </summary>
public sealed class LayerClaimsTests
{
    private static readonly LyntaiDoor Door = new()
    {
        Id = "acp",
        Kind = AgentDoorKinds.Acp,
        Abilities =
        [
            new AgentAbility(AgentAbilities.Resume, AgentSupport.Supported, AgentAbilitySource.Negotiated) { Evidence = "session/load in the answer", AppliesTo = "1.4.0" },
            new AgentAbility(AgentAbilities.Steering, AgentSupport.Unsupported, AgentAbilitySource.Declared) { Evidence = "the door takes no word mid-turn" },
            new AgentAbility(AgentAbilities.PlanEvents, AgentSupport.Unknown, AgentAbilitySource.Measured),
        ],
    };

    [Fact]
    public void A_supported_claim_reads_true_with_its_source_its_evidence_and_where_it_applies()
    {
        Assert.Equal(
            new Known<bool>(true, KnownBy.Negotiated, "session/load in the answer", "tests-door 1.4.0", IsKnown: true),
            LayerClaims.Of(Door, "tests-door", AgentAbilities.Resume));
    }

    [Fact]
    public void An_unsupported_claim_reads_false_and_applies_to_the_door_where_it_names_no_version()
    {
        Assert.Equal(
            new Known<bool>(false, KnownBy.Declared, "the door takes no word mid-turn", "tests-door", IsKnown: true),
            LayerClaims.Of(Door, "tests-door", AgentAbilities.Steering));
    }

    [Fact]
    public void An_unknown_claim_and_one_the_door_never_names_are_stated_absent()
    {
        Assert.False(LayerClaims.Of(Door, "tests-door", AgentAbilities.PlanEvents).IsKnown);
        Assert.False(LayerClaims.Of(Door, "tests-door", AgentAbilities.LiveUsage).IsKnown);
    }

    [Theory]
    [InlineData(AgentAbilitySource.Declared, KnownBy.Declared)]
    [InlineData(AgentAbilitySource.Negotiated, KnownBy.Negotiated)]
    [InlineData(AgentAbilitySource.Measured, KnownBy.Measured)]
    public void The_source_maps_one_to_one(AgentAbilitySource source, KnownBy by)
    {
        var door = Door with { Abilities = [new AgentAbility(AgentAbilities.Model, AgentSupport.Supported, source)] };

        Assert.Equal(by, LayerClaims.Of(door, "tests-door", AgentAbilities.Model).By);
    }
}
