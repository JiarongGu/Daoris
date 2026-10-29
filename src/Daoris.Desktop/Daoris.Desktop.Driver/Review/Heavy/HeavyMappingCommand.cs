namespace Daoris.Driver;

/// <summary>What <c>daoris-driver heavy</c> was asked (HEAVYRUN1b2a): a repository, and its gates to set or a clear, or none to say.</summary>
public sealed record HeavyMappingAsk
{
    /// <summary>The repository named, or null to list every repository's.</summary>
    public string? Repository { get; init; }

    /// <summary>The gates to set, in the order they run, or null.</summary>
    public IReadOnlyList<HeavyGate>? Gates { get; init; }

    /// <summary>Whether the repository's gates are taken away.</summary>
    public bool Clear { get; init; }

    /// <summary>Whether the ask writes the driver's file.</summary>
    public bool Changes => Clear || Gates is not null;
}

/// <summary>
/// <c>daoris-driver heavy</c> (HEAVYRUN1b2a, D180's HEAVYRUN1b2a note, D50): this machine's heavy gates for a repository whose own
/// <c>daoris.gates.json</c> marks none, from a terminal: listed, said, set in the order they run, or cleared. It reads and writes the
/// driver's file alone, through <see cref="HeavyMapping"/>, the door the page's will go through (HEAVYRUN1b2b), so they refuse alike;
/// it never touches the repository.
/// </summary>
/// <remarks>Exit codes are the family's: 0 said or set · 1 refused, nothing written · 2 could not, the usage among them.</remarks>
public static class HeavyMappingCommand
{
    public const string Usage = "usage: daoris-driver heavy [<repository> [<name>=<command> [--cwd <folder>]… | --clear]]";

    private const string OneChange = "one change at a time: the gates, in the order they run, or `--clear` alone.";

    /// <summary>What the words after <c>heavy</c> ask, or null with what is wrong with them.</summary>
    public static HeavyMappingAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args.Count == 0) return new HeavyMappingAsk();
        var repository = args[0].Trim();
        if (args[0].StartsWith("--", StringComparison.Ordinal) || repository.Length == 0)
        {
            problem = $"`{args[0]}` is not a repository's name: `heavy` takes a repository's name, then its gates or `--clear`.";
            return null;
        }

        var words = args.Skip(1).ToList();
        if (words.Count == 0) return new HeavyMappingAsk { Repository = repository };
        if (words is ["--clear"]) return new HeavyMappingAsk { Repository = repository, Clear = true };

        var gates = new List<HeavyGate>();
        for (var at = 0; at < words.Count; at++)
        {
            var word = words[at];
            if (word == "--cwd")
            {
                if (gates.Count == 0)
                {
                    problem = "`--cwd` names the folder of the gate before it.";
                    return null;
                }

                if (at + 1 >= words.Count || words[at + 1].StartsWith("--", StringComparison.Ordinal) || words[at + 1].Trim().Length == 0)
                {
                    problem = "`--cwd` takes a folder under the repository's root.";
                    return null;
                }

                if (gates[^1].WorkingDirectory is not null)
                {
                    problem = $"`{gates[^1].Name}` runs in one folder.";
                    return null;
                }

                gates[^1] = gates[^1] with { WorkingDirectory = words[++at] };
                continue;
            }

            if (word == "--clear")
            {
                problem = OneChange;
                return null;
            }

            // The name is up to the first `=`, so a command may carry its own (`npm test -- --maxWorkers=2`).
            var equals = word.IndexOf('=', StringComparison.Ordinal);
            if (word.StartsWith("--", StringComparison.Ordinal) || equals < 0)
            {
                problem = $"`{word}` is not a gate: each is `<name>=<command>`, in the order they run, or `--clear` alone.";
                return null;
            }

            gates.Add(new HeavyGate(word[..equals], word[(equals + 1)..], null, Heavy: true));
        }

        return new HeavyMappingAsk { Repository = repository, Gates = gates };
    }

    /// <summary>
    /// List every repository's gates, say one's, or make the change, written to the driver's file at <paramref name="configPath"/>.
    /// </summary>
    /// <exception cref="DriverConfigUnreadableException">The file does not read, so nothing was written.</exception>
    public static int Run(HeavyMappingAsk ask, string configPath, TextWriter output)
    {
        var config = DriverConfig.Load(configPath);
        if (ask.Repository is not { } repository)
        {
            Every(config, output);
            return 0;
        }

        if (!ask.Changes)
        {
            Say(config, repository, output, now: false);
            return 0;
        }

        DriverConfig changed;
        try
        {
            changed = config.WithHeavyGates(repository, ask.Clear ? null : ask.Gates);
        }
        catch (DriverException refused)
        {
            output.WriteLine($"heavy: {refused.Message}");
            return 1;
        }

        changed.Save(configPath);
        Say(changed, repository, output, now: true);
        return 0;
    }

    /// <summary>One repository's gates in the order they run, each its command and its folder; or that it has none here.</summary>
    private static void Say(DriverConfig config, string repository, TextWriter output, bool now)
    {
        if (HeavyMapping.For(config, repository) is not { } gates)
        {
            output.WriteLine($"heavy: `{repository}` has no heavy gates on this machine{(now ? " now" : "")}: `run-heavy` runs those its own {HeavyGates.FileName} marks.");
            return;
        }

        output.WriteLine($"heavy: `{repository}`'s heavy gates on this machine{(now ? " are now these" : "")}, run in this order where its own {HeavyGates.FileName} marks none:");
        var width = gates.Max(gate => gate.Name.Length);
        foreach (var gate in gates)
        {
            output.WriteLine($"  {gate.Name.PadRight(width)}  {gate.Run}{(gate.WorkingDirectory is { } folder ? $"  (in {folder})" : "")}");
        }
    }

    /// <summary>Every repository's gates by name, or that none are set.</summary>
    private static void Every(DriverConfig config, TextWriter output)
    {
        if (config.HeavyGates.Count == 0)
        {
            output.WriteLine($"heavy: no repository's heavy gates are set on this machine; `run-heavy` runs those a repository's own {HeavyGates.FileName} marks.");
            return;
        }

        output.WriteLine($"heavy: the heavy gates set on this machine, each used where its repository's own {HeavyGates.FileName} marks none:");
        foreach (var (repository, gates) in config.HeavyGates.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"  `{repository}`: {string.Join(", ", gates.Select(gate => gate.Name))}");
        }
    }
}
