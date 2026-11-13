namespace Daoris.Driver;

/// <summary>The ask and the part a landing is for.</summary>
public sealed record TrainRef(string Ask, string Part);

/// <summary>The ask's branch: the line its parts' landings integrate into, one candidate at a time as on every line.</summary>
public static class TrainBranch
{
    /// <summary>The ask's branch name, <c>daoris/ask-&lt;id&gt;</c>.</summary>
    public static string Of(string ask) => $"{Prefix}{ask.TrimStart('#')}";

    private const string Prefix = "daoris/ask-";

    /// <summary>The ask a line is the branch of; null for any other line.</summary>
    public static string? AskOf(string line) => line.StartsWith(Prefix, StringComparison.Ordinal) && line.Length > Prefix.Length ? line[Prefix.Length..] : null;

    /// <summary>
    /// Make the ask's branch at <paramref name="from"/> (the line's commit) where the repository has none; whether it stands. An existing
    /// branch is left exactly where it is.
    /// </summary>
    public static async Task<bool> EnsureAsync(string root, string ask, string from, CancellationToken ct)
    {
        var name = Of(ask);
        var (found, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{name}^{{commit}}"], ct).ConfigureAwait(false);
        if (found == 0) return true;
        var (made, _, _) = await WorkingTree.GitAsync(root, ["branch", name, from], ct).ConfigureAwait(false);
        return made == 0;
    }
}

/// <summary>The service's answer for an ask's plan, read for the train.</summary>
public sealed class ServiceTrainPlans(ServiceClient service) : ITrainPlans
{
    public async Task<TrainPlan?> OfAsync(string ask, CancellationToken ct)
    {
        try
        {
            return TrainPlan.Of(await service.AskPlanAsync(ask, ct).ConfigureAwait(false));
        }
        catch (Exception error) when (!ct.IsCancellationRequested && error is DriverException or HttpRequestException or IOException or TimeoutException)
        {
            // Not read: the ask's parts wait for the next look, never guessed.
            return null;
        }
    }
}
