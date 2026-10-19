using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a heavy gate runs once its test runner's workers are bounded, and the sentence saying what was bound or why nothing was.</summary>
/// <param name="Run">The gate's command line: as written, or with the bound at its end.</param>
/// <param name="Said">One sentence naming the gate, without the verb's prefix.</param>
public sealed record HeavyBound(string Run, string Said);

/// <summary>
/// A heavy gate's test runner held to two workers (HEAVYRUN1a2, D180's HEAVYRUN1 notes): Jest's and Vitest's own <c>--maxWorkers=2</c>,
/// the pool left as the repository has it, so a heavy run beside a session's own work does not take every core.
/// </summary>
/// <remarks>
/// <para><b>The runner is what the repository declares, never a guess at the text.</b> A gate whose one command is <c>jest</c> or
/// <c>vitest</c>, or either through <c>npx</c>, is that runner; <c>npm test</c> and <c>npm run &lt;script&gt;</c> are read through the
/// gate folder's own <c>package.json</c>, and bound through npm, after its <c>--</c>, only where that script is one command whose runner is
/// Jest or Vitest. A chain in the gate or its script, Angular's <c>ng test</c>, any other runner, npm pointed at another package, a script
/// that cannot be read, a runner whose options already end with <c>--</c>: each runs as written, said.</para>
///
/// <para><b>An owner's bound is never doubled.</b> A gate or script that already names <c>--maxWorkers</c>, <c>--max-workers</c>, <c>-w</c>,
/// <c>--pool</c> or <c>--poolOptions</c> keeps it untouched: the bound an owner writes into this machine's mapping
/// (<c>npm test -- --maxWorkers=2</c>, D180's HEAVYRUN1b2a note) is one of these.</para>
///
/// <para><b>Bound only at a version whose flag was read.</b> The installed version is read from
/// <c>node_modules/&lt;jest|vitest&gt;/package.json</c> in the gate's folder, then each folder above it up to the checkout, the nearest
/// winning as node resolves it; reading it starts no process. The majors in <see cref="Jest"/> and <see cref="Vitest"/> are those whose
/// command line was read declaring <c>maxWorkers</c> (D180's HEAVYRUN1a2 note names each package and line); semver keeps a flag through its
/// major, and a prerelease promises nothing, so any other version runs as written, said.</para>
/// </remarks>
public static class HeavyWorkers
{
    /// <summary>The bound a heavy gate's runner is handed: Jest's and Vitest's own flag, two workers.</summary>
    public const string Flag = "--maxWorkers=2";

    /// <summary>Jest, whose CLI declares <c>maxWorkers</c> (alias <c>w</c>) in jest-cli 27.5.1, 29.7.0, 30.3.0 and 30.5.2.</summary>
    private static readonly Runner Jest = new("jest", "Jest", [27, 29, 30]);

    /// <summary>Vitest, whose CLI declares <c>maxWorkers</c> in 2.1.9, 3.1.2, 3.2.4, 4.1.5, 4.1.11, 5.0.1 and 5.0.5.</summary>
    private static readonly Runner Vitest = new("vitest", "Vitest", [2, 3, 4, 5]);

    private static readonly StringComparison Paths = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Said of a runner whose options already end: a bound after a bare <c>--</c> is read as a test's name.</summary>
    private const string EndedSaid = "ends its runner's options with `--`, so a bound after them would not be read as one; it runs as written.";

    /// <summary>
    /// <paramref name="gate"/>'s command with its runner's workers bounded, run in <paramref name="folder"/> of <paramref name="checkout"/>;
    /// or as written, saying why.
    /// </summary>
    public static HeavyBound Bind(HeavyGate gate, string folder, string checkout)
    {
        var named = $"`{gate.Name}`";
        var run = gate.Run.TrimEnd();
        if (Words(run) is not { } words) return As(gate, $"{named} is more than one command, so it runs as written.");
        if (words.Count == 0) return As(gate, $"{named} names no command, so it runs as written.");
        if (Is(words[0], "npm")) return Npm(gate, run, words, folder, checkout);

        var (runner, at, other) = Classify(words);
        if (runner is null) return As(gate, $"{named} {other}, so it runs as written.");
        if (Own(words) is { } own) return As(gate, $"{named} names its own worker bound (`{own}`), so it runs as written.");
        if (Ended(words, at + 1)) return As(gate, $"{named} {EndedSaid}");
        return Versioned(gate, runner, "", folder, checkout, $"{run} {Flag}");
    }

