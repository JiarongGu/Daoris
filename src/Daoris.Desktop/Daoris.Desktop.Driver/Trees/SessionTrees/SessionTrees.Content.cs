namespace Daoris.Driver;

/// <summary>
/// The content proof and what a removal keeps and deletes by it, as <see cref="ContentHolds"/> (PART10l): built once here for the
/// front's removal, its clean-up's sweep and the tidy, which PART10l-ii moves. It has no public call. PART10l-ii folds this part.
/// </summary>
public sealed partial class SessionTrees
{
    private ContentHolds? _content;

    /// <summary>Where a branch's work is held by content, and the delete at the commit judged (SQUASHTIDY1, SQUASHTIDY1c).</summary>
    private ContentHolds Content => _content ??= new(_context);
}
