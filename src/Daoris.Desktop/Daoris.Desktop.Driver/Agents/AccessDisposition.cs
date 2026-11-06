namespace Daoris.Driver;

/// <summary>
/// How an access failure was read (the agent-lyntai design §5.3): what a refused resume's open writes of its account. It records how the
/// failure was read, not why, so a sign-in read by a code the door declares stays apart from one read by its words, which also holds the
/// tool's own sign-in and says a line.
/// </summary>
public enum AccessDisposition
{
    /// <summary>The credential's refusal, by its words: the account is refused.</summary>
    Refused,

    /// <summary>A sign-in's words: the account reads signed out, its tool's own sign-in held and a line said.</summary>
    SignedOutByWords,

    /// <summary>A code, kind or status the door declares for access: the account reads signed out, nothing else.</summary>
    SignedOutByCode,
}
