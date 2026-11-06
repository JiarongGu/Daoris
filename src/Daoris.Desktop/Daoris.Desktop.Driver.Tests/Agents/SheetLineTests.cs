using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>COAGENT1e: the sheet's line under an installed agent in <c>daoris agent list</c>.</summary>
public sealed class SheetLineTests
{
    private static SheetRow Row(string capability, string? value, string source) =>
        new(capability, value, source, source == SheetRow.Absent ? null : "evidence", "any", "a fallback");

    [Fact]
    public void Each_capability_is_said_with_its_value_and_an_absent_one_as_not_known()
    {
        var sheet = new AgentCapabilities("door", AgentCapabilities.Pipe,
        [
            Row(SheetCapability.Door, "native", SheetRow.Declared),
            Row(SheetCapability.Conversation, "yes", SheetRow.Declared),
            Row(SheetCapability.Resume, "no", SheetRow.Declared),
            Row(SheetCapability.WordsAtNextStep, "yes", SheetRow.Negotiated),
            Row(SheetCapability.Steering, null, SheetRow.Absent),
            Row(SheetCapability.Postures, "ordinary, asking, full", SheetRow.Declared),
            Row(SheetCapability.TakesRules, "yes", SheetRow.Declared),
            Row(SheetCapability.HeldRequests, "no", SheetRow.Measured),
            Row(SheetCapability.ServersOnWire, "no", SheetRow.Declared),
            Row(SheetCapability.Confinement, null, SheetRow.Absent),
        ]);

        Assert.Equal(
            "what it can do: conversation yes, resume no, words at the next step yes, steering not known, postures ordinary, asking, full, "
            + "takes Daoris's rules yes, held requests no, servers on its wire no, confinement not known",
            SheetLine.Of(sheet));
    }

    [Fact]
    public void An_agent_s_line_is_its_own_door_s_sheet()
    {
        var facts = ProviderRegistry.Of(AdapterSet.Built()).Facts;

        Assert.Equal(
            "what it can do: conversation yes, resume yes, words at the next step no, steering not known, postures default · bypassPermissions, "
            + "takes Daoris's rules yes, held requests no, servers on its wire no, confinement not known",
            SheetLine.Of(facts.Of("claude-code")!.Door("claude-code")!));
    }
}
