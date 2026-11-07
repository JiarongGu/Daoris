namespace Daoris.Driver;

/// <summary>
/// What a protocol session hands the part that reads a prompt's failure and tries again (<see cref="AcpEpisodes"/>): the retry the run
/// is handed, the words an episode holds, the words a failed prompt left unacknowledged, and the one try written to the wire.
/// </summary>
internal interface ITurnRetryWiring : ITurnEndWiring
{
    /// <summary>The run's transient retry, or null where it is handed none and keeps today's end.</summary>
    TransientRetry? Retry { get; }

    /// <summary>Whose words an episode holds, waits on and carries: a run's inbox, or a conversation's turns.</summary>
    IEpisodeWords? EpisodeWords { get; }

    /// <summary>The person's words a failed prompt left unacknowledged, taken off the list.</summary>
    List<IAgentWord> TakeUnacknowledged();

    /// <summary>The unacknowledged words forgotten: no episode opened, and today's judgement of the words stands.</summary>
    void ForgetUnacknowledged();

    /// <summary>One try's prompt on the wire: the stop reason of its answer.</summary>
    Task<string> TryAsync(TransientEpisode episode, CancellationToken ct);
}
