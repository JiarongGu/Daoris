using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
/// Jest or Vitest. <c>yarn test</c>, <c>yarn run &lt;script&gt;</c>, <c>pnpm test</c> and <c>pnpm run &lt;script&gt;</c> are read the same
/// way and bound as the yarn or pnpm the folder runs hands a script its words (HEAVYRUN1a3), only where that version is recorded and was
/// read. A chain in the gate or its script, Angular's <c>ng test</c>, any other runner, npm or pnpm pointed at other packages, a tool's
/// own options before its script, a script that cannot be read, a runner whose options already end with <c>--</c>: each runs as
/// written, said.</para>
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
        if (Is(words[0], "yarn")) return Yarn(gate, run, words, folder, checkout);
        if (Is(words[0], "pnpm")) return Pnpm(gate, run, words, folder, checkout);

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

        var declared = Declared(gate, words, "npm", script, folder);
        if (declared.Refusal is { } refused) return As(gate, refused);
        return AfterDashes(gate, run, words, dashes, declared, $" through npm's `{script}` script", folder, checkout);
    }

    /// <summary>
    /// <c>yarn test</c> or <c>yarn run &lt;script&gt;</c> (HEAVYRUN1a3): every yarn read (1–4) hands each word after the script's name to
    /// the script, so the bound goes at the end; a <c>--</c> there is taken away by yarn 1 and handed on by yarn 2 and later, so it runs as
    /// written. Bound only where the yarn the folder runs is known (<see cref="ManagerVersion"/>) and is one of those majors.
    /// </summary>
    private static HeavyBound Yarn(HeavyGate gate, string run, List<string> words, string folder, string checkout)
    {
        var named = $"`{gate.Name}`";
        var verb = words.Count > 1 ? words[1] : "";
        string script;
        int after;
        if (verb == "test")
        {
            (script, after) = ("test", 2);
        }
        else if (verb == "run")
        {
            if (words.Count < 3 || words[2].StartsWith('-')) return As(gate, $"{named} runs `yarn run` with no script named after it, so it runs as written.");
            (script, after) = (words[2], 3);
        }
        else if (verb.StartsWith('-'))
        {
            return As(gate, $"{named} names yarn's own options before its script, so it runs as written.");
        }
        else
        {
            return As(gate, $"{named} runs `{(verb.Length > 0 ? $"yarn {verb}" : "yarn")}`, which is not `yarn test` or `yarn run <script>`, so it runs as written.");
        }

        var declared = Declared(gate, words, "yarn", script, folder);
        if (declared.Refusal is { } refused) return As(gate, refused);
        if (words.IndexOf("--", after) >= 0)
        {
            return As(gate, $"{named} puts `--` after yarn's script, which yarn 1 takes away and yarn 2 and later hand on, so it runs as written.");
        }

        if (ManagerVersion("yarn", folder, checkout, out var other) is not { } version) return As(gate, Unrecorded(gate, "yarn", script, other));
        if (Major(version) is not (>= 1 and <= 4)) return As(gate, Unforwarded(gate, "yarn", version, script));
        if (Ended(declared.Words, declared.At + 1)) return As(gate, $"{declared.Named} {EndedSaid}");
        return Versioned(gate, declared.Runner!, $" through yarn {version}'s `{script}` script", folder, checkout, $"{run} {Flag}");
    }

    /// <summary>
    /// <c>pnpm test</c> (<c>t</c>, <c>tst</c>) or <c>pnpm run &lt;script&gt;</c> (HEAVYRUN1a3), bound as the pnpm the folder runs takes a
    /// script's words: pnpm 6 reads every flag as its own until its <c>--</c>, and its test command does so through pnpm 9; from pnpm 7
    /// <c>run</c> hands the script every word after its name, and from pnpm 10 <c>test</c> does too, spelled so (never <c>t</c> or
    /// <c>tst</c>). Bound only where that pnpm is known (<see cref="ManagerVersion"/>) and is 6 to 11.
    /// </summary>
    private static HeavyBound Pnpm(HeavyGate gate, string run, List<string> words, string folder, string checkout)
    {
        var named = $"`{gate.Name}`";
        var verb = words.Count > 1 ? words[1] : "";
        string script;
        int after;
        if (verb is "test" or "t" or "tst")
        {
            (script, after) = ("test", 2);
        }
        else if (verb is "run" or "run-script")
        {
            if (words.Count < 3 || words[2].StartsWith('-')) return As(gate, $"{named} runs `pnpm {verb}` with no script named after it, so it runs as written.");
            (script, after) = (words[2], 3);
        }
        else if (verb.StartsWith('-'))
        {
            return As(gate, $"{named} names pnpm's own options before its script, so it runs as written.");
        }
        else
        {
            return As(gate, $"{named} runs `{(verb.Length > 0 ? $"pnpm {verb}" : "pnpm")}`, which is not `pnpm test` or `pnpm run <script>`, so it runs as written.");
        }

        var declared = Declared(gate, words, "pnpm", script, folder);
        if (declared.Refusal is { } refused) return As(gate, refused);
        if (ManagerVersion("pnpm", folder, checkout, out var other) is not { } version) return As(gate, Unrecorded(gate, "pnpm", script, other));

        var major = Major(version);
        var verbatim = major switch
        {
            6 => false,
            >= 7 and <= 9 => after == 3,
            >= 10 and <= 11 => after == 3 || verb == "test",
            _ => (bool?)null,
        };
        if (verbatim is null) return As(gate, Unforwarded(gate, "pnpm", version, script));

        var via = $" through pnpm {version}'s `{script}` script";
        if (verbatim == true)
        {
            // Every word after the script's name reaches its runner, a `--` among them too.
            if (Ended(words, after)) return As(gate, $"{named} {EndedSaid}");
            if (Ended(declared.Words, declared.At + 1)) return As(gate, $"{declared.Named} {EndedSaid}");
            return Versioned(gate, declared.Runner!, via, folder, checkout, $"{run} {Flag}");
        }

        // pnpm's own options come before its `--`; one that points it at other packages runs their scripts, not the folder's.
        var dashes = words.IndexOf("--", after);
        var options = words.Skip(after).Take((dashes < 0 ? words.Count : dashes) - after);
        if (options.FirstOrDefault(PnpmElsewhere) is { } elsewhere)
        {
            return As(gate, $"{named} points pnpm at other packages (`{elsewhere}`), whose scripts are not read here, so it runs as written.");
        }

        return AfterDashes(gate, run, words, dashes, declared, via, folder, checkout);
    }

    /// <summary>
    /// A script whose tool ends its own options with <c>--</c> and hands the script what follows it (npm; pnpm where it reads flags as
    /// its own): the bound after the gate's <c>--</c>, or after one added.
    /// </summary>
    private static HeavyBound AfterDashes(HeavyGate gate, string run, List<string> words, int dashes, Scripted declared, string via, string folder, string checkout)
    {
        if (dashes >= 0 && Ended(words, dashes + 1)) return As(gate, $"`{gate.Name}` {EndedSaid}");
        if (Ended(declared.Words, declared.At + 1)) return As(gate, $"{declared.Named} {EndedSaid}");
        return Versioned(gate, declared.Runner!, via, folder, checkout, dashes >= 0 ? $"{run} {Flag}" : $"{run} -- {Flag}");
    }

    /// <summary>A script a gate runs through its package manager: its runner, its words, the word naming the runner and how it is said; or why it is not bound.</summary>
    private sealed record Scripted(Runner? Runner, List<string> Words, int At, string Named, string? Refusal);

    /// <summary>
    /// The script <paramref name="tool"/> runs by <paramref name="name"/>, read from the folder's own package.json: bound only where it
    /// is one command whose runner is Jest or Vitest, and neither it nor the gate names the owner's own bound.
    /// </summary>
    private static Scripted Declared(HeavyGate gate, List<string> words, string tool, string name, string folder)
    {
        var named = $"`{gate.Name}`";
        var scripted = $"`{gate.Name}`'s {tool} script `{name}`";
        Scripted No(string said) => new(null, [], 0, scripted, said);

        if (Script(folder, name, out var problem) is not { } declared) return No($"{named} runs {tool}'s `{name}` script, and its folder{problem}, so it runs as written.");
        if (Words(declared) is not { } scriptWords) return No($"{scripted} is more than one command, so it runs as written.");
        if (scriptWords.Count == 0) return No($"{scripted} names no command, so it runs as written.");

        var (runner, at, other) = Classify(scriptWords);
        if (runner is null) return No($"{scripted} {other}, so it runs as written.");
        if (Own(words) is { } own) return No($"{named} names its own worker bound (`{own}`), so it runs as written.");
        if (Own(scriptWords) is { } owned) return No($"{scripted} names its own worker bound (`{owned}`), so it runs as written.");
        return new Scripted(runner, scriptWords, at, scripted, null);
    }

    /// <summary>Said where the version of <paramref name="tool"/> the folder runs is not recorded, or the nearest record names <paramref name="other"/>.</summary>
    private static string Unrecorded(HeavyGate gate, string tool, string script, string? other) => other is not null
        ? $"`{gate.Name}` runs {tool}'s `{script}` script, and the package.json nearest it declares `{other}` as its package manager, so it runs as written."
        : $"`{gate.Name}` runs {tool}'s `{script}` script, and which {tool} version runs it is recorded neither by a `packageManager` field nor by its install, so it runs as written.";

    private static string Unforwarded(HeavyGate gate, string tool, string version, string script) =>
        $"`{gate.Name}` runs {tool} {version}'s `{script}` script, and how this {tool} version hands a script its words is not known here, so it runs as written.";

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
        foreach (var at in Up(folder, checkout))
        {
            var manifest = Path.Combine(at, "node_modules", package, "package.json");
            if (File.Exists(manifest))
            {
                version = Field(manifest, "version");
                return true;
            }
        }

        return false;
    }

    /// <summary><paramref name="folder"/>, then each folder above it up to <paramref name="checkout"/>, never past it.</summary>
    private static IEnumerable<string> Up(string folder, string checkout)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(checkout));
        var at = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        while (at is not null && (at.Equals(root, Paths) || at.StartsWith(root + Path.DirectorySeparatorChar, Paths)))
        {
            yield return at;
            if (at.Equals(root, Paths)) break;
            at = Path.GetDirectoryName(at);
        }
    }

    /// <summary>
    /// The version of <paramref name="tool"/> (yarn or pnpm) the gate's folder runs, read without starting a process, each from the
    /// folder up to the checkout, the nearest winning (HEAVYRUN1a3): the <c>packageManager</c> field corepack reads; then the install's
    /// own record, pnpm's <c>node_modules/.modules.yaml</c> (<c>packageManager: pnpm@x.y.z</c>), or yarn's <c>.yarnrc.yml</c>
    /// <c>yarnPath</c> release (<c>yarn-x.y.z.cjs</c>) and a <c>node_modules/.yarn-integrity</c>, which only yarn 1 writes (said
    /// <c>1.x</c>). Null where none records it, or where the field names another tool, given in <paramref name="other"/>.
    /// </summary>
    private static string? ManagerVersion(string tool, string folder, string checkout, out string? other)
    {
        other = null;
        var up = Up(folder, checkout).ToList();
        foreach (var at in up)
        {
            var manifest = Path.Combine(at, "package.json");
            if (!File.Exists(manifest) || Field(manifest, "packageManager") is not { } declared) continue;
            if (Named(declared, tool) is { } version) return version;
            other = declared;
            return null;
        }

        if (tool == "pnpm")
        {
            foreach (var at in up)
            {
                if (Line(Path.Combine(at, "node_modules", ".modules.yaml"), "packageManager") is { } recorded) return Named(recorded, "pnpm");
            }

            return null;
        }

        foreach (var at in up)
        {
            if (Line(Path.Combine(at, ".yarnrc.yml"), "yarnPath") is not { } release) continue;
            var match = Regex.Match(release.Replace('\\', '/').Split('/')[^1], @"^yarn-(\d.*?)\.c?js$");
            if (match.Success) return match.Groups[1].Value;
        }

        return up.Any(at => File.Exists(Path.Combine(at, "node_modules", ".yarn-integrity"))) ? "1.x" : null;
    }

    /// <summary>The version in <c>tool@version</c>, a corepack hash after <c>+</c> taken off; or null where it names another tool.</summary>
    private static string? Named(string declared, string tool)
    {
        var text = declared.Trim();
        if (!text.StartsWith(tool + "@", StringComparison.Ordinal)) return null;
        var version = text[(tool.Length + 1)..];
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return (plus < 0 ? version : version[..plus]) is { Length: > 0 } read ? read : null;
    }

    /// <summary>A top-level <c>key: value</c> line's value in a YAML file, its quotes taken off; or null.</summary>
    private static string? Line(string file, string key)
    {
        try
        {
            if (!File.Exists(file)) return null;
            foreach (var line in File.ReadLines(file))
            {
                if (!line.StartsWith(key + ":", StringComparison.Ordinal)) continue;
                var value = line[(key.Length + 1)..].Trim().Trim('"', '\'');
                return value.Length > 0 ? value : null;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }

    /// <summary>A string field of a package.json's top level, trimmed; or null where it gives none that reads.</summary>
    private static string? Field(string manifest, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String
                   && field.GetString()!.Trim() is { Length: > 0 } text
                ? text
                : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A release's major; null for a prerelease, which promises nothing, or a version that does not read.</summary>
    private static int? Major(string version)
    {
        if (version.Contains('-', StringComparison.Ordinal)) return null;
        var dot = version.IndexOf('.', StringComparison.Ordinal);
        return int.TryParse(dot < 0 ? version : version[..dot], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ? major : null;
    }

    /// <summary>A pnpm option, read as pnpm's own before its <c>--</c>, that runs other packages' scripts.</summary>
    private static bool PnpmElsewhere(string word) =>
        word is "-r" or "-F" or "-C" or "-w" || word.StartsWith("-F=", StringComparison.Ordinal) || Names(word, "--recursive")
        || Names(word, "--filter") || Names(word, "--filter-prod") || Names(word, "--dir") || Names(word, "--workspace-root");

    /// <summary>A test runner: its package, its name as said, and the majors whose command line was read declaring <c>maxWorkers</c>.</summary>
    private sealed record Runner(string Package, string Name, int[] Majors)
    {
        /// <summary>Whether <paramref name="version"/> is a release of a major read declaring the flag; a prerelease promises nothing.</summary>
        public bool Takes(string version) => Major(version) is { } major && Majors.Contains(major);
    }
}
