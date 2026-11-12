namespace Daoris.Knowledge;

/// <summary>
/// The person's through words on an ask: their own words asking for its work to be delivered with no further press of theirs,
/// kept with when. A run of the ask's quests applies their Accept on them where nothing else waits on the person; <see cref="Words"/>
/// null is their take-back, after which every run waits for them again.
/// </summary>
public sealed record AskThrough(string? Words, DateTimeOffset At);
