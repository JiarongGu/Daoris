namespace Daoris.Driver;

/// <summary>
/// The look's tidy of a folder department's copies, as the look's own tidy asks it (<see cref="BranchTidying.Copies"/>): each copy no
/// run will bring back any more moved aside into the trees' <c>.tidied</c>, as a tree is. The copies and the runs they worked for live
/// above the workplace, so the composition root hands the one that reads them.
/// </summary>
public interface ICopyTidying
{
    /// <summary>Each orphan copy moved aside, or kept where the move did not happen.</summary>
    /// <param name="inUse">The folders sessions hold: a copy one works in is no orphan.</param>
    IReadOnlyList<CopySetAside> Tidy(IReadOnlySet<string> inUse, DateTimeOffset now);
}

/// <summary>A copy the tidy took: moved aside to <paramref name="MovedTo"/>, or kept for <paramref name="Kept"/>.</summary>
/// <param name="Folder">The name of the folder it is a copy of.</param>
public sealed record CopySetAside(string Workspace, string Department, string Copy, string Folder, string? MovedTo, string? Kept)
{
    /// <summary>The tidy's line for it, the set-aside wait <paramref name="days"/> long.</summary>
    public string Said(double days)
    {
        if (Kept is { } why) return $"tidy  {Department}: copy `{Copy}` of `{Folder}` stays, though its run ended and nothing returns it, since it could not be moved aside ({why}).";
        return MovedTo is { } to
            ? $"tidy  {Department}: copy `{Copy}` of `{Folder}`: its run ended and nothing returns it, so it was moved aside to {to}, and goes once it is left untouched for {days:0} days."
            : $"tidy  {Department}: copy `{Copy}` of `{Folder}`: its run ended and nothing returns it, and its folder was already gone, so its record went.";
    }
}
