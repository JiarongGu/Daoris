namespace Daoris.Driver;

/// <summary>
/// A door's sheet read from the contract (COAGENT1 §3; the agent-provider design §2.4): each capability from the door's declaration,
/// its transport's, an opening's negotiation or a measurement, in that order of strength, else absent with its fallback. Pure: it
/// reads what it is handed and keeps nothing; the measurements table stays its own record.
/// </summary>
public static class Sheet
{
    /// <summary>
    /// One door's sheet.
    /// </summary>
    /// <param name="provider">The provider the door belongs to.</param>
    /// <param name="door">The door.</param>
    /// <param name="negotiated">What an opening of the door answered, each ability by its sheet name; null where none is kept.</param>
    /// <param name="measurements">The measurements table; only the door's own rows are read.</param>
    public static AgentCapabilities Of(
        Provider provider, AgentDoor door, IReadOnlyDictionary<string, bool>? negotiated, IReadOnlyList<Measurement> measurements)
    {
        var abilities = DoorAbilities.Of(door);
        var protocol = door.Transport == Transport.Protocol;
        var transport = protocol ? "every protocol door" : "every native door";
        var own = $"{door.Id.Value}'s declaration";

        SheetRow Known(string capability, Known<bool> declared, bool? given, string fallback)
        {
            if (Measured(capability) is { } measured) return measured with { Fallback = fallback };
            if (negotiated is not null && negotiated.TryGetValue(capability, out var answered))
            {
                return new(capability, YesNo(answered), SheetRow.Negotiated, $"{door.Id.Value}'s opening", Known<bool>.Anywhere, fallback);
            }

            if (declared.IsKnown) return new(capability, YesNo(declared.Required), SheetRow.Declared, declared.Evidence, declared.Applies, fallback);
            return given is { } value
                ? new(capability, YesNo(value), SheetRow.Declared, transport, Known<bool>.Anywhere, fallback)
                : Absent(capability, fallback);
        }

        SheetRow? Measured(string capability) =>
            measurements.LastOrDefault(row => Ids.Same(row.Door, door.Id.Value) && row.Capability == capability) is { } row
                ? new(capability, row.Outcome, SheetRow.Measured, row.Evidence, row.Applies, "")
                : null;

        var postures = new[] { door.Postures.Ordinary, door.Postures.Asking, door.Postures.Full }.OfType<string>().ToList();
        var rows = new List<SheetRow>
        {
            new(SheetCapability.Door, TransportWord(door.Transport), SheetRow.Declared, own, Known<bool>.Anywhere,
                "none: without a door it is no agent"),
            Known(SheetCapability.Conversation, door.Abilities.Interactive, null,
                "driven one-turn parts only: a partner, never a conversation's lead"),
            Known(SheetCapability.Resume, door.Abilities.Resumes, abilities.Resumes, "a new part handed the brief"),
            Known(SheetCapability.WordsAtNextStep, default, abilities.WordsAtNextStep, "words wait for the turn's end, said so"),
            Known(SheetCapability.Steering, default, null, "now waits, said no-interrupt"),
            postures.Count > 0
                ? new(SheetCapability.Postures, string.Join(" · ", postures), SheetRow.Declared, own, Known<bool>.Anywhere,
                    "a full or asking start is refused naming the agent")
                : Absent(SheetCapability.Postures, "a full or asking start is refused naming the agent"),
            Known(SheetCapability.TakesRules, door.Abilities.TakesSettings, null,
                "the copy is the floor: the person's denies do not reach it"),
            Known(SheetCapability.HeldRequests, default, protocol && door.Postures.Asking is not null, "refused under any other posture"),
            Known(SheetCapability.ServersOnWire, default, abilities.ServersOnWire, "handed in a file on its command line"),
            Measured(SheetCapability.Confinement) is { } confined
                ? confined with { Fallback = "not admitted as a partner" }
                : Absent(SheetCapability.Confinement, "not admitted as a partner"),
        };
        return new AgentCapabilities(door.Id.Value, TransportWord(door.Transport), rows);
    }

    /// <summary>A transport's word on the sheet and the page: <c>protocol</c> or <c>native</c>.</summary>
    public static string TransportWord(Transport transport) => transport.ToString().ToLowerInvariant();

    /// <summary>
    /// What a person can do with an agent's accounts, by its pool's flows, and whether Daoris installs it, by its leading door's program,
    /// else its own.
    /// </summary>
    public static AgentActs ActsOf(Provider provider, AgentDoor? lead)
    {
        var pool = provider.Accounts.IsKnown ? provider.Accounts.Required.Facts : PoolFacts.None;
        var program = lead is { Program.IsKnown: true } ? lead.Program : provider.Program;
        var signsIn = pool.LoginArguments is { Count: > 0 };
        return new AgentActs(
            signsIn,
            pool.KeyVariable is { Length: > 0 },
            signsIn && pool.DeviceLoginArguments is { Count: > 0 },
            program.IsKnown && program.Required.Install is { Count: > 0 });
    }

    private static string YesNo(bool value) => value ? SheetRow.Yes : SheetRow.No;

    private static SheetRow Absent(string capability, string fallback) =>
        new(capability, null, SheetRow.Absent, null, Known<bool>.Anywhere, fallback);
}
