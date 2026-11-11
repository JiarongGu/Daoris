using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The review record a door test hands a step writing the work, read as a review would read the work now: a plan's sessions' newest plan, or
/// the copy its records worked in. A full graph run is fed its record by the runtime instead.
/// </summary>
internal static class EffectRecords
{
    public static ReviewRecord Planned(string home, IReadOnlyList<string> sessions) => PlanReview.Reference(home, sessions) is { } plan
        ? Composed(home, ReviewKinds.Plan, plan)
        : Composed(home, ReviewKinds.Answer, new());

    public static ReviewRecord Copied(string home, IReadOnlyList<string> records) => FolderCopies.Of(home, records) is { } copy
        ? Composed(home, ReviewKinds.Copy, new() { Workspace = copy.Workspace, Department = copy.Department, Copy = copy.Id, Baseline = copy.Id })
        : Composed(home, ReviewKinds.Folder, new());

    private static ReviewRecord Composed(string home, string kind, ReviewReference reference) =>
        new ReviewWork(kind, "place", reference, []).Compose(home, "r1", "review", 1, DateTimeOffset.UtcNow);
}
