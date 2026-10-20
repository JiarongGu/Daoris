namespace Daoris.Driver;

/// <summary>What <c>daoris-driver checks</c> was asked (FABRIC-PIPELINE1b): a repository, and its checks to set or a clear, or none to say.</summary>
public sealed record SessionChecksAsk
{
    /// <summary>The repository named, or null to list every repository's.</summary>
    public string? Repository { get; init; }

    /// <summary>The checks to set, as written: the door judges them.</summary>
    public string? Checks { get; init; }

    /// <summary>Whether the repository's rule is taken away.</summary>
    public bool Clear { get; init; }

    /// <summary>Whether the ask writes the driver's file.</summary>
    public bool Changes => Clear || Checks is not null;
}

/// <summary>
/// <c>daoris-driver checks</c> (FABRIC-PIPELINE1b, D192 point 4, D50): which checks a repository's sessions run under Current, from a
/// terminal: listed, said, set or cleared. It reads and writes the driver's file alone, through <see cref="SessionChecks.Apply"/>, the door
/// Setup's row goes through, so they refuse alike.
/// </summary>
/// <remarks>Exit codes are the family's: 0 said or set · 1 refused, nothing written · 2 could not, the usage among them.</remarks>
public static class SessionChecksCommand
{
    public const string Usage = "usage: daoris-driver checks [<repository> [light | heavy-at-end | --clear]]";

    /// <summary>What the words after <c>checks</c> ask, or null with what is wrong with them.</summary>
    public static SessionChecksAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args.Count == 0) return new SessionChecksAsk();
        var repository = args[0].Trim();
        if (args[0].StartsWith("--", StringComparison.Ordinal) || repository.Length == 0)
        {
            problem = $"`{args[0]}` is not a repository's name: `checks` takes a repository's name, then `light`, `heavy-at-end` or `--clear`.";
            return null;
        }

        var words = args.Skip(1).ToList();
        if (words.Count == 0) return new SessionChecksAsk { Repository = repository };
        if (words.Count > 1)
        {
            problem = "one change at a time: `light`, `heavy-at-end` or `--clear`.";
            return null;
        }

        if (words[0] == "--clear") return new SessionChecksAsk { Repository = repository, Clear = true };
        if (words[0].StartsWith("--", StringComparison.Ordinal))
        {
            problem = $"`{words[0]}` is not a session's checks: `checks` takes `light`, `heavy-at-end` or `--clear`.";
            return null;
        }

        return new SessionChecksAsk { Repository = repository, Checks = words[0] };
    }

    /// <summary>List every repository's rule, say one's, or make the change, written to the driver's file at <paramref name="configPath"/>.</summary>
    /// <exception cref="DriverConfigUnreadableException">The file does not read, so nothing was written.</exception>
    public static int Run(SessionChecksAsk ask, string configPath, TextWriter output)
    {
        var config = DriverConfig.Load(configPath);
        if (ask.Repository is not { } repository)
        {
            Every(config, output);
            return 0;
        }

        if (!ask.Changes)
        {
            output.WriteLine(Said(SessionChecks.For(config, repository), repository, now: false));
            return 0;
        }

        DriverConfig changed;
        try
        {
            changed = config.WithSessionChecks(repository, ask.Clear ? null : ask.Checks);
        }
        catch (DriverException refused)
        {
            output.WriteLine($"checks: {refused.Message}");
            return 1;
        }

        changed.Save(configPath);
        output.WriteLine(Said(SessionChecks.For(changed, repository), repository, now: true));
        return 0;
    }

    /// <summary>What a repository's sessions run, as a sentence; <paramref name="now"/> where it was just set.</summary>
    private static string Said(string? checks, string repository, bool now) => checks switch
    {
        SessionChecks.Light =>
            $"checks: `{repository}`'s sessions {(now ? "now " : "")}run light: the gates its {HeavyGates.FileName} does not mark heavy and the "
            + "checks a change reaches, filtered; its heavy gates are left to the verification and refused to them.",
        SessionChecks.HeavyAtEnd =>
            $"checks: `{repository}`'s sessions {(now ? "now " : "")}run the checks a change reaches while they work, and its heavy gates once, "
            + "at their end, through `run-heavy`.",
        _ => $"checks: `{repository}` sets no sessions' checks{(now ? " now" : "")}, so its sessions run their checks as they did.",
    };

    /// <summary>Every repository's rule by name, or that none are set.</summary>
    private static void Every(DriverConfig config, TextWriter output)
    {
        if (config.SessionChecks.Count == 0)
        {
            output.WriteLine("checks: no repository's sessions' checks are set on this machine, so each runs its checks as it did.");
            return;
        }

        output.WriteLine("checks: the sessions' checks set on this machine, each a repository's under Current:");
        foreach (var (repository, checks) in config.SessionChecks.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"  `{repository}`: {checks}");
        }
    }
}
