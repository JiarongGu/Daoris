namespace Daoris.Knowledge;

/// <summary>
/// The ask a connector speaks for, when the session that spawned it is an INTAKE (D65 §1b).
/// </summary>
/// <remarks>
/// <para><b>Why the environment.</b> An intake runs in a room under the home, which is no registered
/// repository — so nothing about where it runs can say which ask it answers, or which circle that ask
/// was made in. The driver that opened the session knows both, and hands them to the connector it
/// offers the way it hands everything else a session needs: in the spawn's environment.</para>
///
/// <para><b>The session names itself, and the ask checks it.</b> A connector carrying these publishes
/// AS the ask, in its circle; only the ask's own intake moves its tier to <c>intake</c>, so a stray
/// variable can publish on an ask's behalf — as a person could — but cannot claim a harness decided.</para>
/// </remarks>
/// <param name="Ask">The ask the session answers, or null for every other session.</param>
/// <param name="Session">
/// The session answering it — what the ask's record is checked against. The driver names it for every
/// session it starts, and a rule proposal records it as the author (PERM2); with no ask it changes
/// nothing about a publish.
/// </param>
public sealed record IntakeScope(string? Ask, string? Session)
{
    /// <summary>Which ask — set by the driver on an intake's spawn and on the connector it offers.</summary>
    public const string AskVariable = "DAORIS_ASK_ID";

    /// <summary>Which session — the intake's own record.</summary>
    public const string SessionVariable = "DAORIS_SESSION_ID";

    /// <summary>A session that answers no ask — every session but an intake.</summary>
    public static IntakeScope None { get; } = new(null, null);

    /// <summary>What this process was spawned with. Blank is nothing said.</summary>
    public static IntakeScope FromEnvironment() => new(
        Blank(Environment.GetEnvironmentVariable(AskVariable)),
        Blank(Environment.GetEnvironmentVariable(SessionVariable)));

    /// <summary>Whether this connector speaks for an ask.</summary>
    public bool Active => Ask is { Length: > 0 };

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimStart('#');
}
