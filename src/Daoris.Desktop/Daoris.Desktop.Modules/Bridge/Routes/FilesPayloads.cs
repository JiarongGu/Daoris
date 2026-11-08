namespace Daoris.Desktop;

// The payloads of FilesRoutes, each named for its route. Every one names the repository by its <c>Repository</c>.

/// <summary>A repository's files git does not manage, or its read again.</summary>
public sealed record FilesRead(string? Repository);

/// <summary>A rule of the person's own: its kind, its paths and, for a local file, whether it is copied; <c>Machine</c> puts it in the rules for every repository here.</summary>
public sealed record FilesSetPress(string? Repository, string? Kind, IReadOnlyList<string>? Paths, bool? Copy, bool Machine = false);

/// <summary>The person's own rules naming these paths, taken out.</summary>
public sealed record FilesRemovePress(string? Repository, IReadOnlyList<string>? Paths, bool Machine = false);

/// <summary>The person's wording of every path their file reads as written.</summary>
public sealed record FilesSettlePress(string? Repository, IReadOnlyList<FilesWordingChoice>? Wordings);

/// <summary>One path read as written, by its scope (<c>machine</c> or a repository's name), worded as the folder whole or its files.</summary>
public sealed record FilesWordingChoice(string? Scope, string? Path, bool Whole);

/// <summary>A permission added to a list or taken out: an allow made from an ask, or taken back.</summary>
public sealed record FilesPermitPress(string? Repository, string? Act, string? List, string? Rule, bool Machine = false);

/// <summary>What a press of the files section answers: done, or why not (a code the page words) with the driver's sentence.</summary>
public sealed record FilesAnswered(bool Done, string? Why, string? Message);
