using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One point as the plugin kit tells an author about it (PLUG8): what kind of question it is, when it
/// is asked, the frame it is sent, what it must answer, how long the driver waits, and the handler a new
/// plugin starts from.
/// </summary>
/// <param name="Kind">`decision`, `observation` or `act` — what the answer does (D64 §4, D100).</param>
/// <param name="OnFailure">What the driver does with a late, wrong or missing answer, finishing "so …".</param>
/// <param name="Frame">
/// The frame the driver sends for the kit's sample input, built by <see cref="HookFrames"/> — the
/// function the driver itself sends it with. A shared instance: clone it before changing it.
/// </param>
/// <param name="Handler">The wire script's entry for this point, as the scaffold writes it.</param>
public sealed record KitPoint(
    string Name, string Kind, string When, string Answer, string OnFailure, TimeSpan Patience, JsonObject Frame, string Handler);

/// <summary>One file a new plugin's folder gets.</summary>
public sealed record ScaffoldFile(string Name, string Content);

/// <summary>What `plugins new` would write, and where — printable and assertable before anything is on disk.</summary>
public sealed record ScaffoldPlan(string Id, string Folder, IReadOnlyList<string> Points, IReadOnlyList<ScaffoldFile> Files);

/// <summary>
/// The kit a session makes a plugin with (PLUG8, D101): a new plugin's folder, and a trial that starts a
/// plugin as the driver would and checks every answer by the driver's own reader.
/// </summary>
/// <remarks>
/// <para><b>Making a plugin is work.</b> A plugin is code that runs on the person's machine as them, so
/// the kit writes a folder for a repository — a manifest, a wire script, a wire test that needs nothing
/// of Daoris's, and a README carrying the wire — and installs nothing. `daoris plugin add` does that, once
/// a person has read it.</para>
///
/// <para><b>The samples are the driver's frames.</b> Each point's sample is built by
/// <see cref="HookFrames"/> from the kit's sample input, so what the kit tells an author and what the
/// driver sends cannot drift; <c>PluginKitTests</c> also sends the samples through the loop and the
/// landing and compares.</para>
///
/// <para><b>One implementation behind both doors.</b> `daoris-driver plugins new|try` and Settings →
/// Plugins both call this class. The CLI names no scaffold of its own: it spawns nothing outside
/// <c>toolchain.ts</c>, and the samples come from this library.</para>
/// </remarks>
public static partial class PluginKit
{
    /// <summary>The wire script a new plugin speaks from.</summary>
    public const string Script = "plugin.mjs";

    /// <summary>Its wire test: `node --test` in the folder runs it.</summary>
    public const string Test = "plugin.test.mjs";

    public const string Readme = "README.md";

    /// <summary>
    /// A sample frame's `root`: the repository's checkout, which the trial and the wire test replace with
    /// an empty scratch folder — never a path on anybody's machine.
    /// </summary>
    public const string SampleRoot = "{root}";

    /// <summary>A planned start, as the kit's samples say it.</summary>
    public static readonly Consideration SampleConsideration = new(
        new QuestView("0fda18", "game", "engine", "Expose a streaming budget", "World streaming needs a per-frame cap.", "Open"),
        StartVerdict.Start, "start", Root: SampleRoot, Workspace: "default");

    /// <summary>A session that concluded, as the kit's samples say it.</summary>
    public static readonly SessionEnded SampleEnding = new(
        "s1a2b3c4", "engine", "completed", ByPerson: false, Note: null, Quest: "0fda18", Adapter: "claude-code", Account: "work");

    /// <summary>A landing's branch, as the kit's samples say it.</summary>
    public static readonly LandingFrame SampleLanding = new(
        "engine", "default", SampleRoot, "feature/0fda18-expose-a-streaming-budget", "main", "Expose a streaming budget",
        "0fda18", "s1a2b3c4", [new LandingCommit("3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a", "Expose a streaming budget")]);

