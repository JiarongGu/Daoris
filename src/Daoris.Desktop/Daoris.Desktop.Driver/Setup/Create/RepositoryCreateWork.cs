namespace Daoris.Driver;

/// <summary>
/// Where the person's work follows the set-up. The work is one quest to the new department, chained behind its set-up quest as the set-up
/// press's one door carries it (<see cref="SetupPlan.Then"/>): the service publishes it when the set-up closes done, asked on behalf of the
/// same asker, and a decline of the set-up ends the chain. Nothing of the work is in the set-up's own words, and the driver publishes nothing
/// of it, so a retry that finds the set-up quest has found the chain.
/// </summary>
public static class RepositoryCreateWork
{
    /// <summary>How long a title may run before it is cut at a word, as an ask's first line becomes a quest's.</summary>
    private const int TitleLength = 100;

    /// <summary>
    /// The step the creation's work becomes, or null where it has none: the first line of the request as the title, the request whole and the
    /// department's purpose as the body, with the set-up named by <c>{parent}</c> for the service to put its id in.
    /// </summary>
    public static SetupThen? StepOf(CreateAsk ask)
    {
        if (ask.Work is not { Length: > 0 } work) return null;
        var body = $"{work}\n\n— Chained behind {{parent}}, the set-up of `{ask.Name}`: `{ask.Name}` was opened for {ask.Purpose}. "
                   + "This starts once that set-up is done, so the repository's own brief and checks are in place.";
        return new SetupThen(TitleOf(work), body);
    }

    private static string TitleOf(string work)
    {
        var line = work.Split('\n', 2)[0].Trim();
        if (line.Length <= TitleLength) return line;
        var cut = line[..TitleLength];
        var space = cut.LastIndexOf(' ');
        return (space > TitleLength / 2 ? cut[..space] : cut).TrimEnd() + "…";
    }
}
