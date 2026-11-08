namespace Daoris.Driver;

/// <summary>
/// A caller from outside the process told as beneath a git of the scheduler's by its process ancestry: a hook a push runs, or a
/// textconv or filter helper a diff runs, that asks the window over the instance pipe. Its gits take the place that git lends
/// (<see cref="GitScheduler.Beneath"/>), never one of the four its parent may be waiting behind.
/// </summary>
/// <remarks>
/// A word the caller carries (a variable its act hands it) is not asked: any process can carry or drop one. The ancestry is read only
/// while a git holds a place, since otherwise no caller can be beneath one.
/// </remarks>
public static class GitCallers
{
    /// <summary>
    /// Until disposed, the gits this flow asks are placed beneath the running git among the caller's <paramref name="ancestors"/>
    /// (nearest first), where one is; else as asked.
    /// </summary>
    public static IDisposable Beneath(Func<IReadOnlyList<int>> ancestors) => Beneath(GitScheduler.Shared, ancestors);

    internal static IDisposable Beneath(GitScheduler scheduler, Func<IReadOnlyList<int>> ancestors) =>
        GitScheduler.Beneath(scheduler.Any ? scheduler.PlaceOf(ancestors()) : null);
}