    /// <summary>A landed branch asked about, as the kit's samples say it (PLUGHOOK1a): pushed, with the pull request its plugin opened.</summary>
    public static readonly StateFrame SampleState = new(
        "engine", "default", SampleRoot, "feature/0fda18-expose-a-streaming-budget", "main",
        "https://example.test/example-org/engine/pull/7", "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a");

    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Every point the driver has, in <see cref="HookPoints.All"/>'s order.</summary>
    public static IReadOnlyList<KitPoint> Points { get; } =
    [
        new(
            HookPoints.QuestConsider,
            "decision",
            "Before a planned start spends anything, every plugin that listens here is asked, in the order of "
            + "their ids. The first to hold ends it, and its reason becomes the quest's own reason for sitting, "
            + "so it names what a person would do about it.",
            """`{ "kind": "allow" }`, or `{ "kind": "hold", "reason": "…" }`.""",
            "the driver would hold the quest, naming this plugin",
            HookPeer.DefaultPatience,
            Sample(HookFrames.Consider(SampleConsideration)),
            """
              // A decision, before a planned start spends anything. The first plugin to hold ends it, and its
              // reason becomes the quest's own reason for sitting, so name what a person would do about it:
              //   return { kind: 'hold', reason: 'outside working hours; it starts at 09:00' };
              'quest/consider': (frame) => ({ kind: 'allow' }),
            """),
        new(
            HookPoints.SessionEnded,
            "observation",
            "After a session concludes, every plugin that listens here is told what the driver concluded. "
            + "Nothing said here changes anything: the record has already moved.",
            "`{}`. The answer is not read, but it is waited for.",
            "the driver would say so on its console and go on",
            HookPeer.DefaultPatience,
            Sample(HookFrames.Ended(SampleEnding)),
            """
              // An observation, after a session concludes. Nothing said here changes anything; answer {} promptly.
              'session/ended': (frame) => ({}),
            """),
        new(
            HookPoints.Land,
            "act",
            "Once a landing has made its branch (only then, and only where a workspace's branch landing rule "
            + "names this plugin), it is started for that one landing, told the branch, and stopped. It pushes "
            + "the branch and opens the pull request for its platform, in `root`, with whatever that "
            + "platform's tools are signed in as. Daoris itself never pushes. Where `pullRequest` is set, that pull "
            + "request is already open from the branch, which has moved on since: read its state, push only while "
            + "it is open, and open no second one. `acceptedBy` is `person` or `auto`.",
            """`{ "pushed": true, "pullRequest": "https://…", "message": "…" }`. `pushed` is required; the pull """
            + "request is an absolute web address, or null; the message is the plugin's own sentence, or null. "
            + "The branch stands whatever it answers.",
            "the landing would say the plugin's step failed, and the branch stands",
            LandingPlugins.DefaultPatience,
            Sample(HookFrames.Land(SampleLanding)),
            """
              // An act, once a landing has made its branch: push it and open the pull request for your
              // platform, in frame.root, and say what happened. The branch stands whatever this answers, and
              // until it pushes it says so, since `pushed: false` is an honest answer. A push is
              //   run('git', ['push', '--quiet', '-u', 'origin', frame.branch], frame.root)
              'work/land': (frame) => ({
                pushed: false,
                pullRequest: null,
                message: `${id} does not push yet; push \`${frame.branch}\` by hand.`,
              }),
            """),
        new(
            HookPoints.State,
            "query",
            "Only where git cannot tell (a pull request completed by squash leaves no ancestor), at a look that may remove "
            + "something, the plugin that pushed a landed branch is asked about its pull request, in `root`. Find it by "
            + "`pullRequest`, else by `branch` as its source, preferring one into `line`. Daoris acts on a completed answer only "
            + "where git confirms it: its merge commit on the line, and the branch at or under the commit it merged.",
            "`{ \"state\": \"open\" | \"completed\" | \"abandoned\" | \"unknown\", \"pullRequest\": \"https://…\", \"mergeCommit\": \"…\", "
            + "\"sourceCommit\": \"…\", \"target\": \"main\", \"how\": \"merge\" | \"squash\" | \"rebase\" | \"rebase-merge\", \"at\": \"…\", "
            + "\"message\": \"…\" }`. `state` is required; a completed one names its `mergeCommit` and `sourceCommit` in full; "
            + "the rest is null where the platform does not say. `unknown` is the platform answering without a state.",
            "nothing is removed on its word, and the row says why",
            LandingPlugins.StatePatience,
            Sample(HookFrames.State(SampleState)),
            """
              // A query, only where git cannot tell: the pull request from frame.branch (or at frame.pullRequest),
              // asked of your platform in frame.root. Nothing is removed until git confirms a completed answer, so
              // `unknown` is an honest answer until this asks. A read is
              //   run('az', ['repos', 'pr', 'show', '--id', id, '--output', 'json'], frame.root)
              'work/state': (frame) => ({
                state: 'unknown',
                pullRequest: null,
                message: `${id} does not read pull requests yet.`,
              }),
            """),
    ];

