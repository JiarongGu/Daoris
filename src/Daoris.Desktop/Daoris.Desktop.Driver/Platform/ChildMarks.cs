namespace Daoris.Driver;

/// <summary>
/// The names of the variables Daoris puts in a child's environment that no start hands on to a person's own terminal: a process
/// start's own, so the start that strips them names them here and not in the subdomain that sets them.
/// </summary>
public static class ChildMarks
{
    /// <summary>The variable a session's shell carries its id in (<see cref="IntakeRoom.SessionVariable"/>, which reads it from here).</summary>
    public const string SessionVariable = "DAORIS_SESSION_ID";

    /// <summary>The variable the holder's terminal carries its key in (<see cref="HolderKey.Variable"/>, which reads it from here).</summary>
    public const string RoleKeyVariable = "DAORIS_ROLE_KEY";

    /// <summary>The variable a session's shell carries its delegate key in (<see cref="DelegateKey.Variable"/>, which reads it from here).</summary>
    public const string DelegateKeyVariable = "DAORIS_DELEGATE_KEY";

    /// <summary>
    /// The two variables a Claude Code session's shell carries for its inbox: its socket and its token.
    /// No Daoris executable or start passes them on, so an agent Daoris starts never holds the steward's inbox.
    /// </summary>
    public static readonly IReadOnlyList<string> MessagingVariables = ["CLAUDE_CODE_MESSAGING_SOCKET", "CLAUDE_CODE_MESSAGING_TOKEN"];

    /// <summary>
    /// The variable a session's process carries its lineage token in, set anew at each spawn. 🔴 The lineage hook and
    /// the connector's <c>LineageFile.ProcVariable</c> spell it as twins.
    /// </summary>
    public const string LineageProcVariable = "DAORIS_LINEAGE_PROC";
}
