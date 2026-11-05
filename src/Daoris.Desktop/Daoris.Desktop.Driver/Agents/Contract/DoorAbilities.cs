namespace Daoris.Driver;

/// <summary>
/// What a door can do, as its callers read it (the agent-provider design §2.3, §2.6): each ability the door declares, else what its
/// transport gives every door of it. A caller asks the ability it acts on, never which transport the door rides.
/// </summary>
/// <remarks>
/// Interactive is always the door's declaration, never the transport's (C2-1.1): <c>dsh</c> declares none and is refused a conversation.
/// The rest are the transport's today (O1-F13); a door that states one of them is read as it states it once the part declares it.
/// </remarks>
/// <param name="Transport">The transport the door rides; read by the transports alone.</param>
/// <param name="Interactive">It holds a conversation (the door's declaration).</param>
/// <param name="Resumes">A resume is eligible to be negotiated on it (declared, else the protocol's).</param>
/// <param name="TakesSettings">It takes a rules file, so the person's denies reach it.</param>
/// <param name="Structured">It reports a conversation's structure: protocol frames, or a native dialect's lines (D76 §1).</param>
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
    /// identity arriving with the process's variables (D70, O1-F19). A door that states it absent cannot drive by a repository's own servers.
    /// </summary>
    public bool ServersInherit { get; init; }

    /// <summary>It can leave its agent's own servers out of a session, so a start that requires that may use it (CONNECTORS1).</summary>
    public bool OwnServersLeftOut { get; init; }

    /// <summary>A contract door's abilities: its declared ones, the rest its transport's.</summary>
    public static DoorAbilities Of(AgentDoor door) => Of(
        door.Transport,
        Declared(door.Abilities.Interactive, false),
        Declared(door.Abilities.Resumes, door.Transport == Transport.Protocol),
        Declared(door.Abilities.TakesSettings, false),
        door.Dialect.IsKnown && door.Dialect.Required is INativeDialect) with
    {
        // A native door's servers inherit its environment as every one did before the ability was stated; a provider says otherwise.
        ServersInherit = Declared(door.Abilities.ServersInherit, door.Transport == Transport.Native),
        OwnServersLeftOut = Declared(door.Abilities.OwnServersLeftOut, false),
    };

    /// <summary>
    /// A door's abilities from its declarations and its transport's: what every door of the transport is given (§2.6), the protocol's
    /// on the wire and the native's on the command line.
    /// </summary>
    /// <param name="nativeDialect">A native door reads its program's structured lines.</param>
    public static DoorAbilities Of(Transport transport, bool interactive, bool resumes, bool takesSettings, bool nativeDialect)
    {
        var protocol = transport == Transport.Protocol;
        return new(
            transport,
            interactive,
            resumes,
            takesSettings,
            Structured: protocol || nativeDialect,
            ServersOnWire: protocol,
            RulesOnWire: protocol,
            SetAtOpen: protocol,
            WordsAtNextStep: protocol,
            HeldOnWire: protocol,
            ResumesByRestart: !protocol)
        {
            ServersInherit = !protocol,
        };
    }

    private static bool Declared(Known<bool> ability, bool otherwise) => ability.IsKnown ? ability.Required : otherwise;
}
