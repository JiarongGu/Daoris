namespace Daoris.Driver;

/// <summary>The protocol door's posture markers, shared with the higher owners that request a mode.</summary>
public static class AgentPostures
{
    /// <summary>
    /// The mark before a protocol door's posture that must be set: an agent that does not offer it fails the start rather
    /// than running in a narrower mode under the requested label (D183 point 3; AUTONOMY1b).
    /// </summary>
    public const char Required = '!';
}
