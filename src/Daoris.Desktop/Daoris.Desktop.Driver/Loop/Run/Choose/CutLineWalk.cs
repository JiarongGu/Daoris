namespace Daoris.Driver;

/// <summary>
/// Where a line an account's limit cut off carries its waiting words: the same walk of the role's choices a line with none takes, so a
/// cut-off line goes on elsewhere the same way whether words wait on its record or not.
/// </summary>
internal static class CutLineWalk
{
    /// <summary>
    /// Whether the words' own agent found nothing ready for them (every account cooling) while the role lists another choice to walk, and
    /// the account is no one the person named: the line then walks the role's choices, its words with it.
    /// </summary>
    /// <param name="resume">What the resume on the record's own agent answered.</param>
    /// <param name="limitWalks">Whether the role lists another choice for the cut-off line to walk, and does not wait on the limit.</param>
    /// <param name="picked">Whether the person named the record's account.</param>
    internal static bool Leaves(ResumeChoice resume, bool limitWalks, bool picked) =>
        limitWalks && !picked && !resume.Selection.Allowed && resume.Answer is LineAnswer.Wait;
}