    /// <summary>The kit's word on a point, or null for a name this build does not have.</summary>
    public static KitPoint? Find(string name) => Points.FirstOrDefault(point => point.Name == name);

    private static string Names => string.Join(", ", Points.Select(point => point.Name));

    private static JsonObject Sample(object frame) => JsonSerializer.SerializeToNode(frame)!.AsObject();

    /// <summary>
    /// What `plugins new` would write: a folder named by the id inside <paramref name="parent"/>, holding
    /// the manifest, the wire script answering each named point, its wire test and its README. Refused, in
    /// a sentence, for a name that is not an id, a point this build lacks, a folder that is not there, and
    /// a folder that already holds anything — the kit never writes over somebody's work.
    /// </summary>
    public static ScaffoldPlan Plan(string id, IEnumerable<string> points, string parent)
    {
        id = (id ?? "").Trim();
        if (!PluginCatalog.IsId(id))
        {
            throw new DriverException(
                $"`{id}` is not a plugin id — one is lowercase letters, digits, dots and dashes, like "
                + "`acme.quiet-hours`, and it names the plugin's folder.");
        }

        var named = points.Select(point => point.Trim()).Where(point => point.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (named.Count == 0)
        {
            throw new DriverException(
                $"a plugin that speaks needs at least one point — one of: {Names}. A plugin that only declares a "
                + "agent or a server is a manifest written by hand (the plugin design's §3); there is no code to make.");
        }

        if (named.FirstOrDefault(point => Find(point) is null) is { } unknown)
        {
            throw new DriverException($"`{unknown}` is not a point — one of: {Names}.");
        }

        var chosen = Points.Where(point => named.Contains(point.Name, StringComparer.Ordinal)).ToList();
        var folder = Path.Combine(Path.GetFullPath(parent), id);
        Writable(folder);

        var name = NameOf(id);
        // LF whatever this source file's own line endings were when it was compiled: raw literals carry them.
        return new ScaffoldPlan(id, folder, chosen.Select(point => point.Name).ToList(),
        [
            new(PluginCatalog.ManifestName, Manifest(id, name, chosen).Replace("\r\n", "\n")),
            new(Script, WireScript(id, name, chosen).Replace("\r\n", "\n")),
            new(Test, WireTest()),
            new(Readme, ReadmeOf(id, name, chosen).Replace("\r\n", "\n")),
        ]);
    }

    /// <summary>Write a plan's files, each whole or not at all. The folder is judged again first: a plan is what the disk said when it was made.</summary>
    /// <returns>The names written, in the plan's order.</returns>
    public static IReadOnlyList<string> Write(ScaffoldPlan plan)
    {
        Writable(plan.Folder);
        Directory.CreateDirectory(plan.Folder);
        foreach (var file in plan.Files) AtomicFile.WriteText(Path.Combine(plan.Folder, file.Name), file.Content);
        return plan.Files.Select(file => file.Name).ToList();
    }

    /// <summary>A new plugin goes into a folder that is not there yet, or is empty, inside one that is.</summary>
    private static void Writable(string folder)
    {
        var parent = Path.GetDirectoryName(folder)!;
        if (!Directory.Exists(parent))
        {
            throw new DriverException(
                $"`{parent}` does not exist — name a folder that does; the plugin's own folder is made inside it, named by its id.");
        }

        if (File.Exists(folder))
        {
            throw new DriverException($"`{folder}` already holds a file of that name — choose another id, or another folder to make it in.");
        }

        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Count() is > 0 and var held)
        {
            throw new DriverException(
                $"`{folder}` already holds {held} item(s) — the kit writes a new plugin into a new or empty folder, "
                + "never over one. Choose another id, or another folder to make it in.");
        }
    }

