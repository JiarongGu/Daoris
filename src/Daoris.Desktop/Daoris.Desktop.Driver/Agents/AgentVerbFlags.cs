namespace Daoris.Driver;

/// <summary>
/// The flags <c>login</c>, <c>trust</c>, <c>key</c> and <c>settings</c> take. <see cref="AgentsCommand.Operands"/> drops a flag it does not
/// know, so a stray one (<c>--profile work</c>, the retired name of <c>--account</c>) signed the order's first account in, or trusted a
/// folder for every account, and said nothing: any other flag is refused, as <c>account use</c> refuses its own.
/// </summary>
internal static class AgentVerbFlags
{
    private static readonly Dictionary<string, string[]> Taken = new(StringComparer.Ordinal)
    {
        ["login"] = ["--account", "--new", "--name", "--device-code"],
        ["trust"] = ["--account", "--yes", "--dry-run"],
        ["key"] = ["--account"],
        ["settings"] = ["--account", "--for"],
    };

    /// <summary>The refusal of the first flag <paramref name="verb"/> does not take, nothing done; a verb with no row here takes any.</summary>
    internal static void Refuse(string verb, IReadOnlyList<string> args)
    {
        if (!Taken.TryGetValue(verb, out var flags)) return;
        if (args.Skip(1).FirstOrDefault(word => word.StartsWith("--", StringComparison.Ordinal) && !flags.Contains(word, StringComparer.Ordinal)) is { } stray)
        {
            throw new AgentWords($"`{stray}` is not a flag of `agent {verb}` — it takes {string.Join(", ", flags.Select(flag => $"`{flag}`"))}.");
        }
    }
}
