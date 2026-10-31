namespace Daoris.Driver;

/// <summary>A session of one repository with a tree here, as the comparison takes it (LINESTOGETHER1c).</summary>
/// <param name="Id">The session's id.</param>
/// <param name="Kind">Its record's kind, <c>driven</c> or <c>chat</c>.</param>
/// <param name="Tree">The tree it holds.</param>
/// <param name="BaseCommit">The commit its tree stood at when it opened.</param>
/// <param name="Running">Whether it runs now: a running tree is read at each look.</param>
/// <param name="Declared">The paths its quest declared, none for a chat.</param>
/// <param name="Quest">The quest it serves, or null.</param>
public sealed record RelatedCandidate(
    string Id, string Kind, string Tree, string BaseCommit, bool Running, IReadOnlyList<string> Declared, string? Quest = null);