    /// <summary><c>npm test</c> or <c>npm run &lt;script&gt;</c>: the script read from the folder's own package.json, bound after npm's <c>--</c>.</summary>
    private static HeavyBound Npm(HeavyGate gate, string run, List<string> words, string folder, string checkout)
    {
        var named = $"`{gate.Name}`";
        var verb = words.Count > 1 ? words[1] : "";
        string script;
        int after;
        if (Is(verb, "test") || Is(verb, "t") || Is(verb, "tst"))
        {
            (script, after) = ("test", 2);
        }
        else if (Is(verb, "run") || Is(verb, "run-script") || Is(verb, "rum") || Is(verb, "urn"))
        {
            if (words.Count < 3 || words[2].StartsWith('-')) return As(gate, $"{named} runs `npm run` with no script named after it, so it runs as written.");
            (script, after) = (words[2], 3);
        }
        else
        {
            return As(gate, $"{named} runs `{(verb.Length > 0 ? $"npm {verb}" : "npm")}`, neither Jest nor Vitest, so it runs as written.");
        }

        // npm's own options come before its `--`; one that points it at another package reads a package.json that is not the folder's.
        var dashes = words.IndexOf("--", after);
        var options = words.Skip(after).Take((dashes < 0 ? words.Count : dashes) - after);
        if (options.FirstOrDefault(Elsewhere) is { } elsewhere)
        {
            return As(gate, $"{named} points npm at another package (`{elsewhere}`), whose script is not read here, so it runs as written.");
        }

        if (Script(folder, script, out var problem) is not { } declared)
        {
            return As(gate, $"{named} runs npm's `{script}` script, and its folder{problem}, so it runs as written.");
        }

        var scripted = $"`{gate.Name}`'s npm script `{script}`";
        if (Words(declared) is not { } scriptWords) return As(gate, $"{scripted} is more than one command, so it runs as written.");
        if (scriptWords.Count == 0) return As(gate, $"{scripted} names no command, so it runs as written.");

        var (runner, at, other) = Classify(scriptWords);
        if (runner is null) return As(gate, $"{scripted} {other}, so it runs as written.");
        if (Own(words) is { } own) return As(gate, $"{named} names its own worker bound (`{own}`), so it runs as written.");
        if (Own(scriptWords) is { } owned) return As(gate, $"{scripted} names its own worker bound (`{owned}`), so it runs as written.");
        if (dashes >= 0 && Ended(words, dashes + 1)) return As(gate, $"{named} {EndedSaid}");
        if (Ended(scriptWords, at + 1)) return As(gate, $"{scripted} {EndedSaid}");

        // What follows npm's `--` reaches the script's runner; a gate that already has one passes the bound after it.
        return Versioned(gate, runner, $" through npm's `{script}` script", folder, checkout, dashes >= 0 ? $"{run} {Flag}" : $"{run} -- {Flag}");
    }

    /// <summary>The bound where the installed version's flag is known; as written, said, where it is not or cannot be told.</summary>
    private static HeavyBound Versioned(HeavyGate gate, Runner runner, string via, string folder, string checkout, string bound)
    {
        var named = $"`{gate.Name}`";
        if (!Installed(runner.Package, folder, checkout, out var version))
        {
            return As(gate, $"{named} runs {runner.Name}{via}, which is not installed in its folder or above it in the checkout, so its version is not known and it runs as written.");
        }

        if (version is null) return As(gate, $"{named} runs {runner.Name}{via}, whose installed package.json gives no readable version, so it runs as written.");
        if (!runner.Takes(version))
        {
            return As(gate, $"{named} runs {runner.Name} {version}{via}, and this {runner.Name} version's worker flag is not known here, so it runs as written.");
        }

        return new HeavyBound(bound, $"{named} runs {runner.Name} {version}{via}, so its workers are bounded to two (`{Flag}`).");
    }

    /// <summary>The runner a command's words name, at the word naming it; or null and what they run instead.</summary>
    private static (Runner? Runner, int At, string Other) Classify(List<string> words)
    {
        var at = Is(words[0], "npx") && words.Count > 1 ? 1 : 0;
        var word = words[at];
        if (Is(word, "jest")) return (Jest, at, "");
        if (Is(word, "vitest")) return (Vitest, at, "");
        if (Is(word, "ng") && at + 1 < words.Count && Is(words[at + 1], "test")) return (null, at, "runs Angular's `ng test`, whose builder takes its own flags");
        var shown = at == 1 ? $"npx {word}" : Is(word, "npm") && words.Count > 1 ? $"npm {words[1]}" : word;
        return (null, at, $"runs `{shown}`, neither Jest nor Vitest");
    }

