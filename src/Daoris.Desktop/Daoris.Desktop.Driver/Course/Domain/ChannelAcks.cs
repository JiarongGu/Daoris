namespace Daoris.Driver;

/// <summary>
/// An entry's acknowledgments of a channel message (COWORK1d, D188 point 6; the message-urgency design §6), as the service's
/// <c>ChannelAcks</c> spells them: a twin, since the driver and the service share no code.
/// </summary>
public static class ChannelAcks
{
    /// <summary>A part claimed it: one part of an entry takes a message.</summary>
    public const string Taking = "taking";

    /// <summary>Written to the part's harness, or kept on its record's own queue.</summary>
    public const string Sent = "sent";

    /// <summary>The door said the part took it, or a start's packet carried it whole once the part works.</summary>
    public const string Taken = "taken";

    /// <summary>The agent's own <c>channel_read</c> returned it whole.</summary>
    public const string Read = "read";

    /// <summary>A start's packet cut it, or left it among the older ones: a new part is handed it again.</summary>
    public const string Partly = "partly";

    /// <summary>A part let its claim go unsent: its run ended with nothing else to say.</summary>
    public const string Released = "released";
}
