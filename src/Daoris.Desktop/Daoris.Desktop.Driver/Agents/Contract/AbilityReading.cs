namespace Daoris.Driver;

/// <summary>
/// The one reading of what a door can do: the value its provider states, with its source and the versions it was measured at, else
/// the value its transport gives every door of it, said as a default that was not measured, else absent. The door's callers
/// (<see cref="DoorAbilities"/>) and the provider sheet both read it, so they cannot disagree.
/// </summary>
public static class AbilityReading
{
    /// <summary>The evidence a transport's value carries: it is the transport's, never measured on this door.</summary>
    public const string TransportDefault = "transport default, not measured";

    public static Known<bool> Of(AgentDoor door, string ability)
    {
        var stated = Stated(door.Abilities, ability);
        if (stated.IsKnown) return stated;
        return TransportGives(door, ability) is { } value ? Known<bool>.Declared(value, TransportDefault) : Known<bool>.Absent;
    }

    /// <summary>What the part states of one ability, absent where it states none.</summary>
    public static Known<bool> Stated(AbilitiesPart part, string ability) => ability switch
    {
        AbilityName.Interactive => part.Interactive,
        AbilityName.Resumes => part.Resumes,
        AbilityName.TakesSettings => part.TakesSettings,
        AbilityName.ServersInherit => part.ServersInherit,
        AbilityName.Structured => part.Structured,
        AbilityName.WordsAtNextStep => part.WordsAtNextStep,
        AbilityName.Steering => part.Steering,
        AbilityName.StopKeepsConversation => part.StopKeepsConversation,
        AbilityName.OneTaskStopped => part.OneTaskStopped,
        AbilityName.ResumeFailsClosed => part.ResumeFailsClosed,
        AbilityName.RequestsHeld => part.RequestsHeld,
        AbilityName.ServersOnWire => part.ServersOnWire,
        AbilityName.LeavesOwnServersOut => part.LeavesOwnServersOut,
        _ => throw new ArgumentException($"`{ability}` is not an ability a door states", nameof(ability)),
    };

    /// <summary>Whether the door does it: its stated or defaulted value, false where absent.</summary>
    public static bool Does(AgentDoor door, string ability) => Of(door, ability) is { IsKnown: true, Value: true };

    private static bool? TransportGives(AgentDoor door, string ability)
    {
        var protocol = door.Transport == Transport.Protocol;
        return ability switch
        {
            AbilityName.Resumes => protocol,
            AbilityName.ServersInherit => !protocol,
            AbilityName.Structured => protocol || door.Dialect is { IsKnown: true, Value: INativeDialect },
            AbilityName.WordsAtNextStep => protocol,
            AbilityName.ServersOnWire => protocol,
            AbilityName.RequestsHeld => protocol && door.Postures.Asking is not null,
            _ => null,
        };
    }
}
