namespace Daoris.Driver;

/// <summary>
/// A door's sheet as <c>daoris agent list</c> says it (COAGENT1e; the agent-provider design §6): one line under an installed agent,
/// each capability with its value and, where it is not known, that it is not: a fallback applies there, and silence is never read as
/// support. The door's own row is the name the list already prints, so it is left out.
/// </summary>
internal static class SheetLine
{
    private static readonly (string Capability, string Words)[] Labels =
    [
        (SheetCapability.Conversation, "conversation"),
        (SheetCapability.Resume, "resume"),
        (SheetCapability.WordsAtNextStep, "words at the next step"),
        (SheetCapability.Steering, "steering"),
        (SheetCapability.Postures, "postures"),
        (SheetCapability.TakesRules, "takes Daoris's rules"),
        (SheetCapability.HeldRequests, "held requests"),
        (SheetCapability.ServersOnWire, "servers on its wire"),
        (SheetCapability.Confinement, "confinement"),
    ];

    /// <summary>The line, without its indent: <c>what it can do: conversation yes, resume yes, steering not known, …</c>.</summary>
    public static string Of(AgentCapabilities sheet) =>
        "what it can do: " + string.Join(", ", Labels.Select(label =>
        {
            var row = sheet.Row(label.Capability);
            return $"{label.Words} {(row.Source == SheetRow.Absent ? "not known" : row.Value)}";
        }));
}
