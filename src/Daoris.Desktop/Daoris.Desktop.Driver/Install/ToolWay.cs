namespace Daoris.Driver;

/// <summary>How a tool is run (D121 §2.2): the system's, from <c>PATH</c>; managed; or a file the person names.</summary>
public enum ToolWay
{
    System,
    Managed,
    File,
}

/// <summary>A program Daoris starts, or hands to a session, that is neither an agent (D57) nor Daoris's own (§2.1).</summary>
/// <param name="Id">How the file and the doors name it.</param>
/// <param name="Name">Its product's name, as a person knows it.</param>
/// <param name="Answers">The names it answers for on a child's <c>PATH</c>; the first is the program itself.</param>
/// <param name="Version">The arguments that ask its version.</param>
/// <param name="Settings">The settings it may carry (§2.5): git's allow-list, and none for the rest.</param>
public sealed record ToolDeclaration(
    string Id, string Name, IReadOnlyList<string> Answers, IReadOnlyList<string> Version, IReadOnlyList<string> Settings);

/// <summary>One tool's entry as read: its way, or why it is refused.</summary>
/// <param name="Way">Null when the entry, or the file, could not say one.</param>
/// <param name="Problem">Why the entry is refused: its tool is never run another way. Null when it reads.</param>
public sealed record ToolEntry(ToolWay? Way, string? Version, string? File, string? Problem);
