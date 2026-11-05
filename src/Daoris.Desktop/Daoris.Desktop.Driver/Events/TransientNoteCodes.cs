namespace Daoris.Driver;

/// <summary>
/// The codes a pass that is no driven session or conversation ends with past its transient bound (AGENTERRORS1d, D210 point 8; the
/// agent-errors design §7): its provider kept failing through the tries it made, or until its own minutes ran out inside the episode, never
/// <i>timed out</i>. Each is followed by the provider's words. Part of the catalogue <see cref="NoteCodes"/> introduces.
/// </summary>
public static class TransientNoteCodes
{
    /// <summary>A helper settled refused for its provider (<see cref="HelperWhy.Transient"/>), said on the asker.</summary>
    public static readonly NoteCode HelperTransient = new("helper.transient", ["agent", "count"]);

    /// <summary>A second opinion's pass failed for its provider: its gate is unavailable, and the person's <i>Try again</i> asks it again.</summary>
    public static readonly NoteCode OpinionTransient = new("opinion.transient", ["agent", "count"]);

    /// <summary>An intake ended for its provider: its ask is unbound once, so the next look picks it again with a fresh intake.</summary>
    public static readonly NoteCode IntakeTransient = new("intake.transient", ["agent", "count"]);

    /// <summary>A partner's piece ended for its provider, its work kept; its lead is told.</summary>
    public static readonly NoteCode PartnerTransient = new("partner.transient", ["agent"]);

    /// <summary>A brainstorm voice dropped for its provider within its minutes.</summary>
    public static readonly NoteCode VoiceTransient = new("voice.transient", ["agent", "count"]);
}
