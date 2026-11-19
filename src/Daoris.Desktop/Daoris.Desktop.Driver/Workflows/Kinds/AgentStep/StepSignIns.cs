namespace Daoris.Driver;

/// <summary>
/// The words a step fails <see cref="StepAnswer.SignIn"/> with, from its sessions' ends in the order run: the accounts its agent refused
/// for their sign-in, the others the walk passed not signed in, and the sign-in to make. Machine-local, as the step's answer is: it names
/// accounts by the person's names, and the sign-in by the id a shell takes.
/// </summary>
public static class StepSignIns
{
    /// <summary>
    /// The step's words where its last session's end names a sign-in; null where the last ran, whatever the one before it said, so its own
    /// answer, or its want of one, is the step's.
    /// </summary>
    /// <param name="names">The names the person gave the agent's accounts, by id.</param>
    public static string? Words(IReadOnlyList<StepSessionEnd> ends, IReadOnlyDictionary<string, string> names)
    {
        if (ends.Count == 0 || ends[^1] is { RefusedOn: null, SignedOut.Count: 0 } || ends.Select(end => end.Agent).OfType<string>().FirstOrDefault() is not { } agent)
        {
            return null;
        }

        var refused = ends.Select(end => end.RefusedOn).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var others = ends[^1].SignedOut.Where(account => !refused.Contains(account, StringComparer.OrdinalIgnoreCase)).ToList();
        if (refused.Count == 0 && others.Count == 0) return null;

        string Said(IReadOnlyList<string> accounts) => Joined([.. accounts.Select(account => $"`{AccountNames.Said(names, account)}`")]);
        string Are(IReadOnlyList<string> accounts) => accounts.Count == 1 ? "is" : "are";
        var what = refused.Count == 0
            ? $"No `{agent}` account could take this step: {Said(others)} {Are(others)} not signed in"
            : $"`{agent}` refused this step's session on {Said(refused)} for {(refused.Count == 1 ? "its" : "their")} sign-in"
              + (others.Count == 0 ? "" : $", and no other account could take it: {Said(others)} {Are(others)} not signed in either");
        var first = refused.Count > 0 ? refused[0] : others[0];
        return $"{what}. Sign one in on its card under Agents, or with `daoris agent login {agent} --account {ShellWord.Of(first, ShellWord.Account)}`, "
               + "then try the step again.";
    }

    private static string Joined(IReadOnlyList<string> said) =>
        said.Count == 1 ? said[0] : $"{string.Join(", ", said.Take(said.Count - 1))} and {said[^1]}";
}
