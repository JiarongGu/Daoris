namespace Daoris.Desktop;

// The payloads of TreesRoutes' reads and presses, each named for its route. A flag the terminal's verbs send (`Said`, `Follow`) is
// read as the page's flags are: true only where it says true.

/// <summary>A reviewed session's landing plan; <c>Branch</c> is the name the person entered in place of the rule's, blank keeping the rule's.</summary>
public sealed record LandingRead(string Id, string? Branch, bool Said = false);

public sealed record OpinionGateRead(string Id, bool Said = false);

/// <summary><c>SameAgent</c> asks the same agent fresh; an <c>Occasion</c> of <c>failure</c> asks for help with a struck quest.</summary>
public sealed record AskOpinion(string Id, string? Reviewer, string? Occasion, string? Words, bool SameAgent = false, bool Said = false);

public sealed record OpinionAnyway(string Id, string? Words, bool Said = false);

public sealed record OpinionMyself(string Id, string? Words, bool Said = false);

public sealed record StopOpinion(string Opinion, bool Said = false);

/// <summary>The <c>Read</c> is the token the call waits under, which the module reads off any request so a later <c>READ_LEAVE</c> can stop it.</summary>
public sealed record SessionDiff(string Id, string? Read);

public sealed record ReadLeave(string Read);

public sealed record SessionProcess(string Id);

public sealed record SessionFiles(string Id);

public sealed record SessionFile(string Id, string Path);

/// <summary>A session by its <c>Id</c>, or a tree by its <c>Path</c> as the terminal's <c>trees remove</c> names it; <c>Force</c> says out loud that unlanded work may go.</summary>
public sealed record DiscardSessionTree(string? Id, string? Path, bool Force = false);
