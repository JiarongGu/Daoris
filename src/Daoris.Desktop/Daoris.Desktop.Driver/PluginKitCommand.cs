using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The plugin kit from a terminal (PLUG8, D50): `daoris-driver plugins new|try`. Settings → Plugins is
/// the other door, onto the same <see cref="PluginKit"/>.
/// </summary>
/// <remarks>
/// <para><b>Why here and not in the `daoris` CLI.</b> `try` starts a plugin's process and speaks its
/// wire, and the CLI's discipline is that only <c>toolchain.ts</c> spawns anything; the driver library
/// already starts plugins and speaks the wire, and its frames are the ones a sample must be. `new` stays
/// beside `try` so there is one scaffold, whose samples come from the same place. The CLI's
/// `daoris plugin new|try` say where the kit is.</para>
///
/// <para>The printing lives in the library, not the host, so a test can run the whole door in-process.
/// Exit codes keep the family contract: 0 clean · 1 the plugin failed a check · 2 the kit could not do
/// what was asked.</para>
/// </remarks>
public static class PluginKitCommand
{
    public const string Usage =
        "usage: daoris-driver plugins new <id> --point <point>… [--in <folder>]\n"
        + "       daoris-driver plugins try <folder|id> [--point <point>] [--frame <file.json>]";

    /// <param name="log">
    /// The machine log, where a trial of an installed plugin is one <c>plugin.tried</c> line from this door
    /// (PLUGUI1d). Null writes none.
    /// </param>
    public static async Task<int> RunAsync(string[] args, TextWriter output, string? home, CancellationToken ct = default, MachineLog? log = null)
    {
        try
        {
            switch (args)
            {
                case ["new", .. var rest]:
                    return New(rest, output);
                case ["try", .. var rest]:
                    return await TryAsync(rest, output, home, log, ct).ConfigureAwait(false);
                default:
                    output.WriteLine(Usage);
                    output.WriteLine("  new writes a plugin's folder — a manifest, a wire script answering each point, its wire");
                    output.WriteLine("  test and a README — into a new or empty folder, and installs nothing. try checks each tool");
                    output.WriteLine("  the manifest declares, starts the plugin as the driver would, speaks the handshake, a frame");
                    output.WriteLine("  at each point and the shutdown, and checks every answer.");
                    output.WriteLine($"  Points: {string.Join(", ", PluginKit.Points.Select(point => point.Name))}.");
                    return 2;
            }
        }
        catch (DriverException error)
        {
            output.WriteLine($"plugins: {error.Message}");
            return 2;
        }
    }

    private static int New(string[] args, TextWriter output)
    {
        var (positional, points, options) = Parse(args, "new", ["--in"]);
        if (positional is null)
        {
            throw new DriverException(
                "`plugins new` needs a plugin id — e.g. `daoris-driver plugins new acme.quiet-hours --point quest/consider`.");
        }

        var plan = PluginKit.Plan(positional, points, options.GetValueOrDefault("--in") ?? Directory.GetCurrentDirectory());
        var written = PluginKit.Write(plan);

        output.WriteLine($"plugins: made `{plan.Id}` in {plan.Folder}");
        var about = new Dictionary<string, string>
        {
            [PluginCatalog.ManifestName] = $"the manifest: it speaks on {string.Join(", ", plan.Points)}",
            [PluginKit.Script] = "the wire script: the handshake, a handler for each point, the shutdown",
            [PluginKit.Test] = "its wire test: `node --test` in the folder, nothing of Daoris's needed",
            [PluginKit.Readme] = "the wire, the rules, and each point's frame and answer",
        };
        foreach (var name in written) output.WriteLine($"  {name,-16} {about.GetValueOrDefault(name, "")}");
        output.WriteLine("  Next: write what each handler does in plugin.mjs, then `node --test` in the folder and");
        output.WriteLine($"  `daoris-driver plugins try {plan.Folder}`. Once a person has read it, `daoris plugin add <folder>`");
        output.WriteLine("  installs it; nothing runs before that.");
        return 0;
    }

