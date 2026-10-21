using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

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
///
/// <para><b>The use case</b> (STRUCT3a): what the kit says and plans is here; the folder it writes, the templates the build
/// carries, a trial's scratch and its process are <see cref="PluginKitFiles"/>' and
/// <see cref="PluginTrialProcess"/>'s.</para>
///
/// <para><b>A change to a working plugin's kit files</b> (<c>work.mjs</c>, the wire and contract tests, <c>cycle.json</c>) is written into
/// <c>examples/plugins/local-preview</c> in the same commit, by `plugins new` into a scratch folder and copied over: <c>PluginKitTests</c>
/// holds the example's copies to what the kit writes (FABRIC-PERFORM1e).</para>
/// </remarks>
public static class PluginKit
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
        "0fda18", "s1a2b3c4", [new LandingCommit("3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a", "Expose a streaming budget")],
        Tip: "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a");

    /// <summary>A landed branch asked about, as the kit's samples say it (PLUGHOOK1a): pushed, with the pull request its plugin opened.</summary>
    public static readonly StateFrame SampleState = new(
        "engine", "default", SampleRoot, "feature/0fda18-expose-a-streaming-budget", "main",
        "https://example.test/example-org/engine/pull/7", "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a");

    /// <summary>A change, as the kit's samples say it (STATEHUB1g): what a session's landing moved, as accepting it declares.</summary>
    public static readonly StateChangedFrame SampleChange = new(["sessions", "landing", "diff", "handOff", "sweep", "treesSync"], ["s1a2b3c4"]);

    /// <summary>
    /// A sample frame's step (FABRIC-PERFORM1e): the plugin's own, which the wire test and the trial replace with its first declared
    /// step, its kind <c>&lt;plugin&gt;/&lt;id&gt;</c> where Daoris names a kind and its id where it names an id. A README says the plugin's.
    /// </summary>
    public const string SampleStep = "{step}";

    /// <summary>A sample reference's owner (FABRIC-PERFORM1e): the plugin's id, written in a README as the plugin's own.</summary>
    public const string SamplePlugin = "{plugin-id}";

    /// <summary>The units awaiting a plugin's steps, as the kit's samples say it (FABRIC-PERFORM1e): one branch, its step awaited, a resync asked.</summary>
    public static readonly WorkWatchFrame SampleWatch = new(1, Resync: true,
    [
        new WatchedUnit("engine", "default", SampleRoot, "feature/0fda18-expose-a-streaming-budget", "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a",
            "main", null, [SampleStep]),
    ]);

    /// <summary>One attempt, as the kit's samples say it (FABRIC-PERFORM1e): what the performer sends at <see cref="HookPoints.Perform"/>.</summary>
    public static readonly WorkPerformCall SamplePerform = new(
        "u-0fda18a2", SampleStep, 1, "i-0fda18a2b3c4", "k-3f2a9c1e5b7d4a6f", "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a", [],
        new DateTimeOffset(2026, 10, 21, 9, 10, 0, TimeSpan.Zero));

    /// <summary>The attempt's outcome, as the kit's samples say it (FABRIC-PERFORM1e): the event a performer sends, bound to its invocation.</summary>
    public static readonly WorkStepFrame SampleOutcome = new(
        SampleStep,
        new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence($"{SamplePlugin}/receipt@1:i-0fda18a2b3c4", null, null, "deployed"))
        {
            Resources = [new WorkResource($"{SamplePlugin}/resource@1", "r-i-0fda18a2b3c4")],
        },
        new DateTimeOffset(2026, 10, 21, 9, 2, 0, TimeSpan.Zero), "i-0fda18a2b3c4", Invocation: "i-0fda18a2b3c4");

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
            + "platform's tools are signed in as. Daoris itself never pushes. Push `tip`, the commit the landing judged, "
            + "to the branch (`<tip>:refs/heads/<branch>`), not the branch as it stands then: a commit made after the "
            + "judgement must not ride the push. Where `pullRequest` is set, that pull "
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
              // until it pushes it says so, since `pushed: false` is an honest answer. A push is of the commit judged:
              //   run('git', ['push', '--quiet', 'origin', `${frame.tip}:refs/heads/${frame.branch}`], frame.root)
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
        new(
            HookPoints.StateChanged,
            "observation",
            "Once something the driver holds has changed (a press in a window, or a terminal's `daoris-driver trees` verb, has "
            + "answered), every plugin that listens here is told what moved: `scopes`, the families of state that changed, in the "
            + "screen's words, and `sessions`, the sessions it was about, or null where it was not one session's. Never what it is "
            + "now, and never the press's answer, which was given before any plugin heard. Changes come one at a time, in order, "
            + $"and no more than {HookSet.ChangesWaiting} wait while a plugin is slow, the oldest going first. Nothing said here "
            + "changes anything.",
            "`{}`. The answer is not read, but it is waited for.",
            "the driver would say so on its console and go on; the press it told of has already answered",
            HookPeer.DefaultPatience,
            Sample(HookFrames.Changed(SampleChange)),
            """
              // An observation, once something the driver holds has changed: frame.scopes names what moved, frame.sessions
              // which sessions it was about (null is any). Nothing said here changes anything; answer {} promptly.
              'state/changed': (frame) => ({}),
            """),
        new(
            HookPoints.Watch,
            "observation",
            "Only by the Daoris that holds the machine, and only while some unit awaits one of this plugin's steps: the whole set of "
            + "units awaiting them, each with its repository, branch, tip and the steps it awaits, every time the set changes. "
            + "`generation` counts this process's starts; where `resync` is true, the plugin sends again, at `work/step`, everything "
            + "it knows of each awaited step, each under the id it was first sent with. The kit's `work.mjs` does both.",
            "`{}`. The answer is not read, but it is waited for.",
            "the driver would say so in the plugin's health and tell it the set again with `resync`",
            HookPeer.DefaultPatience,
            WorkWire.Watch(SampleWatch),
            """
              // The awaited set: which units await this plugin's steps. The kit's work.mjs answers it, and sends again what it knows.
              'work/watch': kit.points['work/watch'],
            """),
        new(
            HookPoints.Step,
            "event",
            "Never asked: the plugin's own notification, the one frame it sends unasked, `{ \"jsonrpc\": \"2.0\", \"method\": "
            + "\"work/step\", \"params\": … }` with no id. A performed step's outcome names the `invocation` it was asked; an observed "
            + "step's names the `repository`, `branch` and full `commit` it is about. `id` names the event, and sending it again "
            + "under the same id is the same event. Only reached outcomes carry `outputs`; any outcome may name `resources` it made.",
            "nothing: it is never answered.",
            "the driver would refuse the frame, count it under the plugin's name, and ask it to send again what it knows",
            HookPeer.DefaultPatience,
            WorkWire.Step(SampleOutcome)["params"]!.DeepClone().AsObject(),
            """
              // Not asked: the plugin's own event, which the kit's work.mjs sends for it. Listed so the handshake says it reports there.
              'work/step': null,
            """),
        new(
            HookPoints.Perform,
            "act",
            "Once a performed step is due, its attempt: written on Daoris's record before it is sent, and sent again under the same "
            + "`idempotencyKey` after a restart or a lost answer, which must never do the work twice. It is answered on receipt, "
            + "before the work; the outcome comes later at `work/step`, naming the `invocation`. Asked beside it: `work/reconcile` "
            + "(what became of an attempt) and `work/cleanup` (one thing it made, removed), which the kit's `work.mjs` answers.",
            "`{ \"invocation\": \"…\" }`, the invocation it was sent, and nothing more.",
            "the attempt's outcome would be unknown, and the driver asks `work/reconcile` before any other attempt",
            HookPeer.DefaultPatience,
            WorkWire.Perform(SamplePerform),
            """
              // One attempt, written down and acknowledged by the kit's work.mjs, which then calls act() above once for its key.
              'work/perform': kit.points['work/perform'],
            """),
    ];

    /// <summary>The kit's word on a point, or null for a name this build does not have.</summary>
    public static KitPoint? Find(string name) => Points.FirstOrDefault(point => point.Name == name);

    internal static string Names => string.Join(", ", Points.Select(point => point.Name));

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

        // FABRIC-PERFORM1e: a plugin that works reports at `work/step`, a performer's outcome and an observer's alike, so either brings it;
        // and the hook set tells every plugin that works the awaited set at `work/watch`, a performer included, so performing brings that.
        if (named.Any(point => point is HookPoints.Watch or HookPoints.Perform) && !named.Contains(HookPoints.Step)) named.Add(HookPoints.Step);
        if (named.Contains(HookPoints.Perform) && !named.Contains(HookPoints.Watch)) named.Add(HookPoints.Watch);
        var chosen = Points.Where(point => named.Contains(point.Name, StringComparer.Ordinal)).ToList();
        var folder = PluginKitFiles.FolderIn(parent, id);
        PluginKitFiles.Writable(folder);

        var name = NameOf(id);
        var files = new List<ScaffoldFile>
        {
            // LF whatever this source file's own line endings were when it was compiled: raw literals carry them.
            new(PluginCatalog.ManifestName, Manifest(id, name, chosen).Replace("\r\n", "\n")),
            new(Script, WireScript(id, name, chosen).Replace("\r\n", "\n")),
            new(Test, WireTest()),
            new(Readme, ReadmeOf(id, name, chosen).Replace("\r\n", "\n")),
        };
        if (Works(chosen)) files.Add(new(WorkScript, WorkLibrary()));
        if (Performs(chosen))
        {
            files.Add(new(ContractTest, ContractTestText()));
            files.Add(new(CycleFixture, PluginCycles.Fixture(PerformedStep)));
        }

        return new ScaffoldPlan(id, folder, chosen.Select(point => point.Name).ToList(), files);
    }

    /// <summary>The kit's half of a plugin that works (FABRIC-PERFORM1e): the journal, the work points' handlers and the stand-in world.</summary>
    public const string WorkScript = "work.mjs";

    /// <summary>A performer's contract test (§D9): `node --test` runs it beside the wire test.</summary>
    public const string ContractTest = "plugin.contract.test.mjs";

    /// <summary>The cycle `plugins try --cycle` drives a performer through (§D9).</summary>
    public const string CycleFixture = "cycle.json";

    /// <summary>The performed step a new performer declares, and the observed step a new observer does.</summary>
    public const string PerformedStep = "perform";

    public const string ObservedStep = "observe";

    /// <summary>The failure codes a new performer's step retries: §D6's own example.</summary>
    public static readonly IReadOnlyList<string> Transient = ["unavailable", "rate-limited", "timeout"];

    /// <summary>Whether a plugin of these points works: it speaks the work wire, and the kit gives it <see cref="WorkScript"/>.</summary>
    private static bool Works(IReadOnlyList<KitPoint> points) => points.Any(point => HookPoints.Work.Contains(point.Name, StringComparer.Ordinal));

    private static bool Performs(IReadOnlyList<KitPoint> points) => points.Any(point => point.Name == HookPoints.Perform);

    /// <summary>The kit's <see cref="WorkScript"/>: the same for every plugin of this build, since it reads the manifest it sits beside.</summary>
    internal static string WorkLibrary() => PluginKitFiles.Template(WorkScript).Replace("{{capability}}", WorkWire.Capability, StringComparison.Ordinal);

    /// <summary>The kit's <see cref="ContractTest"/>: the same for every performer of this build, since it reads the manifest it sits beside.</summary>
    internal static string ContractTestText() => PluginKitFiles.Template(ContractTest)
        .Replace("{{protocol}}", HookPeer.ProtocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{{grace}}", ((long)HookProcess.Grace.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{{capability}}", WorkWire.Capability, StringComparison.Ordinal);

    /// <summary>Write a plan's files, each whole or not at all. The folder is judged again first: a plan is what the disk said when it was made.</summary>
    /// <returns>The names written, in the plan's order.</returns>
    public static IReadOnlyList<string> Write(ScaffoldPlan plan) => PluginKitFiles.Write(plan);

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
          },{{Declared(id, points)}}
          "tools": []
        }

        """;

    /// <summary>The receipt a new working plugin's step is reached on, and what a new performer makes (§D4, §D3): schemas of its own.</summary>
    internal static string ReceiptOf(string id) => $"{id}/receipt@1";

    internal static string ResourceOf(string id) => $"{id}/resource@1";

    /// <summary>
    /// A working plugin's steps and resources (FABRIC-PERFORM1e, §D9): a performer's one performed step, declaring §D6's contract as the
    /// kit keeps it (idempotent, reconciled, §D6's transient codes under a retry that can take them) and the one temporary thing it makes;
    /// an observer's one observed step. Each reached on a receipt of the plugin's own. Nothing for a plugin that does not work.
    /// </summary>
    private static string Declared(string id, IReadOnlyList<KitPoint> points)
    {
        if (!Works(points)) return "";
        if (!Performs(points))
        {
            return $$"""

                  "steps": [
                    { "id": {{Json(ObservedStep)}}, "reach": "observed", "evidence": { "schema": {{Json(ReceiptOf(id))}} } }
                  ],
                """.TrimEnd();
        }

        return $$"""

              "steps": [
                {
                  "id": {{Json(PerformedStep)}},
                  "reach": "performed",
                  "evidence": { "schema": {{Json(ReceiptOf(id))}} },
                  "perform": { "point": "{{HookPoints.Perform}}", "idempotent": true, "reconcile": "{{PluginSteps.ReconcilePoint}}", "transient": [{{string.Join(", ", Transient.Select(Json))}}] },
                  "retry": { "maxAttempts": 3, "delaysSeconds": [5, 30] },
                  "timeoutSeconds": 600
                }
              ],
              "resources": [
                { "schema": {{Json(ResourceOf(id))}}, "role": "temporary", "cleanup": "{{PluginSteps.CleanupPoint}}" }
              ],
            """.TrimEnd();
    }

    /// <summary>Whether a plugin of these points runs a platform's tool, so it is written <c>run()</c> (a landing, or a query, PLUGHOOK1a).</summary>
    private static bool RunsATool(IReadOnlyList<KitPoint> points) => points.Any(point => point.Name is HookPoints.Land or HookPoints.State);

    private static string WireScript(string id, string name, IReadOnlyList<KitPoint> points)
    {
        var lands = RunsATool(points);
        var works = Works(points);
        var performs = Performs(points);
        return PluginKitFiles.Template("plugin.mjs")
            .Replace("{{name}}", name, StringComparison.Ordinal)
            .Replace("{{id}}", id, StringComparison.Ordinal)
            .Replace("{{imports}}", (lands
                ? "import { spawnSync } from 'node:child_process';\nimport { existsSync } from 'node:fs';\nimport { delimiter, join } from 'node:path';\n"
                : "") + (works ? "import { work } from './work.mjs';\n" : ""), StringComparison.Ordinal)
            .Replace("{{helpers}}", lands ? PluginKitFiles.Template("run.mjs") : "", StringComparison.Ordinal)
            .Replace("{{work}}", works ? WorkCalls(id, performs) : "", StringComparison.Ordinal)
            .Replace("{{handlers}}", string.Join("\n\n", points.Select(point => point.Handler.Replace("\r\n", "\n"))), StringComparison.Ordinal)
            .Replace("{{calls}}", performs ? ", ...kit.calls" : "", StringComparison.Ordinal)
            .Replace("{{capabilities}}", performs ? ", capabilities: kit.capabilities" : "", StringComparison.Ordinal);
    }

    /// <summary>
    /// The external calls a working plugin's author writes (FABRIC-PERFORM1e, §D9), handed to the kit's <c>work()</c>: a performer's
    /// <c>act</c>, <c>check</c> and <c>remove</c>, an observer's <c>observe</c>. Each starts by saying it is not written yet.
    /// </summary>
    private static string WorkCalls(string id, bool performs) => (performs
        ? $$"""

            // The work points (README.md, "Working"): the kit's work.mjs keeps the journal, answers each point, and sends each outcome at
            // `work/step`. These are the external calls it makes, the only part to write. Under the kit's stand-in world, which its tests
            // and `daoris-driver plugins try` set, none of them is called.
            const kit = work({
              data,
              send,
              say,
              // The work, done once for an attempt: call.unit, call.step, call.invocation, call.idempotencyKey, call.subjectRevision,
              // call.inputs, call.deadlineAt. Say the receipt it reached on and what it made, which Daoris has removed at the end:
              //   return { state: 'reached', evidence: { ref: `{{ReceiptOf(id)}}:${call.invocation}`, url, message },
              //            resources: [{ schema: '{{ResourceOf(id)}}', id: '<the platform's id for it>' }] };
              // A failure names its code; only the codes plugin.json lists as `transient` are tried again:
              //   return { state: 'failed', code: 'unavailable', evidence: { message: '…' } };
              act: async (call) => ({ state: 'failed', code: 'not-written', evidence: { message: `${id} does not perform yet.` } }),
              // What became of an attempt a process before this one began, asked of the platform: { state: 'done', outcome },
              // { state: 'not-started' }, or null where it cannot say, which stops the step for the person.
              check: async (attempt) => null,
              // One thing act made, removed: { disposition: 'removed' | 'already-gone' | 'retained' | 'failed', proof, message }.
              remove: async (resource) => ({ disposition: 'failed', message: `${id} does not remove ${resource.resource} yet.` }),
            });
            """
        : """

            // The work points (README.md, "Working"): the kit's work.mjs keeps the journal, answers each point, and sends each
            // observation at `work/step`. This is the external call it makes, the only part to write. Under the kit's stand-in world,
            // which its tests and `daoris-driver plugins try` set, it is not called.
            const kit = work({
              data,
              send,
              say,
              // What the platform says of an awaited step at a unit's tip (unit.repository, unit.branch, unit.tip): an outcome, like
              //   return { state: 'reached', evidence: { ref: '<receipt schema>:<id>', url, message } };
              // or null while it says nothing yet. A changed answer for the same tip is sent once, under an id of its own.
              observe: async (unit, step) => null,
            });
            """).Replace("\r\n", "\n") + "\n";

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

        return PluginKitFiles.Template("plugin.test.mjs")
            .Replace("{{protocol}}", HookPeer.ProtocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{{grace}}", ((long)HookProcess.Grace.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{{capability}}", WorkWire.Capability, StringComparison.Ordinal)
            .Replace("{{step}}", SampleStep, StringComparison.Ordinal)
            .Replace("{{points}}", table.ToJsonString(Pretty), StringComparison.Ordinal);
    }

    private static string ReadmeOf(string id, string name, IReadOnlyList<KitPoint> points)
    {
        // A sample's step and its references' owner are this plugin's own in its README (FABRIC-PERFORM1e): the awaited set names a step
        // by its id, an attempt and its outcome by its kind.
        var step = Performs(points) ? PerformedStep : ObservedStep;
        string Own(KitPoint point) => point.Frame.ToJsonString(Pretty)
            .Replace(SampleStep, point.Name == HookPoints.Watch ? step : PluginSteps.KindOf(id, step), StringComparison.Ordinal)
            .Replace(SamplePlugin, id, StringComparison.Ordinal);

        var sections = new StringBuilder();
        foreach (var point in points)
        {
            sections.Append($"### `{point.Name}`: {Article(point.Kind)} {point.Kind}\n\n");
            sections.Append($"{point.When}\n\n");
            if (point.Kind == "event")
            {
                // The plugin's own notification: what it sends, never what it is sent; and nothing waits for it.
                sections.Append("It sends, as `params`:\n\n```json\n").Append(Own(point)).Append("\n```\n\n");
                sections.Append($"It is answered {point.Answer} A frame the driver cannot read: {point.OnFailure}.\n\n");
                continue;
            }

            sections.Append("It is sent:\n\n```json\n").Append(Own(point)).Append("\n```\n\n");
            sections.Append($"It answers {point.Answer} Late, wrong or no answer: {point.OnFailure}. ");
            sections.Append($"The driver waits {Seconds(point.Patience)} for it.\n\n");
        }

        var files = new StringBuilder();
        if (Works(points)) files.Append($"| `{WorkScript}` | the kit's half of working: the journal, the work points' answers, the stand-in world; written by the kit, not by hand |\n");
        if (Performs(points))
        {
            files.Append($"| `{ContractTest}` | its contract test: what the kit promises of a performer, checked over the wire |\n");
            files.Append($"| `{CycleFixture}` | the cycle `daoris-driver plugins try --cycle` drives it through |\n");
        }

        var work = !Works(points) ? ""
            : PluginKitFiles.Template(Performs(points) ? "work-perform.md" : "work-observe.md")
                .Replace("{{step}}", step, StringComparison.Ordinal)
                .Replace("{{resource}}", ResourceOf(id), StringComparison.Ordinal);

        var lands = points.Any(point => point.Name == HookPoints.Land);
        return PluginKitFiles.Template("README.md")
            .Replace("{{name}}", name, StringComparison.Ordinal)
            .Replace("{{id}}", id, StringComparison.Ordinal)
            .Replace("{{files}}", files.ToString(), StringComparison.Ordinal)
            .Replace("{{points}}", sections.ToString(), StringComparison.Ordinal)
            .Replace("{{work}}", work, StringComparison.Ordinal)
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

}