    /// <summary>The first word naming a worker bound or a pool, the owner's own; or null.</summary>
    private static string? Own(IEnumerable<string> words) => words.FirstOrDefault(word =>
        Names(word, "--maxWorkers") || Names(word, "--max-workers") || Names(word, "--pool")
        || word.StartsWith("--poolOptions", StringComparison.OrdinalIgnoreCase) || word.StartsWith("--pool-options", StringComparison.OrdinalIgnoreCase)
        || word == "-w" || word.StartsWith("-w=", StringComparison.Ordinal) || (word.Length > 2 && word.StartsWith("-w", StringComparison.Ordinal) && char.IsAsciiDigit(word[2])));

    /// <summary>An npm option that points it at another package: a workspace, or another prefix.</summary>
    private static bool Elsewhere(string word) =>
        word is "-w" or "-ws" or "-C" || word.StartsWith("-w=", StringComparison.Ordinal)
        || Names(word, "--workspace") || Names(word, "--workspaces") || Names(word, "--prefix");

    /// <summary>Whether a runner's words from <paramref name="from"/> on already end its options with a bare <c>--</c>.</summary>
    private static bool Ended(List<string> words, int from) => from < words.Count && words.IndexOf("--", from) >= 0;

    private static bool Names(string word, string flag) =>
        word.Equals(flag, StringComparison.OrdinalIgnoreCase) || word.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase);

    private static bool Is(string word, string name) => word.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static HeavyBound As(HeavyGate gate, string said) => new(gate.Run, said);

    /// <summary>
    /// The words of one command, quotes taken off; or null where it is more than one: a separator, a pipe or a redirection outside
    /// quotes, as the shell would read it, since a bound added at the end would reach only its last part.
    /// </summary>
    private static List<string>? Words(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var quoted = false;
        var any = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
                any = true;
                continue;
            }

            if (!quoted && c is '&' or '|' or ';' or '<' or '>') return null;
            if (!quoted && char.IsWhiteSpace(c))
            {
                if (any) words.Add(word.ToString());
                word.Clear();
                any = false;
                continue;
            }

            word.Append(c);
            any = true;
        }

        if (any) words.Add(word.ToString());
        return words;
    }

    /// <summary>The script <paramref name="folder"/>'s own package.json declares by <paramref name="name"/>; or null, with what is wrong.</summary>
    private static string? Script(string folder, string name, out string problem)
    {
        problem = "";
        var file = Path.Combine(folder, "package.json");
        if (!File.Exists(file))
        {
            problem = " has no package.json";
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Object
                && scripts.TryGetProperty(name, out var script) && script.ValueKind == JsonValueKind.String)
            {
                return script.GetString();
            }

            problem = $"'s package.json declares no `{name}` script";
        }
        catch (JsonException)
        {
            problem = "'s package.json is not valid JSON";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            problem = $"'s package.json could not be read ({error.Message})";
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="package"/> is installed in <paramref name="folder"/> or a folder above it up to <paramref name="checkout"/>,
    /// never past it, the nearest winning; and its version, null where its package.json gives none that reads.
    /// </summary>
    private static bool Installed(string package, string folder, string checkout, out string? version)
    {
        version = null;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(checkout));
        var at = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        while (at is not null && (at.Equals(root, Paths) || at.StartsWith(root + Path.DirectorySeparatorChar, Paths)))
        {
            var manifest = Path.Combine(at, "node_modules", package, "package.json");
            if (File.Exists(manifest))
            {
                version = VersionOf(manifest);
                return true;
            }

            if (at.Equals(root, Paths)) break;
            at = Path.GetDirectoryName(at);
        }

        return false;
    }

    private static string? VersionOf(string manifest)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String
                   && version.GetString()!.Trim() is { Length: > 0 } text
                ? text
                : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A test runner: its package, its name as said, and the majors whose command line was read declaring <c>maxWorkers</c>.</summary>
    private sealed record Runner(string Package, string Name, int[] Majors)
    {
        /// <summary>Whether <paramref name="version"/> is a release of a major read declaring the flag; a prerelease promises nothing.</summary>
        public bool Takes(string version)
        {
            if (version.Contains('-', StringComparison.Ordinal)) return false;
            var dot = version.IndexOf('.', StringComparison.Ordinal);
            return int.TryParse(dot < 0 ? version : version[..dot], NumberStyles.None, CultureInfo.InvariantCulture, out var major) && Majors.Contains(major);
        }
    }
}