    private static async Task<int> TryAsync(string[] args, TextWriter output, string? home, MachineLog? log, CancellationToken ct)
    {
        var (positional, points, options) = Parse(args, "try", ["--frame"]);
        if (positional is null)
        {
            throw new DriverException("`plugins try` needs a folder, or an installed plugin's id — e.g. `daoris-driver plugins try ./acme.quiet-hours`.");
        }

        if (points.Count > 1) throw new DriverException("`plugins try` sends one point's frame at a time: name one --point, or none for every point.");

        var trial = new TrialOptions(Point: points.FirstOrDefault(), Frame: options.TryGetValue("--frame", out var file) ? Frame(file) : null);

        // A folder first: after `plugins new acme.x`, `plugins try acme.x` means the folder just made.
        PluginTrial result;
        if (Directory.Exists(positional))
        {
            // A trial keeps its scratch under the home (D63): with none named, it refuses, as every writer does.
            result = await PluginKit.TryFolderAsync(
                home ?? throw new DriverException(
                    $"no Daoris home: a trial keeps what the plugin keeps under it. Set {DaorisHome.Variable}, or run "
                    + "`node --test` in the folder, which needs no Daoris."),
                positional, trial, ct).ConfigureAwait(false);
        }
        else if (PluginCatalog.IsId(positional) && home is not null)
        {
            var took = System.Diagnostics.Stopwatch.StartNew();
            result = await PluginKit.TryInstalledAsync(home, positional, trial, ct).ConfigureAwait(false);
            // An installed plugin's trial is part of its history (D119 §4.2). A folder's is printed and never kept: its id
            // may name an installed plugin it is not.
            new PluginLog(log).Tried(result, took.ElapsedMilliseconds, PluginEvents.Terminal);
        }
        else
        {
            throw new DriverException(home is null || !PluginCatalog.IsId(positional)
                ? $"no folder `{positional}` here, and no Daoris home to find an installed plugin by that name in."
                : $"no folder `{positional}` here.");
        }

        output.WriteLine($"plugins: trying `{result.Plugin}` in {result.Folder}");
        foreach (var step in result.Steps)
        {
            output.WriteLine($"  {(step.Ok ? "ok  " : "fail")}  {step.Name,-15} {step.Sentence}");
        }

        foreach (var line in result.Said) output.WriteLine($"  plugin:{result.Plugin}  {line}");
        output.WriteLine($"plugins: {result.Summary}");
        return result.ExitCode;
    }

    /// <summary>The frame a person wrote, read whole: an object, or a refusal naming the file.</summary>
    private static JsonObject Frame(string file)
    {
        if (!File.Exists(file)) throw new DriverException($"no file `{file}` — --frame names a JSON file holding the frame.");
        try
        {
            return JsonNode.Parse(File.ReadAllText(file)) as JsonObject
                ?? throw new DriverException($"`{file}` is not a JSON object — a frame is the object a point is sent.");
        }
        catch (JsonException)
        {
            throw new DriverException($"`{file}` is not a JSON object — a frame is the object a point is sent.");
        }
    }

    /// <summary>One positional argument, every `--point`, and the named options; anything else is refused by name.</summary>
    private static (string? Positional, List<string> Points, Dictionary<string, string> Options) Parse(
        string[] args, string verb, IReadOnlyList<string> named)
    {
        string? positional = null;
        var points = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--point" || named.Contains(arg))
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new DriverException($"`{arg}` needs a value.");
                }

                if (arg == "--point") points.Add(args[++i]);
                else options[arg] = args[++i];
            }
            else if (arg == "--harness" && verb == "new")
            {
                // D101: a harness is a declaration, not code — the kit makes plugins that speak.
                throw new DriverException(
                    "the kit makes plugins that speak; a harness is declared in plugin.json by hand, with the fields "
                    + "the plugin design's §3 lists, and `daoris plugin add` checks it.");
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal) || positional is not null)
            {
                throw new DriverException($"`{arg}` is not something `plugins {verb}` takes.\n{Usage}");
            }
            else
            {
                positional = arg;
            }
        }

        return (positional, points, options);
    }
}
