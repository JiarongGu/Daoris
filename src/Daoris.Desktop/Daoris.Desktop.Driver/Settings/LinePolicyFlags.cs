using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// The line policy's keys as <c>daoris place line</c> takes them, the terminal's door to what <c>SET_LINE</c> edits beside the branch
/// (one truth, two doors): <c>--request</c>, <c>--commit</c>, <c>--batch</c>, <c>--order</c>, <c>--gates</c>, <c>--unreached</c>,
/// <c>--reached</c>, <c>--conflicts</c> and <c>--integrate-line</c>, each judged by <see cref="LinePolicyKey"/> as the window's press is,
/// and <c>--no-policy</c> withdrawing the policy. The flags merge into the policy the place already holds, so one key changes alone.
/// </summary>
internal static class LinePolicyFlags
{
    /// <summary>The flags that carry a value, each a key of the policy (<c>--integrate-line</c> is <c>integrate.line</c>, the line by name).</summary>
    internal static readonly string[] Valued =
        ["--request", "--commit", "--batch", "--order", "--gates", "--unreached", "--reached", "--conflicts", "--integrate-line"];

    /// <summary>The switch that withdraws the place's own policy.</summary>
    internal const string Withdraw = "--no-policy";

    /// <summary>Whether the words name the policy at all: a key's flag, or its withdrawal.</summary>
    internal static bool Names(IReadOnlyList<string> args) => args.Any(arg => arg == Withdraw || Valued.Contains(arg));

    /// <summary>
    /// The keys the words name, and only those: laid over the policy the place holds when the change is made, so a key the words leave
    /// out is never written back from an older read. Null where the words withdraw the policy. A value the policy does not take is
    /// refused in <see cref="LinePolicyKey"/>'s sentence, nothing done.
    /// </summary>
    internal static LandingIntegrate? Named(IReadOnlyList<string> args)
    {
        if (args.Contains(Withdraw))
        {
            if (args.Any(arg => Valued.Contains(arg))) throw new ChoiceWords($"`{Withdraw}` withdraws the policy, so it names no key beside it.");
            return null;
        }

        var policy = new LandingIntegrate();
        if (FlagValue(args, "--request") is { } request) policy = policy with { Request = request };
        if (FlagValue(args, "--commit") is { } commit) policy = policy with { Commit = commit };
        if (FlagValue(args, "--batch") is { } batch)
        {
            var count = Number(batch);
            if (!IsWhole(count) || count is < int.MinValue or > int.MaxValue) throw new ChoiceWords(LinePolicyKey.BatchShape);
            policy = policy with { Batch = (int)count };
        }

        if (FlagValue(args, "--order") is { } order) policy = policy with { Order = order };
        if (FlagValue(args, "--gates") is { } gates) policy = policy with { Gates = gates };
        if (FlagValue(args, "--unreached") is { } unreached) policy = policy with { Unreached = unreached };
        if (FlagValue(args, "--reached") is { } reached) policy = policy with { Reached = reached };
        if (FlagValue(args, "--conflicts") is { } conflicts) policy = policy with { Conflicts = conflicts };
        if (FlagValue(args, "--integrate-line") is { } line) policy = policy with { Line = line };

        if (LinePolicyKey.PolicyProblem(policy, wholeSet: true) is { } problem) throw new ChoiceWords(problem);
        return policy;
    }

    /// <summary>The keys a policy sets, as the terminal says them, in the order the file writes them.</summary>
    internal static string Listed(LandingIntegrate policy)
    {
        var said = new List<string>();
        if (policy.Request is not null) said.Add($"request {policy.Request}");
        if (policy.Commit is not null) said.Add($"commit {policy.Commit}");
        if (policy.Batch is { } batch) said.Add($"batch {batch}");
        if (policy.Order is not null) said.Add($"order {policy.Order}");
        if (policy.Gates is not null) said.Add($"gates {policy.Gates}");
        if (policy.Unreached is not null) said.Add($"unconfirmed unreached {policy.Unreached}");
        if (policy.Reached is not null) said.Add($"unconfirmed reached {policy.Reached}");
        if (policy.Conflicts is not null) said.Add($"conflicts {policy.Conflicts}");
        if (policy.Line is not null) said.Add($"line {policy.Line}");
        return said.Count == 0 ? "every key at its default" : string.Join(", ", said);
    }
}
