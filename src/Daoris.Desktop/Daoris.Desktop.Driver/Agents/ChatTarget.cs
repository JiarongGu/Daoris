namespace Daoris.Driver;

/// <param name="Repository">Whose agent the conversation is.</param>
/// <param name="Root">The working tree it runs in.</param>
/// <param name="ServiceUrl">Where the service is, so the session can take or publish quests itself.</param>
/// <remarks>
/// A chat carries no quest (D49 §3) — that is the whole point: it is for work not yet shaped as an
/// ask. It may take one mid-conversation through its own connector, exactly as a driven session does.
/// </remarks>
public sealed record ChatTarget(string Repository, string Root, string ServiceUrl)
{
    public ActorAsked? Asked { get; init; }
    /// <summary>
    /// The harness conversation an ended chat goes on in (MSG1c, D137 §4.2), by the id Daoris kept for its record; null for
    /// a new conversation. A native door hands it to its harness's own resume; a protocol door resumes on its wire.
    /// </summary>
    public string? Resume { get; init; }

    /// <summary>
    /// The permission mode a native door runs this conversation in, where it names one (HELPMODE1a1): Ask Daoris in full access,
    /// the adapter's <see cref="ISessionAdapter.FullPosture"/>. Null keeps the adapter's own, as every conversation had. A protocol
    /// door sets its mode on the wire instead.
    /// </summary>
    public string? PermissionMode { get; init; }

    /// <summary>
    /// The conversation's own record (CHATTAKE1b, D126's CHATTAKE1 note), named on its spawn as a driven session's is (SESS1):
    /// a connector its harness starts itself inherits it, so a take or a publication through it says which conversation made
    /// it. Null names none.
    /// </summary>
    public string? Session { get; init; }
}
