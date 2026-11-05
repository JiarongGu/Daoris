namespace Daoris.Driver;

/// <summary>
/// A conversation's refused call read for its account: a limit cools the account it runs as, a refused sign-in signs it out, each with the
/// line its record takes; and what a conversation going on from a limit carries. The conversation goes on either way: the process is still
/// there, and so is the person.
/// </summary>
internal sealed class ChatAccountReads(ChatContext context)
{
    /// <summary>
    /// A conversation's turn the door refused, read for an account's limit: where the adapter's table recognises it, the account the
    /// conversation runs as cools, the log says so, and the line for its record comes back with its code, as a session record's cooling line
    /// carries it. Null is a refusal like any other.
    /// </summary>
    /// <param name="coolOff">The machine's <c>cooloff</c>, for a limit that names no time; the default when null.</param>
    /// <param name="byCode">
    /// The reader's reading of the same failure where it read a limit by its code or kind with no sentence, as a driven run passes it: cooled
    /// for the unstated default. Null reads the sentence alone.
    /// </param>
    public Noted? Limited(string sessionId, ChatAgent agent, string? profile, string failure, TimeSpan? coolOff = null, LimitSeen? byCode = null)
    {
        (CoolingEntry Entry, LimitSeen Seen)? limited;
        try
        {
            limited = context.Harnesses.Limited(agent.Name, profile, failure, sessionId, coolOff, byCode);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (limited is not { } read) return null;

        context.Service.AccountSaid(AccountLine.Limited(
            sessionId, agent.Name, profile, read.Seen, context.Events.TurnsEnded(sessionId) + 1, used: null));
        return CoolingWords.ConversationOf(read.Entry, context.Harnesses.Zone);
    }

    /// <summary>
    /// What a conversation that goes on from a limit carries, for its opening line and <c>account.rotated</c>: the one it continues, the turn
    /// the limit refused (its last ended, since a conversation's refused turn ends <c>error</c> in its record, after <see cref="Limited"/>
    /// counted it) and its context there. Null for a start that carries none.
    /// </summary>
    public RotatedOpening.Carried? Carried(string? carries) =>
        carries is null ? null : new RotatedOpening.Carried(carries, context.Events.TurnsEnded(carries), RotatedOpening.ContextOf(null, context.Events, carries));

    /// <summary>
    /// A conversation's call the door refused, read for the account's sign-in by the reader a driven start's conclusion uses
    /// (<see cref="RefusedAccounts.SignInRefused"/>): where the adapter's table recognises it, the account the conversation runs as reads
    /// signed out, kept as a reading is, so the next start walks past it, and the line for its record comes back. Null is a refusal like any
    /// other. Read from the door's failure, never from the transcript.
    /// </summary>
    public Noted? SignedOut(ChatAgent agent, string? profile, string failure)
    {
        try
        {
            return agent.SignInRefused(context.Harnesses, profile, failure);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // A reading not written, or an adapter a plugin no longer declares, costs the next start one more refusal, never this
            // conversation: this runs inside the refused turn's own handling.
            return null;
        }
    }
}
