namespace Daoris.Knowledge;

/// <summary>What the channel shows in time order: a message, or one of Daoris's lines.</summary>
public interface IChannelItem
{
    /// <summary>When it was posted, or when the quest reached the state the line says.</summary>
    DateTimeOffset At { get; }
}
