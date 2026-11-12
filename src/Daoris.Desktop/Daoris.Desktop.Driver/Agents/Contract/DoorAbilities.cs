namespace Daoris.Driver;

/// <summary>
/// What a door can do, as its callers read it: each ability the door declares, else what its
/// transport gives every door of it. A caller asks the ability it acts on, never which transport the door rides.
/// </summary>
/// <remarks>
/// Interactive is always the door's declaration, never the transport's: <c>dsh</c> declares none and is refused a conversation.
/// </remarks>
/// <param name="Transport">The transport the door rides; read by the transports alone.</param>
/// <param name="Interactive">It holds a conversation (the door's declaration).</param>
/// <param name="Resumes">A resume is eligible to be negotiated on it (declared, else the protocol's).</param>
/// <param name="TakesSettings">It takes a rules file, so the person's denies reach it.</param>
/// <param name="Structured">It reports a conversation's structure: protocol frames, or a native dialect's lines.</param>
/// <param name="ServersOnWire">Its servers ride the session's wire; else they are handed in a file on its command line.</param>
/// <param name="RulesOnWire">Its rules ride the open's <c>_meta</c>; else they are handed by its own flag.</param>
/// <param name="SetAtOpen">Its posture, model and effort are set on the session once it opens; else on the command line.</param>
/// <param name="WordsAtNextStep">A person's words reach the running session at its next step.</param>
/// <param name="HeldOnWire">The session is held on its wire: its end said before a timeout's kill, a transient error tried again
/// in the same conversation, and a limit's resume.</param>
/// <param name="ResumesByRestart">A kept conversation goes on in a new process started on it.</param>
public sealed record DoorAbilities(
    Transport Transport,
    bool Interactive,
    bool Resumes,
    bool TakesSettings,
    bool Structured,
    bool ServersOnWire,
    bool RulesOnWire,
    bool SetAtOpen,
    bool WordsAtNextStep,
    bool HeldOnWire,
    bool ResumesByRestart)
{
    /// <summary>
    /// Its servers inherit its environment: a native door driving an adopted repository by the repository's own <c>.mcp.json</c>, the session's
    /// identity arriving with the process's variables. A door that states it absent cannot drive by a repository's own servers.
    /// </summary>
    public bool ServersInherit { get; init; }

    /// <summary>A door's abilities, each as <see cref="AbilityReading"/> reads it; the wiring flags are its transport's.</summary>
    public static DoorAbilities Of(AgentDoor door)
    {
        var protocol = door.Transport == Transport.Protocol;
        return new(
            door.Transport,
            AbilityReading.Does(door, AbilityName.Interactive),
            AbilityReading.Does(door, AbilityName.Resumes),
            AbilityReading.Does(door, AbilityName.TakesSettings),
            Structured: AbilityReading.Does(door, AbilityName.Structured),
            ServersOnWire: AbilityReading.Does(door, AbilityName.ServersOnWire),
            RulesOnWire: protocol,
            SetAtOpen: protocol,
            WordsAtNextStep: AbilityReading.Does(door, AbilityName.WordsAtNextStep),
            HeldOnWire: protocol,
            ResumesByRestart: !protocol)
        {
            ServersInherit = AbilityReading.Does(door, AbilityName.ServersInherit),
        };
    }
}
