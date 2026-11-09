namespace Daoris.Driver;

/// <summary>
/// The flags each <c>agent</c> verb takes. <see cref="AgentsCommand.Operands"/> drops a flag it does not know, so a stray one
/// (<c>--profile work</c>, the retired name of <c>--account</c>; <c>--repo</c> for <c>--repository</c>) signed the order's first account
/// in, trusted a folder for every account, allowed a rule to every session on the machine or moved the machine's pin, and said nothing:
/// any other flag is refused, as <c>account use</c> refuses its own.
/// </summary>
internal static class AgentVerbFlags
{
    private static readonly Dictionary<string, string[]> Taken = new(StringComparer.Ordinal)
    {
        ["list"] = ["--again"],
        ["rules"] = ["--workspace", "--repository", "--note"],
        ["trust"] = ["--account", "--yes", "--dry-run"],
        ["settings"] = ["--account", "--for"],
        ["which"] = ["--workspace", "--json"],
        ["unpin"] = ["--workspace"],
        ["profile"] = ["--clear", "--own"],
        ["install"] = [],
        ["key"] = ["--account", "--remove"],
        ["login"] = ["--account", "--new", "--name", "--device-code"],
        ["update"] = ["--workspace"],
        ["pin"] = ["--workspace"],
    };

    /// <summary>The verbs whose next word is a verb of their own, read by its place.</summary>
    private static readonly Dictionary<string, IReadOnlyList<string>> Words = new(StringComparer.Ordinal)
    {
        ["profile"] = AgentAccountsCommand.ProfileVerbs,
        ["rules"] = AgentRulesVerb.Verbs,
    };

    /// <summary>The refusal of the first flag <paramref name="verb"/> does not take, nothing done.</summary>
    internal static void Refuse(string verb, IReadOnlyList<string> args)
    {
        if (!Taken.TryGetValue(verb, out var flags)) return;

        // A flag is its verb's: a word where the verb stands that is not one is refused in the verb's own words, as the window
        // refuses it. `account use` has the flags of the use settings and refuses a retired one in words of its own.
        if (args.Count > 1 && Words.TryGetValue(verb, out var words)
            && (!words.Contains(args[1], StringComparer.Ordinal) || (verb == "profile" && args[1] == "use"))) return;
        if (args.Skip(1).FirstOrDefault(word => word.StartsWith("--", StringComparison.Ordinal) && !flags.Contains(word, StringComparer.Ordinal)) is { } stray)
        {
            var takes = flags.Length == 0 ? "no flag" : string.Join(", ", flags.Select(flag => $"`{flag}`"));
            throw new AgentWords($"`{stray}` is not a flag of `agent {verb}` — it takes {takes}.");
        }
    }
}
