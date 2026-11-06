namespace Daoris.Driver;

/// <summary>
/// How a pass that is no driven session or conversation ends past its transient bound (AGENTERRORS1d, D210 point 8; the agent-errors design
/// §7): an intake, a helper, an opinion, a partner and a brainstorm voice each take the one retry at the hold, and each reads its end here,
/// once: <c>failed</c>, said by its kind's code with the tries it made and the provider's words. A pass whose minutes ran out inside the
/// episode ends the same way, never <i>timed out</i>.
/// </summary>
internal static class TransientEnded
{
    /// <summary>The kinds of pass, as their ends name them.</summary>
    public const string Helper = "helper", Opinion = "opinion", Intake = "intake", Partner = "partner", Voice = "voice";

    /// <summary>
    /// The episode a run ended past its bound in, or that its own minutes ended inside (<paramref name="timedOut"/>, the hold's kill at them);
    /// null where it ended otherwise.
    /// </summary>
    public static TransientEpisode? Of(HarnessEnding ended, bool timedOut = false) =>
        ended.Transient is { Opened: true, Episode: { } episode } && (episode.Exhausted || timedOut) ? episode : null;

    /// <summary>
    /// <paramref name="conclusion"/> as a pass of <paramref name="kind"/> ends it: its transient end where the run ended past its bound or its
    /// minutes ended inside the episode, else as it was. A person's stop is read before this, and stays the person's.
    /// </summary>
    public static SessionConclusion Over(SessionConclusion conclusion, HarnessEnding ended, string kind, string agent, bool timedOut = false) =>
        Of(ended, timedOut) is { } episode ? SessionConclusion.Of("failed", Said(kind, agent, episode)) : conclusion;

    /// <summary>The kind's line: the agent, the tries made, and the provider's own words after it.</summary>
    public static Noted Said(string kind, string agent, TransientEpisode episode)
    {
        var colon = episode.Words is { Length: > 0 } ? ":" : ".";
        var tries = episode.Tries;
        var line = kind switch
        {
            Helper => Noted.Of(PeerNoteCodes.HelperTransient, $"Helper {agent} could not answer: its provider kept failing through {tries} tries{colon}", ("agent", agent), ("count", tries)),
            Opinion => Noted.Of(PeerNoteCodes.OpinionTransient, $"The opinion of {agent} is unavailable: its provider kept failing through {tries} tries{colon}", ("agent", agent), ("count", tries)),
            Intake => Noted.Of(PeerNoteCodes.IntakeTransient, $"The intake on {agent} ended: its provider kept failing through {tries} tries, so the ask is picked again{colon}", ("agent", agent), ("count", tries)),
            Partner => Noted.Of(PeerNoteCodes.PartnerTransient, $"Partner {agent}'s provider kept failing; the work it left is kept{colon}", ("agent", agent)),
            _ => Noted.Of(PeerNoteCodes.VoiceTransient, $"Voice {agent} dropped: its provider kept failing through {tries} tries{colon}", ("agent", agent), ("count", tries)),
        };
        return episode.Words is { Length: > 0 } words ? line.Then(" ", Noted.Said(words, NoteBy.Program)) : line;
    }

    /// <summary>
    /// The check of a pass with no outcome of its own to outrank a retry (a brainstorm voice, §5.5): another try is wanted while the person has
    /// not stopped it. The other kinds hand <see cref="TransientOwners"/>' check, each its own authority.
    /// </summary>
    public static Func<CancellationToken, Task<bool>> Wanted(SessionProcesses processes, string sessionId) =>
        _ => Task.FromResult(!processes.WasStopRequested(sessionId));
}
