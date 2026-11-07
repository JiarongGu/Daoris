using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What an advance of a standing branch is judged by, read from git at the press or the look: the
/// record's entry for it, where it stands, where it is checked out, whether the session's work grows from it, and whether its
/// work already reads on the line.
/// </summary>
/// <param name="Recorded">The landing record's standing entry for this branch in this repository, or null: not Daoris's.</param>
/// <param name="Tip">The commit the branch stands at now.</param>
/// <param name="CheckedOutAt">The working tree that has it checked out, or null.</param>
/// <param name="Descends">Whether the session's work grows from the recorded tip: its tip has that commit in its history.</param>
/// <param name="Ahead">How many commits the session's work holds beyond the recorded tip.</param>
/// <param name="Completed">
/// Where the end proof found the recorded tip's work on the line — a form of it, such as <c>origin/main</c> — so its pull
/// request was merged; null where it does not read there.
/// </param>
public sealed record AdvanceFacts(LandedBranch? Recorded, string Tip, string? CheckedOutAt, bool Descends, int Ahead, string? Completed);
