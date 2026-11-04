namespace Daoris.Desktop;

// The payloads of TogetherRoutes' read and presses, each named for its route.

public sealed record TogetherRead(string Session);

/// <summary>The head's <c>Session</c> and the <c>Members</c> the person ticked, each judged again by the press.</summary>
public sealed record TogetherPressAsk(string Session, IReadOnlyList<string>? Members);

public sealed record TogetherAskAgain(string Session);

public sealed record TogetherLetGo(string Session);
