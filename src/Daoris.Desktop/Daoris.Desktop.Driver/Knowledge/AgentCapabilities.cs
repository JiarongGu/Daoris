namespace Daoris.Driver;

/// <summary>
/// One door's sheet: what a session on it can do, each capability with its value and the
/// source it is known by, or absent with the fallback that applies. Plain values, read by the roster's answer, the terminal's list and
/// the room; <c>Sheet.Of</c> makes it from the contract, a pure read.
/// </summary>
/// <param name="Door">The door's id.</param>
/// <param name="Transport">Its transport: <c>protocol</c> or <c>native</c>.</param>
/// <param name="Rows">Each capability of <see cref="SheetCapability.All"/>, in that order.</param>
public sealed record AgentCapabilities(string Door, string Transport, IReadOnlyList<SheetRow> Rows)
{
    /// <summary>The row of a capability; every capability of the sheet has one.</summary>
    public SheetRow Row(string capability) =>
        Rows.FirstOrDefault(row => row.Capability == capability)
        ?? throw new ArgumentException($"the sheet has no capability `{capability}`", nameof(capability));

    /// <summary>Whether the door is known to do it: a row known as <c>yes</c>, whatever its source; absent never reads as support.</summary>
    public bool Does(string capability) => Row(capability) is { Source: not SheetRow.Absent, Value: SheetRow.Yes };
}

/// <summary>
/// One capability of a sheet: its value and the source it is known by, the evidence and where it applies, or absent.
/// Absent is a statement: its <see cref="Fallback"/> applies, and silence never reads as support.
/// </summary>
/// <param name="Capability">Its closed name, one of <see cref="SheetCapability"/>'s.</param>
/// <param name="Value">What is known (<c>yes</c>, <c>no</c>, or the agent's own words), or null where absent.</param>
/// <param name="Source"><see cref="Declared"/>, <see cref="Negotiated"/>, <see cref="Measured"/> or <see cref="Absent"/>.</param>
/// <param name="Evidence">What says so: the declaration, the opening, or the measurement's evidence document; null where absent.</param>
/// <param name="Applies">The configuration it applies to; <c>any</c> for a declaration.</param>
/// <param name="Fallback">What happens where it is absent or <c>no</c>, in the sheet's words.</param>
public sealed record SheetRow(string Capability, string? Value, string Source, string? Evidence, string Applies, string Fallback)
{
    public const string Declared = "declared";
    public const string Negotiated = "negotiated";
    public const string Measured = "measured";
    public const string Absent = "absent";
    public const string Yes = "yes";
    public const string No = "no";
}

/// <summary>The sheet's capabilities by their closed names, in the order a sheet lists them.</summary>
public static class SheetCapability
{
    /// <summary>Its transport: <c>protocol</c> or <c>native</c>.</summary>
    public const string Door = "door";

    /// <summary>It holds a conversation; else it drives one-turn parts only, a partner and never a conversation's lead.</summary>
    public const string Conversation = "conversation";

    /// <summary>A kept conversation can be resumed on it; else a new part is handed the brief.</summary>
    public const string Resume = "resume";

    /// <summary>A person's words reach the running session at its next step; else they wait for the turn's end.</summary>
    public const string WordsAtNextStep = "words-at-next-step";

    /// <summary>Its turn can be steered now; else *now* waits.</summary>
    public const string Steering = "steering";

    /// <summary>Its postures in its own words: ordinary, asking and full.</summary>
    public const string Postures = "postures";

    /// <summary>It takes Daoris's rules, so the person's denies reach it; else the copy is the floor.</summary>
    public const string TakesRules = "takes-rules";

    /// <summary>A request it holds reaches the person, under the asking posture.</summary>
    public const string HeldRequests = "held-requests";

    /// <summary>Its connector servers ride the session's wire; else they are handed in a file.</summary>
    public const string ServersOnWire = "servers-on-wire";

    /// <summary>A partner's confinement, measured only.</summary>
    public const string Confinement = "confinement";

    public static IReadOnlyList<string> All { get; } =
        [Door, Conversation, Resume, WordsAtNextStep, Steering, Postures, TakesRules, HeldRequests, ServersOnWire, Confinement];
}
