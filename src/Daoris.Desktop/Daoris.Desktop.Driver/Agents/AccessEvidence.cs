namespace Daoris.Driver;

/// <summary>The reason a door's own field stated for an access failure, where it stated one.</summary>
public enum AccessReason
{
    /// <summary>The account must sign in again.</summary>
    SignInAgain,

    /// <summary>The credential is refused.</summary>
    CredentialRefused,
}

/// <summary>
/// An access failure's evidence, decoded apart and never one winner: the
/// reason a field stated, and, separately, whether the failure's words read as a sign-in or as a refused credential. Each account reader
/// reads its own part, as the readers read different evidence of one failure.
/// </summary>
/// <param name="Field">The reason the door's own field stated, or null where none did.</param>
/// <param name="WordsSignIn">Whether the failure's words read as a sign-in, by the door's own reading of its sign-in sentence.</param>
/// <param name="WordsRefused">Whether the failure's words read as a refused credential.</param>
public sealed record AccessEvidence(AccessReason? Field, bool WordsSignIn, bool WordsRefused)
{
    /// <summary>
    /// A refused resume's open, by precedence: a field first, read as a declared code is whatever its reason, since the disposition
    /// records how the failure is read and not why; then a sign-in's words; then a refusal's. Null where nothing reads it as access.
    /// </summary>
    public AccessDisposition? Opening =>
        Field is not null ? AccessDisposition.SignedOutByCode
        : WordsSignIn ? AccessDisposition.SignedOutByWords
        : WordsRefused ? AccessDisposition.Refused
        : null;

    /// <summary>The conclusion's sign-out: a sign-in's words, or the door's own field, whatever the words.</summary>
    public bool SignsOutAtConclusion => WordsSignIn || Field is not null;

    /// <summary>The conversation's sign-out: a sign-in's words alone.</summary>
    public bool SignsOutInConversation => WordsSignIn;

    /// <summary>The conclusion's refused credential: a refusal's words alone.</summary>
    public bool Refuses => WordsRefused;
}
