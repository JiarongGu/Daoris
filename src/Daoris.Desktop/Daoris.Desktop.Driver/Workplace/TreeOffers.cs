namespace Daoris.Driver;

/// <summary>
/// What an ended session's own tree offers to land (LAND4): the commits on it no branch of the person's holds, by D88's
/// proof, whatever the session's ending, with the branch and the tree they are on. The press is the review's Accept, at any
/// of its doors, so everything a landing checks is checked there; this only says there is work a press would land.
/// </summary>
/// <param name="Branch">The session's branch, <c>daoris/</c> and its tree's name, as the layout names it (D51).</param>
/// <param name="Tree">The tree's name under its repository's folder: never a path.</param>
/// <param name="Commits">The commits a landing would carry: a proven count, above zero.</param>
/// <param name="Uncommitted">Its uncommitted paths, which a branch leaves behind; null where git could not say.</param>
public sealed record LandOffer(string Branch, string Tree, int Commits, int? Uncommitted)
{
    /// <summary>
    /// The offer a judged tree makes, or null: only commits git counted, above zero. Uncommitted work alone lands nothing, and
    /// a count git could not give offers no press, though it stays to review (the clean-up's keep, D88). Commits the line holds
    /// by content offer none either (SQUASHTIDY1b): accepting them would make a branch of work already there.
    /// </summary>
    public static LandOffer? Of(string tree, TreeWork work)
    {
        if (work.Commits is not > 0 || work.Held is not null || TreeNames.Normal(tree).Split('/')[^1] is not { Length: > 0 } name) return null;
        // The tree's own branch where it moved onto one (TREEFOLLOW1), else the layout's name for it.
        return new LandOffer(work.Branch ?? TreeFollows.SessionPrefix + name, name, work.Commits.Value, work.Uncommitted);
    }
}

/// <summary>
/// What an ended session's own tree offers where its commits are held by content (SQUASHTIDY1b): no landing, since accepting
/// would make a branch of work a squash merge or a cherry-pick already put elsewhere, but where its work is, in Discard's own
/// clause, and the review's Discard, unforced, naming the ref its commits stay at. The press is that Discard, which judges
/// again and keeps the ref at its press (SQUASHTIDY1c); this only says what it would do.
/// </summary>
/// <param name="Branch">The session's branch, as <see cref="LandOffer.Branch"/>.</param>
/// <param name="Tree">The tree's name under its repository's folder: never a path.</param>
/// <param name="Work">Where its work is held, and what a discard would keep or why the tree stays.</param>
public sealed record DiscardOffer(string Branch, string Tree, HeldWork Work)
{
    /// <summary>
    /// What the session's page says where <i>Accept…</i> would be, the page showing it as it is: where its work is, in the clause
    /// the discard says, as a sentence; and where an unforced discard would not go now, why its tree stays.
    /// </summary>
    public string Says
    {
        get
        {
            var said = Work.Held.Said;
            var where = $"{char.ToUpperInvariant(said[0])}{said[1..]}.";
            return Work.Stays is { } why ? $"{where} Its tree stays: {why}." : where;
        }
    }

    /// <summary>
    /// What the discard's ask says before its press: where its commits stay and how to have the branch back, in the words the
    /// discard says once it went (<see cref="ContentHold.KeptAt"/>). Null where the discard would not go now.
    /// </summary>
    public string? KeptAt => Work.Keeps is { } reference ? ContentHold.KeptAt(Branch, reference) : null;

    /// <summary>
    /// What a landing of this tree says at every door (SQUASHTIDY1f): refused, where its work is in the clause the page says where
    /// <i>Accept…</i> would be, why landing it again is refused, and its discard, or why its tree stays where the discard would not
    /// go unforced. The press's refusal and the plan before it say it alike, so it names no tense.
    /// </summary>
    public string NotLanded =>
        $"{char.ToUpperInvariant(Work.Held.Said[0])}{Work.Held.Said[1..]}, so it is not landed again: that would make a second copy of it. "
        + (Work.Stays is { } why ? $"Its tree stays: {why}." : "Discard its tree instead.");

    /// <summary>The offer a judged tree makes, or null: only commits git counted, above zero, that the content proof found held.</summary>
    public static DiscardOffer? Of(string tree, TreeWork work)
    {
        if (work.Commits is not > 0 || work.Held is not { } held || TreeNames.Normal(tree).Split('/')[^1] is not { Length: > 0 } name) return null;
        return new DiscardOffer(work.Branch ?? TreeFollows.SessionPrefix + name, name, held);
    }
}
