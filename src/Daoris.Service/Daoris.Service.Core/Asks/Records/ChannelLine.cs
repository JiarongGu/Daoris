namespace Daoris.Knowledge;

/// <summary>
/// One of Daoris's lines: a quest of the ask in the state it stands in, read from the quest and never stored.
/// Coded, so the page words it in the reader's language.
/// </summary>
/// <param name="Code"><c>published</c>, <c>taken</c>, <c>done</c>, <c>declined</c>, or <c>awaiting</c>: done, waiting on the person's yes.</param>
/// <param name="Quest">The quest's id, the line's door.</param>
/// <param name="Name">What a list calls the quest, as its publisher wrote it.</param>
/// <param name="From">The asker: a repository, or <c>ask #id</c>.</param>
/// <param name="To">The repository asked.</param>
/// <param name="At">When the quest is published, or last moved.</param>
public sealed record ChannelLine(string Code, string Quest, string Name, string From, string To, DateTimeOffset At) : IChannelItem;
