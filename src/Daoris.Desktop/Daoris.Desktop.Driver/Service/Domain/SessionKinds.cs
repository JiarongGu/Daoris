namespace Daoris.Driver;

/// <summary>
/// The kinds a session record names itself by, as the service answers them: <see cref="ActiveSessions.DrivenKind"/> and
/// <see cref="ActiveSessions.ChatKind"/> are these, for the planner's reading.
/// </summary>
public static class SessionKinds
{
    /// <summary>A record's kind where a person's quest is planned into a session (D49 §3): what a host that answers none is read as.</summary>
    public const string Driven = "driven";

    /// <summary>A record's kind where a person opened a conversation, and the kind the driver's own intakes, helpers and reviewers keep (D65, D155, D163).</summary>
    public const string Chat = "chat";
}
