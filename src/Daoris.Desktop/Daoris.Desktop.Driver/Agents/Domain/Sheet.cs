namespace Daoris.Driver;

/// <summary>
/// A door's sheet read from the contract: each capability from the door's declaration,
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
    public static AgentCapabilities Of(Provider provider, AgentDoor door, IReadOnlyDictionary<string, bool>? negotiated)
    {
        var own = $"{door.Id.Value}'s declaration";

        // A measured value outranks an opening's answer, which outranks a declaration and the transport's default.
        SheetRow Known(string capability, string ability, string fallback)
        {
            var stated = AbilityReading.Of(door, ability);
            if (stated is { IsKnown: true, By: KnownBy.Measured })
            {
                return new(capability, YesNo(stated.Required), SheetRow.Measured, stated.Evidence, AppliesOf(stated), fallback);
            }

            if (negotiated is not null && negotiated.TryGetValue(capability, out var answered))
            {
                return new(capability, YesNo(answered), SheetRow.Negotiated, $"{door.Id.Value}'s opening", Known<bool>.Anywhere, fallback);
            }

            return stated.IsKnown
                ? new(capability, YesNo(stated.Required), SheetRow.Declared, stated.Evidence, stated.Applies, fallback)
                : Absent(capability, fallback);
        }

        var postures = new[] { door.Postures.Ordinary, door.Postures.Asking, door.Postures.Full }.OfType<string>().ToList();
        var rows = new List<SheetRow>
        {
            new(SheetCapability.Door, TransportWord(door.Transport), SheetRow.Declared, own, Known<bool>.Anywhere,
                "none: without a door it is no agent"),
            Known(SheetCapability.Conversation, AbilityName.Interactive,
                "driven one-turn parts only: a partner, never a conversation's lead"),
            Known(SheetCapability.Resume, AbilityName.Resumes, "a new part handed the brief"),
            Known(SheetCapability.WordsAtNextStep, AbilityName.WordsAtNextStep, "words wait for the turn's end, said so"),
            Known(SheetCapability.Steering, AbilityName.Steering, "now waits, said no-interrupt"),
            postures.Count > 0
                ? new(SheetCapability.Postures, string.Join(" · ", postures), SheetRow.Declared, own, Known<bool>.Anywhere,
                    "a full or asking start is refused naming the agent")
                : Absent(SheetCapability.Postures, "a full or asking start is refused naming the agent"),
            Known(SheetCapability.TakesRules, AbilityName.TakesSettings,
                "the copy is the floor: the person's denies do not reach it"),
            Known(SheetCapability.HeldRequests, AbilityName.RequestsHeld, "refused under any other posture"),
            Known(SheetCapability.ServersOnWire, AbilityName.ServersOnWire, "handed in a file on its command line"),
            Absent(SheetCapability.Confinement, "not admitted as a partner"),
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

    private static string AppliesOf(Known<bool> measured) => measured.At is { } at ? $"{at.Said}, {measured.Applies}" : measured.Applies;

    private static string YesNo(bool value) => value ? SheetRow.Yes : SheetRow.No;

    private static SheetRow Absent(string capability, string fallback) =>
        new(capability, null, SheetRow.Absent, null, Known<bool>.Anywhere, fallback);
}
