namespace Daoris.Driver;

/// <summary>The person's ask words and go-aheads a start reads; Work orders owns their reading and composition.</summary>
public interface IAgentAskWords
{
    string Ask { get; }
    IReadOnlyList<AskWordView>? Said { get; }
    DateTimeOffset? KeptFrom { get; }
    IReadOnlyList<GoAheadView>? GoAheads { get; }
}

/// <summary>
/// What a part of an entry is handed from the ask's channel when it starts (SESSONE2b, design §2.3): the messages for its entry
/// it has not acknowledged (COWORK1d), the newest within the bounds, each acknowledged once the part has started.
/// </summary>
/// <param name="Ask">The ask the channel is.</param>
/// <param name="Entry">The entry's key, whose acknowledgments are written.</param>
/// <param name="Messages">The messages for the entry it has not had, oldest first; bounded where they are written.</param>
/// <param name="Session">The starting part's session, which writes the acknowledgments.</param>
public sealed record ChannelHeld(string Ask, string Entry, IReadOnlyList<ChannelItemView> Messages, string? Session);

/// <summary>The work variables a spawn carries; higher work owners forward the shared names.</summary>
public static class AgentWorkVariables
{
    public const string Ask = "DAORIS_ASK_ID";
    public const string Brainstorm = "DAORIS_BRAINSTORM_ID";
    public const string Turn = "DAORIS_BRAINSTORM_TURN";
    public const string Answer = "DAORIS_BRAINSTORM_ANSWER";
    public const string Room = "DAORIS_BRAINSTORM_ROOM";
}