    /// <summary>A readable name from an id: its last part, words for dashes, the first letter raised — `acme.quiet-hours` is "Quiet hours".</summary>
    private static string NameOf(string id)
    {
        var last = id.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? id;
        var words = last.Replace('-', ' ').Trim();
        return words.Length == 0 ? id : char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static string Json(string value) => JsonSerializer.Serialize(value, Pretty);

    private static string Manifest(string id, string name, IReadOnlyList<KitPoint> points) => $$"""
        {
          "id": {{Json(id)}},
          "apiVersion": {{PluginCatalog.ApiVersion}},
          "name": {{Json(name)}},
          "version": "0.1.0",
          "description": "Made with Daoris's plugin kit. Say here what it does, in the sentence a person reads before turning it on.",
          "hooks": {
            "command": ["node", "${plugin}/{{Script}}"],
            "points": [{{string.Join(", ", points.Select(point => Json(point.Name)))}}]
          },
          "tools": []
        }

        """;

    /// <summary>Whether a plugin of these points runs a platform's tool, so it is written <c>run()</c> (a landing, or a query, PLUGHOOK1a).</summary>
    private static bool RunsATool(IReadOnlyList<KitPoint> points) => points.Any(point => point.Name is HookPoints.Land or HookPoints.State);

    private static string WireScript(string id, string name, IReadOnlyList<KitPoint> points)
    {
        var lands = RunsATool(points);
        return Template("plugin.mjs")
            .Replace("{{name}}", name, StringComparison.Ordinal)
            .Replace("{{id}}", id, StringComparison.Ordinal)
            .Replace("{{imports}}", lands
                ? "import { spawnSync } from 'node:child_process';\nimport { existsSync } from 'node:fs';\nimport { delimiter, join } from 'node:path';\n"
                : "", StringComparison.Ordinal)
            .Replace("{{helpers}}", lands ? Template("run.mjs") : "", StringComparison.Ordinal)
            .Replace("{{handlers}}", string.Join("\n\n", points.Select(point => point.Handler.Replace("\r\n", "\n"))), StringComparison.Ordinal);
    }

    /// <summary>The wire test: the same for every plugin of this build, since it reads the manifest it sits beside.</summary>
    private static string WireTest()
    {
        var table = new JsonObject();
        foreach (var point in Points)
        {
            table[point.Name] = new JsonObject
            {
                ["kind"] = point.Kind,
                ["patience"] = (long)point.Patience.TotalMilliseconds,
                ["frame"] = point.Frame.DeepClone(),
            };
        }

        return Template("plugin.test.mjs")
            .Replace("{{protocol}}", HookPeer.ProtocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{{grace}}", ((long)HookProcess.Grace.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{{points}}", table.ToJsonString(Pretty), StringComparison.Ordinal);
    }

    private static string ReadmeOf(string id, string name, IReadOnlyList<KitPoint> points)
    {
        var sections = new StringBuilder();
        foreach (var point in points)
        {
            sections.Append($"### `{point.Name}`: {Article(point.Kind)} {point.Kind}\n\n");
            sections.Append($"{point.When}\n\n");
            sections.Append("It is sent:\n\n```json\n").Append(point.Frame.ToJsonString(Pretty)).Append("\n```\n\n");
            sections.Append($"It answers {point.Answer} Late, wrong or no answer: {point.OnFailure}. ");
            sections.Append($"The driver waits {Seconds(point.Patience)} for it.\n\n");
        }

        var lands = points.Any(point => point.Name == HookPoints.Land);
        return Template("README.md")
            .Replace("{{name}}", name, StringComparison.Ordinal)
            .Replace("{{id}}", id, StringComparison.Ordinal)
            .Replace("{{points}}", sections.ToString(), StringComparison.Ordinal)
            .Replace("{{run}}", RunsATool(points)
                ? "`run()` in `plugin.mjs` does exactly that, and `safe()` respells text so it can pass."
                : "`daoris-driver plugins new <id> --point work/land` writes a `run()` helper that does exactly that.",
                StringComparison.Ordinal)
            .Replace("{{install}}", lands
                ? $" A landing plugin also needs a rule to name it: `daoris driver landing --workspace <name> branch "
                  + $"\"feature/{{quest}}-{{slug}}\" --plugin {id}`, or Repositories → the workspace's page → Setup → Defaults → How work lands."
                : "", StringComparison.Ordinal);
    }

    private static string Article(string kind) => kind is "observation" or "act" ? "an" : "a";

    /// <summary>A span as a person says it: "10 seconds", "2 minutes".</summary>
    internal static string Seconds(TimeSpan span) => span.TotalSeconds >= 60 && span.TotalSeconds % 60 == 0
        ? $"{span.TotalMinutes:0} minute{(span.TotalMinutes == 1 ? "" : "s")}"
        : $"{span.TotalSeconds:0.#} second{(span.TotalSeconds == 1 ? "" : "s")}";

    /// <summary>A template the build carries, with its line endings made LF whatever the checkout did to it.</summary>
    private static string Template(string name)
    {
        using var stream = typeof(PluginKit).Assembly.GetManifestResourceStream($"Daoris.Driver.plugin-kit.{name}.template")
            ?? throw new InvalidOperationException($"the plugin kit's `{name}` template is missing from the build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
