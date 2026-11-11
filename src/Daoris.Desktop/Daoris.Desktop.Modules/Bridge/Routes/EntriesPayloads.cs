namespace Daoris.Desktop;

// A terminal's verbs send <c>Said</c> and <c>Follow</c>, read as the page's flags are: true only where they say true.

/// <summary>Where an entry's parts would land; <c>Said</c> asks for the verb's words in place of the plan's fields.</summary>
public sealed record EntryLandingRead(string Entry, bool Said = false);

/// <summary>An entry's landing; <c>LeaveOut</c> names the parts left out, <c>Follow</c> registers the lines it moved, <c>Revisions</c> each part's record drawn.</summary>
public sealed record LandEntryPress(string Entry, IReadOnlyList<string>? LeaveOut, bool Follow = false, IReadOnlyList<EntryPartRevision>? Revisions = null);

/// <summary>The review record the page drew for one part of an entry, by its session.</summary>
public sealed record EntryPartRevision(string Session, string Revision);

/// <summary>Each of an ask's entries' plans; <c>Said</c> asks for the verb's words.</summary>
public sealed record AskLandingRead(string Ask, bool Said = false);

/// <summary>An ask's landing, entry by entry; <c>LeaveOut</c> and <c>Follow</c> as for an entry's.</summary>
public sealed record LandAskPress(string Ask, IReadOnlyList<string>? LeaveOut, bool Follow = false);
