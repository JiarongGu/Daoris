namespace Daoris.Driver;

/// <summary>
/// How a run's work lands: the intent its bound version's <c>land</c> carries (<see cref="LandNodes.LandingOf"/>). A merge goes into the
/// line; a branch is named by <see cref="Pattern"/>, its ticket found by <see cref="Key"/>, pushed by <see cref="Plugin"/> where one is named,
/// and made one commit where <see cref="Squash"/> says so. <see cref="AutoAccept"/> is a graph with no review before its <c>land</c>.
/// </summary>
public sealed record Landing(
    string Form, string? Pattern = null, string? Plugin = null, bool AutoAccept = false, string? Key = null, bool Squash = false)
{
    /// <summary>A merge accepted by the person: what a process with no bound version declares, so nothing lands it unasked.</summary>
    public static readonly Landing Merge = new(LandingForm.Merge);
}
