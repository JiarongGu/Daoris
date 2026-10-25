using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver workflow …</c> (PUSH1f2b, D194, the alone-path design §5; D157, the workflow design §4.7): the terminal's door
/// to workflows, the words <c>daoris driver workflow …</c> hands this program. <c>show --repository|--workspace [--kind]</c> draws what
/// work there follows: Current, read from the rules as they stand, or the named workflow a choice gives. The rest are named workflows,
/// one file each under the home's <c>workflows</c>: <c>list</c>, <c>show &lt;id&gt;[@&lt;version&gt;]</c>, <c>new</c>, <c>edit</c>,
/// <c>apply</c>, <c>export</c> and <c>import</c>; and the choice in <c>driver.json</c>, <c>use</c> and <c>kind</c>. A writer says its
/// change as Ask Daoris's card draws it and saves a new version, never editing one; <c>--plan</c> says it and saves nothing.
/// </summary>
/// <remarks>
/// <para>No route writes a named workflow or chooses one (D171's PUSH1f note, kept by D194 point 5), so a write acts on the terminal's
/// word, refused from a session's terminal, and holds the home's lease from the top (<see cref="VerbLeases"/>); a read holds nothing.
/// The landed route, <c>use … --landed</c>, accepts the gates at the line's tip as the window's <c>WORKFLOW_LANDED</c> does, and stays
/// the terminal's own act, as it was.</para>
/// <para>Today's text is kept (<c>fixtures/driver-workflows-text.json</c>). Exit codes are the CLI's: 1 where a version would not read
/// or a choice is refused, 2 where the words cannot be done.</para>
/// </remarks>
public static class DriverWorkflowsCommand
{
    /// <summary>The sub-verbs that write a workflow or the choice; with <c>--plan</c> each only says what it would write.</summary>
    public static IReadOnlySet<string> Writes { get; } = ChoiceGrammar.WorkflowWrites;

    /// <summary>The sub-verbs, each the CLI's.</summary>
    public static IReadOnlyList<string> Verbs { get; } = ["list", "show", "use", "kind", "new", "edit", "apply", "export", "import"];

    public const string Usage = "`driver workflow` takes list; show <id>[@<version>]; show --repository <name>|--workspace <name> [--kind <kind>]; "
        + "new <id> --from <preset>|current:<repository>|<id>[@<version>] [--with-opinion] [--with-look] [--name \"…\"]; "
        + "edit <id> [--base <version>] <change>…; apply <id> <file>|--as-graph [--base <version>]; export <id> [--to <file>]; import <file>; "
        + "use <id>|current|--clear --repository <name>|--workspace <name> [--kind <kind>|--landed]; "
        + "kind <workspace> <kind> --label \"…\" [--paths <path,…>]|--drop. "
        + "new, edit, apply, import, use and kind print their change, and with --plan save nothing — e.g. "
        + "`daoris driver workflow new docs --from pull-request-no-press --plan`.";

    /// <summary>Whether the words (after <c>workflow</c>) write: a writing sub-verb with no <c>--plan</c>.</summary>
    public static bool Changes(IReadOnlyList<string> args) => ChoiceGrammar.WorkflowChanges(args);

    /// <summary>
    /// The verb's run over <paramref name="choices"/>, the <c>driver.json</c> this terminal read: said, and written where it writes. A
    /// refusal of the words is <see cref="ChoiceWords"/>, said by the caller.
    /// </summary>
    /// <param name="argv">The words from <c>workflow</c> on.</param>
    public static Task<int> RunAsync(IReadOnlyList<string> argv, DriverConfig choices, DriverChoicesWorld world, CancellationToken ct) =>
        new Run(argv, choices, world, ct).Go();

    /// <summary>A workflow's file as found: the file as parsed (null where it is not JSON) and what it reads as.</summary>
    private sealed record Found(string Id, string Path, JsonNode? File, NamedWorkflowRead Read);

    private sealed class Run(IReadOnlyList<string> argv, DriverConfig choices, DriverChoicesWorld world, CancellationToken ct)
    {
        private readonly string _home = DriverConfig.HomeOf(world.ConfigPath);
        private string Folder => WorkflowStore.FolderOf(_home);
        private bool Plan => argv.Contains("--plan");

        private void Say(string line) => world.Instance.Out.WriteLine(line);

        public Task<int> Go()
        {
            var verb = argv.ElementAtOrDefault(1);
            switch (verb)
            {
                case "list":
                    Flags("list", 2, [], []);
                    return Task.FromResult(List());
                case "show":
                    return argv.Contains("--repository") || argv.Contains("--workspace") ? ShowChosenAsync() : Task.FromResult(Show());
                case "use":
                    return UseAsync();
                case "kind":
                    return Task.FromResult(Kind());
                case "new":
                    return CreateAsync();
                case "edit":
                    return Task.FromResult(Edit());
                case "apply":
                    return Task.FromResult(Apply());
                case "export":
                    return Task.FromResult(Export());
                case "import":
                    return Task.FromResult(Import());
                default:
                    throw new ChoiceWords(Usage);
            }
        }

        /// <summary>Refuses a flag the verb does not take, and an operand past the ones it names.</summary>
        private void Flags(string name, int operands, string[] valued, string[] switches)
        {
            var seen = 0;
            for (var at = 2; at < argv.Count; at++)
            {
                var token = argv[at];
                if (valued.Contains(token)) at++;
                else if (switches.Contains(token)) continue;
                else if (token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"`{token}` is not a flag `driver workflow {name}` takes — {Usage}");
                else if (++seen > operands - 2) throw new ChoiceWords(Usage);
            }

            if (seen < operands - 2) throw new ChoiceWords(Usage);
        }

        private Found? Load(string id)
        {
            // An id is the file's name, so one that is no id would name a path outside the folder.
            if (!WorkflowNamed.IsId(id)) throw new ChoiceWords(WorkflowNamed.IdProblem(id)!, 1);
            var found = WorkflowPorts.Store(_home).Load(id);
            return found is null ? null : new Found(id, WorkflowStore.PathOf(_home, id), found.File, found.Read);
        }

        private Found Required(string id) =>
            Load(id) ?? throw new ChoiceWords($"no workflow `{id}` here — `daoris driver workflow list` names them.");

        /// <summary><c>&lt;id&gt;</c> or <c>&lt;id&gt;@&lt;version&gt;</c>.</summary>
        private static (string Id, int? Version) Named(string? token)
        {
            if (token is null || token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords(Usage);
            var at = token.LastIndexOf('@');
            if (at == -1) return (token, null);
            var version = token[(at + 1)..];
            if (!(version.Length is >= 1 and <= 9 && version[0] is >= '1' and <= '9' && version.All(char.IsAsciiDigit)))
            {
                throw new ChoiceWords($"`{token}` names no version — `<id>@<version>`, such as `docs-to-pr@2`.");
            }

            return (token[..at], int.Parse(version, CultureInfo.InvariantCulture));
        }

        private int List()
        {
            var ids = WorkflowPorts.Store(_home).List();
            if (ids.Count == 0) Say($"daoris: no workflow is named in {Folder} yet.");
            else Say($"daoris: {(ids.Count == 1 ? "1 workflow" : $"{ids.Count} workflows")} named in {Folder}:");
            foreach (var found in ids)
            {
                var read = found.Read;
                if (read.Problem is not null)
                {
                    Say($"  `{found.Id}` — cannot be read: {read.Problem}");
                    continue;
                }

                var newest = read.Versions[^1];
                if (newest.Steps is not null)
                {
                    Say($"  `{found.Id}` — {read.Name}: v{newest.Version}, {newest.Steps.Count} steps. {WorkflowPresets.Line(newest.Steps)}");
                    continue;
                }

                // FABRIC-CUTOVER1d: a graph version reads and runs as a version of steps does.
                if (newest.Graph is not null)
                {
                    Say($"  `{found.Id}` — {read.Name}: v{newest.Version}, {WorkflowWords.GraphSaid(newest)}");
                    continue;
                }

                var reads = read.Versions.Reverse().FirstOrDefault(each => each.Steps is not null || each.Graph is not null);
                Say($"  `{found.Id}` — {read.Name}: v{newest.Version} cannot be read here ({newest.Unrun})"
                    + (reads is null ? "; no version of it reads here." : $"; v{reads.Version} is the newest that reads."));
            }

            Say("Presets built in, for `daoris driver workflow new <id> --from <preset>` (`--with-opinion`, `--with-look`):");
            foreach (var preset in WorkflowPresets.Table)
            {
                Say($"  `{preset.Id}` — {preset.Name}. {WorkflowPresets.Line(StepsOf(preset.Steps(), 1)!)}");
            }

            Say(WorkflowWords.WhatChooses);
            return 0;
        }

        private int Show()
        {
            Flags("show", 3, [], []);
            var (id, version) = Named(argv[2]);
            var read = Required(id).Read;
            if (read.Problem is not null) throw new ChoiceWords($"`{id}` cannot be read: {read.Problem}", 1);
            var shown = version is null ? read.Versions[^1] : read.Versions.FirstOrDefault(each => each.Version == version);
            if (shown is null) throw new ChoiceWords($"`{id}` keeps no v{version} — it keeps {Kept(read)}.");
            if (shown.Graph is not null) throw new ChoiceWords(WorkflowWords.NoSteps(id, shown), 1);
            if (shown.Steps is null) throw new ChoiceWords($"`{id}` v{shown.Version} cannot be read here: {shown.Unrun} The file keeps it as written.", 1);

            var newest = ReferenceEquals(shown, read.Versions[^1]);
            Say($"daoris: `{id}` — {read.Name}: v{shown.Version}{(newest ? ", its newest" : "")}, saved {shown.At} {WorkflowWords.DoorSaid(shown.Door!)} "
                + $"(digest {shown.Digest}).");
            Say($"  {WorkflowPresets.Line(shown.Steps)}");
            foreach (var step in shown.Steps)
            {
                foreach (var line in WorkflowWords.NamedStepSaid(step)) Say(line);
            }

            Say($"  Versions kept: {string.Join(", ", read.Versions.Select(each => $"v{each.Version}{(each.Steps is not null ? "" : each.Graph is not null ? " (a graph)" : " (cannot be read here)")}"))}.");
            Say(WorkflowWords.WhatChooses);
            return 0;
        }

        /// <summary>
        /// The workflow a repository's or a workspace's work follows (WORKFLOW1a, WORKFLOW1e; design §4.1, §4.7): resolved for the kind
        /// named, as a task of that kind would be. Current is drawn from the rules as they stand; a named workflow, its newest version,
        /// which new work binds. A workspace's is read from the file alone; a repository's workspace from the registry.
        /// </summary>
        private async Task<int> ShowChosenAsync()
        {
            const string usage = "`driver workflow show` takes --repository <name>|--workspace <name>, and --kind <kind> for a kind of task: "
                + "the workflow its work follows, Current or the one chosen — e.g. `daoris driver workflow show --repository web-app`; or a "
                + "named workflow's <id>[@<version>].";
            string[] valued = ["--repository", "--workspace", "--kind"];
            for (var at = 2; at < argv.Count; at++)
            {
                var token = argv[at];
                if (valued.Contains(token)) at++;
                else if (token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"`{token}` is not a flag `driver workflow show` takes — {usage}");
                else throw new ChoiceWords(usage);
            }

            var repository = FlagValue("--repository");
            var workspace = FlagValue("--workspace");
            var kind = FlagValue("--kind");
            if ((repository is null) == (workspace is null)) throw new ChoiceWords(usage);
            var task = kind is null ? null : new WorkflowTaskChoice(kind, null);
            if (workspace is not null)
            {
                var selected = WorkflowSelection.Resolve(choices, null, workspace, task);
                var whose = $"the workspace `{workspace}`{KindSaid(selected)}";
                if (selected.Workflow != WorkflowSelection.Current) return ShowNamed(selected, whose, workspace, null, [], workspace);
                var said = WorkflowWords.CurrentSaid(WorkflowCurrent.Derive(choices, null, workspace, Plugins()), whose,
                    "for each repository there that sets none of its own");
                foreach (var line in WithChoice(said, selected, workspace, null, [], workspace, kind)) Say(line);
                return 0;
            }

            var current = await CurrentOfAsync(repository!).ConfigureAwait(false);
            var chosen = WorkflowSelection.Resolve(choices, repository, current.InWorkspace, task);
            var named = $"`{repository}`{KindSaid(chosen)}";
            var circle = RemoteTarget.Workspace(current.InWorkspace);
            if (chosen.Workflow != WorkflowSelection.Current) return ShowNamed(chosen, named, circle, repository, current.Notes, current.InWorkspace);
            foreach (var line in WithChoice(WorkflowWords.CurrentSaid(current.Drawn, named, current.Where), chosen, circle, repository, current.Notes,
                current.InWorkspace, kind))
            {
                Say(line);
            }

            return 0;
        }

        /// <summary>Current's lines, with the registry's notes and, where a choice or a kind is read, what chose it under the header; then the routes.</summary>
        private List<string> WithChoice(
            IReadOnlyList<string> said, WorkflowSelected selected, string circle, string? owner, IReadOnlyList<string> notes, string? inWorkspace, string? kind)
        {
            var lines = new List<string> { said[0] };
            lines.AddRange(notes);
            if (!(selected.Level == WorkflowLevels.Current && kind is null)) lines.Add($"  Chosen: {WorkflowWords.WhyChosen(selected, owner, circle)}.");
            lines.AddRange(UndeclaredSaid(selected, circle));
            lines.AddRange(RoutesSaid(owner, circle, inWorkspace));
            lines.AddRange(said.Skip(1));
            return lines;
        }

        /// <summary>
        /// The start node's routes (FABRIC-PIPELINE1a, the pipeline design §1.3), said before the steps: the default, each kind the
        /// workspace declares by its label and paths, and the landed route, each with the workflow it names and what chose it.
        /// </summary>
        private List<string> RoutesSaid(string? owner, string circle, string? inWorkspace)
        {
            string Target(WorkflowSelected selected) =>
                $"{(selected.Workflow == WorkflowSelection.Current ? "Current" : $"`{selected.Workflow}`")}, {WorkflowWords.WhyChosen(selected, owner, circle)}";
            var lines = new List<string>
            {
                "  Its start node routes the work that comes here:",
                $"    the default → {Target(WorkflowSelection.Resolve(choices, owner, inWorkspace, null))}",
            };
            if (choices.WorkspaceWorkflows.TryGetValue(circle, out var shared))
            {
                foreach (var (id, declared) in shared.Kinds)
                {
                    var paths = string.Concat(declared.Paths.Select(path => $", `{path}`"));
                    lines.Add($"    kind `{id}` ({declared.Label}{paths}) → {Target(WorkflowSelection.Resolve(choices, owner, inWorkspace, new WorkflowTaskChoice(id, null)))}");
                }
            }

            lines.Add($"    landed work → {WorkflowWords.LandedSaid(WorkflowSelection.ResolveLanded(choices, owner, inWorkspace), owner, circle)}");
            return lines;
        }

        /// <summary>A named workflow chosen: its newest version, which new work binds, or why it cannot be read here.</summary>
        private int ShowNamed(WorkflowSelected selected, string whose, string circle, string? owner, IReadOnlyList<string> notes, string? inWorkspace)
        {
            var why = WorkflowWords.WhyChosen(selected, owner, circle);
            var found = Load(selected.Workflow);
            var newest = found?.Read.Problem is null && found is not null ? found.Read.Versions[^1] : null;
            if (newest is { Graph: not null })
            {
                // FABRIC-CUTOVER1d: a graph newest is followed as a version of steps is, its graph said whole.
                Say($"daoris: {whose} follows `{selected.Workflow}` — {found!.Read.Name}, v{newest.Version} (digest {newest.Digest}), {why}.");
                foreach (var line in notes.Concat(UndeclaredSaid(selected, circle)).Concat(RoutesSaid(owner, circle, inWorkspace))) Say(line);
                Say($"  {char.ToUpperInvariant(WorkflowWords.GraphSaid(newest)[0])}{WorkflowWords.GraphSaid(newest)[1..]}");
                Say(WorkflowWords.WhatChooses);
                return 0;
            }

            if (newest?.Steps is null)
            {
                var problem = found is null ? "no workflow by that id is saved here"
                    : found.Read.Problem ?? $"v{newest!.Version} cannot be read here: {newest.Unrun}";
                Say($"daoris: {whose} chooses `{selected.Workflow}`, {why}, which cannot be read here: {problem.TrimEnd('.')}.");
                foreach (var line in notes.Concat(UndeclaredSaid(selected, circle))) Say(line);
                Say(WorkflowWords.WhatChooses);
                return 1;
            }

            Say($"daoris: {whose} follows `{selected.Workflow}` — {found!.Read.Name}, v{newest.Version} (digest {newest.Digest}), {why}.");
            foreach (var line in notes.Concat(UndeclaredSaid(selected, circle)).Concat(RoutesSaid(owner, circle, inWorkspace))) Say(line);
            Say($"  {WorkflowPresets.Line(newest.Steps)}");
            foreach (var step in newest.Steps)
            {
                foreach (var line in WorkflowWords.NamedStepSaid(step)) Say(line);
            }

            Say(WorkflowWords.WhatChooses);
            return 0;
        }

        /// <summary><c> for kind `docs` (Documentation),</c> where a kind is read; nothing where none is.</summary>
        private static string KindSaid(WorkflowSelected selected) =>
            selected.Kind is null ? "" : $", for kind `{selected.Kind}`{(selected.Label is null ? "" : $" ({selected.Label})")},";

        /// <summary>A kind named that the workspace does not declare, read as none.</summary>
        private static IEnumerable<string> UndeclaredSaid(WorkflowSelected selected, string circle) =>
            selected.Undeclared is null ? [] : [$"  `{selected.Undeclared}` is not a kind the workspace `{circle}` declares, so it is read as none."];

        /// <summary>The workspace a repository is in, as the registry says, or why the registry was not read.</summary>
        private async Task<(bool Read, string? Workspace, string? Unread)> WorkspaceOfAsync(string name)
        {
            var read = await world.Registry(ct).ConfigureAwait(false);
            if (read.Checkouts is not { } checkouts) return (false, null, read.Unread);
            return (true, checkouts.FirstOrDefault(each => InScope(each.Repository, name)).Workspace, null);
        }

        /// <summary>
        /// <c>use &lt;id&gt;|current|--clear --repository &lt;name&gt;|--workspace &lt;name&gt; [--kind &lt;kind&gt;]</c> (design §4.7): which
        /// workflow a repository's or a workspace's work follows, or a kind's. A named workflow is one saved here whose newest version
        /// reads (§3.9); a repository's kind is one its workspace declares, where the registry says which that is.
        /// </summary>
        private async Task<int> UseAsync()
        {
            const string usage = "`driver workflow use` takes <id>|current|--clear, then --repository <name>|--workspace <name>, and --kind <kind> "
                + "for a kind of task or --landed for the work that lands — e.g. `daoris driver workflow use docs-to-pr --workspace work --kind docs`.";
            string[] valued = ["--repository", "--workspace", "--kind"];
            var operands = new List<string>();
            for (var at = 2; at < argv.Count; at++)
            {
                var token = argv[at];
                if (valued.Contains(token)) at++;
                else if (token is "--clear" or "--plan" or "--landed") continue;
                else if (token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"`{token}` is not a flag `driver workflow use` takes — {usage}");
                else operands.Add(token);
            }

            var repository = FlagValue("--repository");
            var workspace = FlagValue("--workspace");
            var kind = FlagValue("--kind");
            var clear = argv.Contains("--clear");
            if ((repository is null) == (workspace is null) || operands.Count != (clear ? 0 : 1)) throw new ChoiceWords(usage);
            var chosen = clear ? null : operands[0];

            // A named workflow is chosen only where it reads here (design §3.9): refused where it would be chosen, naming why. Its file's
            // mark (FABRIC-PIPELINE1a) goes with the edit, so a verification pipeline is named by the landed route alone.
            string? pipeline = null;
            if (chosen is not null && chosen != WorkflowSelection.Current && WorkflowNamed.IsId(chosen))
            {
                var found = Load(chosen) ?? throw new ChoiceWords(
                    $"no workflow `{chosen}` is saved here — `daoris driver workflow list` names them, and `new` names one.");
                pipeline = found.Read.Pipeline;
                if (found.Read.Problem is not null) throw new ChoiceWords($"`{chosen}` cannot be read: {found.Read.Problem}", 1);
                var newest = found.Read.Versions[^1];
                if (newest.Steps is null && newest.Graph is null)
                {
                    throw new ChoiceWords($"`{chosen}` v{newest.Version} cannot be read here: {newest.Problem} It can be chosen once a "
                        + "version that reads is saved after it.", 1);
                }
            }

            var given = new WorkflowUse(chosen, repository, workspace, kind) { Pipeline = pipeline };
            if (argv.Contains("--landed")) return await UseLandedAsync(given, repository, workspace, chosen).ConfigureAwait(false);
            if (workspace is not null) return UseApplied(given with { Repository = null }, null, repository, workspace, kind, chosen);
            if (kind is null || chosen is null) return UseApplied(given, null, repository, workspace, kind, chosen);
            var (read, inWorkspace, unread) = await WorkspaceOfAsync(repository!).ConfigureAwait(false);
            return read
                ? UseApplied(given with { InKnown = true, In = inWorkspace }, null, repository, workspace, kind, chosen)
                : UseApplied(given, unread, repository, workspace, kind, chosen);
        }

        private int UseApplied(WorkflowUse edit, string? unchecked_, string? repository, string? workspace, string? kind, string? chosen)
        {
            DriverConfig edited;
            try
            {
                edited = WorkflowSelection.Apply(choices, edit);
            }
            catch (DriverException refused)
            {
                var declare = kind is not null && refused.Message.Contains("declares no kind", StringComparison.Ordinal)
                    ? $" `daoris driver workflow kind <workspace> {kind} --label \"…\"` declares one." : "";
                throw new ChoiceWords($"{refused.Message}{declare} Nothing was written.", 1);
            }

            var owner = repository;
            var name = (repository ?? workspace)!.Trim();
            var whose = repository is not null ? $"`{repository}`" : $"the workspace `{workspace}`";
            var after = ChoiceAt(repository is not null ? edited.Workflows : edited.WorkspaceWorkflows, name);
            var was = ChoiceAt(repository is not null ? choices.Workflows : choices.WorkspaceWorkflows, name);
            var forWhat = kind is null ? " as its default" : $" for kind `{kind}`";
            if (Shape(after, repository is null) == Shape(was, repository is null))
            {
                Say($"daoris: {whose} {(chosen is null ? "chose no workflow" : $"chooses `{chosen}` already")}{forWhat}: nothing was written.");
                return 0;
            }

            if (chosen is null) Say($"daoris: {whose} no longer chooses a workflow{forWhat}.");
            else if (chosen == WorkflowSelection.Current) Say($"daoris: {whose} now chooses Current{forWhat}: its rules as they stand, read at each gate.");
            else
            {
                var found = Load(chosen)!;
                var newest = found.Read.Versions[^1];
                Say($"daoris: {whose} now chooses `{chosen}`{forWhat} — {found.Read.Name}, v{newest.Version} now: {(newest.Graph is { } graph
                    ? $"a graph (schema {WorkflowNamed.GraphSchema}) of {graph.Nodes.Count} nodes and {graph.Wires.Count} wires, digest {newest.Digest}."
                    : WorkflowPresets.Line(newest.Steps!))}");
            }

            if (owner is not null)
            {
                Say(after is null
                    ? $"  `{owner}` names no workflow now, so its workspace's choice reaches it again."
                    : !after.NamesDevelopment
                        // A landed route alone replaces nothing (FABRIC-PIPELINE1a).
                        ? $"  `{owner}` names no workflow for its work now, only its landed route, so its workspace's choice reaches it again."
                        : $"  This replaces the workspace's for every kind: `{owner}` follows its own kinds and its own default, and Current "
                            + "where it names none.");
            }

            if (unchecked_ is not null)
            {
                Say($"  The registry was not read ({unchecked_}), so kind `{kind}` was not checked against the kinds `{owner}`'s workspace declares.");
            }

            if (chosen is not null) Say("  Each new piece of work binds what is chosen at its first start; work already started keeps what it bound.");
            return SavedChoice(edit);
        }

        /// <summary>
        /// <c>use &lt;id&gt;|current|--clear … --landed</c> (FABRIC-PIPELINE1a, the pipeline design §1.3, §4.2): the landed route, a
        /// verification pipeline for the work that reaches the line. For a repository it shows the <c>daoris.gates.json</c> at its line's
        /// tip and accepts it by its blob, as the tab's <i>Accept the gates</i> does; run again, it accepts the tip's new blob. A
        /// workspace's route accepts its gates repository by repository. Judged before any git is read.
        /// </summary>
        private async Task<int> UseLandedAsync(WorkflowUse given, string? repository, string? workspace, string? chosen)
        {
            WorkflowUse Edit(string? gates) => given with { Repository = repository, Workspace = repository is null ? workspace : null, Landed = true, Gates = gates };
            Judge(Edit(null));
            if (repository is null)
            {
                IReadOnlyList<string> shared = chosen is not null && chosen != WorkflowSelection.Current
                    ? [$"  Its gates are accepted repository by repository, at each line's tip: `daoris driver workflow use {chosen} --repository <name> "
                        + "--landed` accepts one's."]
                    : [];
                return LandedDone(Edit(null), workspace, shared, repository, workspace, chosen);
            }

            var own = choices.Workflows.TryGetValue(repository.Trim(), out var held) ? held.Landed : null;
            // A tip that cannot be read keeps what was accepted for the same pipeline: an acceptance names a blob, never a moment.
            var kept = own is not null && own.Workflow == chosen ? own.Gates : null;
            var read = await world.Registry(ct).ConfigureAwait(false);
            var inWorkspace = read.Checkouts?.FirstOrDefault(each => InScope(each.Repository, repository)).Workspace;
            if (chosen is null || chosen == WorkflowSelection.Current) return LandedDone(Edit(null), inWorkspace, [], repository, workspace, chosen);
            if (read.Checkouts is not { } checkouts)
            {
                return LandedDone(Edit(kept), inWorkspace, [$"  No gates were accepted: the registry was not read ({read.Unread}). Run this again where it can be."],
                    repository, workspace, chosen);
            }

            var root = checkouts.FirstOrDefault(each => InScope(each.Repository, repository) && each.Root is { } at && Directory.Exists(at)).Root;
            if (root is null)
            {
                return LandedDone(Edit(kept), inWorkspace,
                    [$"  No gates were accepted: `{repository}` has no checkout here whose line could be read. Run this again where it has one."],
                    repository, workspace, chosen);
            }

            var line = (await CanonicalLine.ResolveAsync(root, repository, inWorkspace, choices, ct).ConfigureAwait(false)).Branch;
            var (tip, problem) = line is null ? (null, "its line is not known here") : await LandedGates.ReadAsync(root, line, ct).ConfigureAwait(false);
            if (tip is null)
            {
                return LandedDone(Edit(kept), inWorkspace,
                    [$"  No gates were accepted: for `{repository}`, {problem}. Run this again where its line can be read."], repository, workspace, chosen);
            }

            var at = $"`{line}`'s tip `{tip.Commit[..7]}`";
            if (tip.Gates == WorkflowSelection.NoGates)
            {
                return LandedDone(Edit(WorkflowSelection.NoGates), inWorkspace,
                    [$"  {at} holds no `daoris.gates.json`: accepted as none, so its checks find no declaration until one lands and you accept it."],
                    repository, workspace, chosen);
            }

            var text = ((await LandedGates.TextAsync(root, tip.Gates, ct).ConfigureAwait(false)) ?? "").TrimEnd();
            var lines = new List<string> { $"  The gates it runs unasked, as `daoris.gates.json` declares them at {at} (blob `{tip.Gates[..7]}`):" };
            lines.AddRange(text.Split('\n').Select(each => $"    {each.TrimEnd('\r')}"));
            lines.Add("  Accepted: these gates run unasked on the work that lands; an edit of `daoris.gates.json` asks you again, and nothing runs "
                + "until you accept it.");
            if (own?.Gates is { } before && before != WorkflowSelection.NoGates && before != tip.Gates)
            {
                lines.Add($"  It replaces the gates accepted before, at blob `{before[..7]}`: what changed is `git diff {before} {tip.Gates}`.");
            }

            return LandedDone(Edit(tip.Gates), inWorkspace, lines, repository, workspace, chosen);
        }

        /// <summary>The landed route edited, said and written, or not with <c>--plan</c>: its pipeline, what was accepted, and what it lowers.</summary>
        private int LandedDone(WorkflowUse edit, string? inWorkspace, IReadOnlyList<string> accepted, string? repository, string? workspace, string? chosen)
        {
            var edited = Judge(edit);
            var whose = repository is not null ? $"`{repository}`" : $"the workspace `{workspace}`";
            var name = (repository ?? workspace)!.Trim();
            WorkflowChoice? Pick(DriverConfig config) => ChoiceAt(repository is not null ? config.Workflows : config.WorkspaceWorkflows, name);
            if (Shape(Pick(edited), repository is null) == Shape(Pick(choices), repository is null))
            {
                Say($"daoris: {whose} {(chosen is null ? "names no landed route" : $"names `{chosen}` for the work that lands already, its gates as accepted")}: "
                    + "nothing was written.");
                return 0;
            }

            if (chosen is null)
            {
                Say($"daoris: {whose} no longer names a landed route: {(repository is not null
                    ? "its workspace's reaches it, where it names one." : "each repository there verifies only the work it names a route for.")}");
            }
            else if (chosen == WorkflowSelection.Current)
            {
                Say($"daoris: {whose} now names no verification for the work that lands: `current`, which keeps its workspace's away.");
            }
            else
            {
                var found = Load(chosen)!;
                var newest = found.Read.Versions[^1];
                var graph = newest.Graph!;
                Say($"daoris: {whose} now verifies the work that lands with `{chosen}` — {found.Read.Name}, v{newest.Version} now: a graph "
                    + $"(schema {WorkflowNamed.GraphSchema}) of {graph.Nodes.Count} node{(graph.Nodes.Count == 1 ? "" : "s")} and {graph.Wires.Count} "
                    + $"wire{(graph.Wires.Count == 1 ? "" : "s")}, digest {newest.Digest}.");
            }

            foreach (var line in accepted) Say(line);
            // D157 point 10 (the pipeline design §1.3): what the change asks less of the person, said where they make it.
            var circle = repository is not null ? inWorkspace : workspace;
            if (WorkflowSelection.ResolveLanded(choices, repository, circle).Workflow != WorkflowSelection.Current
                && WorkflowSelection.ResolveLanded(edited, repository, circle).Workflow == WorkflowSelection.Current)
            {
                Say("  This asks less of you: it leaves the work that lands unverified.");
            }

            if (chosen is not null)
            {
                Say(repository is not null
                    ? $"  The landed route resolves on its own: `{repository}`'s development routes stay as they are."
                    : "  The landed route resolves on its own: each repository there that names none of its own follows it, and every development "
                        + "route stays as it is.");
            }

            if (Plan)
            {
                Say("  --plan: nothing was written.");
            }
            else
            {
                StandingChoices.Write(world.ConfigPath, config => WorkflowSelection.Apply(config, edit));
                Say($"  Written to {world.ConfigPath}.");
            }

            Say(WorkflowWords.LandedReads);
            return 0;
        }

        /// <summary>The choices with <paramref name="edit"/> made, or its refusal in the twins' words: nothing was written.</summary>
        private DriverConfig Judge(WorkflowChoiceEdit edit)
        {
            try
            {
                return WorkflowSelection.Apply(choices, edit);
            }
            catch (DriverException refused)
            {
                throw new ChoiceWords($"{refused.Message} Nothing was written.", 1);
            }
        }

        /// <summary>
        /// <c>kind &lt;workspace&gt; &lt;kind&gt; --label "…" [--paths &lt;path,…&gt;]</c> and <c>kind &lt;workspace&gt; &lt;kind&gt; --drop</c>
        /// (design §4.2, §4.7): a kind of task a workspace declares, with its label and the paths work of that kind keeps to; declared
        /// again, both are replaced.
        /// </summary>
        private int Kind()
        {
            const string usage = "`driver workflow kind` takes <workspace> <kind> --label \"…\" [--paths <path,…>], or <workspace> <kind> --drop — "
                + "e.g. `daoris driver workflow kind work docs --label \"Documentation\" --paths \"docs/**,**/*.md\"`.";
            string[] valued = ["--label", "--paths"];
            var operands = new List<string>();
            for (var at = 2; at < argv.Count; at++)
            {
                var token = argv[at];
                if (valued.Contains(token)) at++;
                else if (token is "--drop" or "--plan") continue;
                else if (token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"`{token}` is not a flag `driver workflow kind` takes — {usage}");
                else operands.Add(token);
            }

            var label = FlagValue("--label");
            var drop = argv.Contains("--drop");
            if (operands.Count != 2 || drop == (label is not null) || (drop && argv.Contains("--paths"))) throw new ChoiceWords(usage);
            var (workspace, id) = (operands[0], operands[1]);
            var paths = (FlagValue("--paths") ?? "").Split(',').Select(each => each.Trim()).Where(each => each.Length > 0).ToList();

            WorkflowChoiceEdit edit = drop ? new WorkflowDrop(id, workspace) : new WorkflowDeclare(id, workspace, label!, paths);
            var edited = Judge(edit);
            if (drop)
            {
                Say($"daoris: the workspace `{workspace}` no longer declares kind `{id}`.");
                var mapping = choices.Workflows.Where(pair => pair.Value.Kinds.Any(each => each.Key == id)).Select(pair => pair.Key)
                    .Order(StringComparer.Ordinal).Select(name => $"`{name}`").ToList();
                if (mapping.Count > 0)
                {
                    Say($"  {string.Join(", ", mapping)} still map{(mapping.Count == 1 ? "s" : "")} it; no task of that kind reaches "
                        + $"{(mapping.Count == 1 ? "that mapping" : "those mappings")} until `{id}` is declared again.");
                }
            }
            else
            {
                var declared = ChoiceAt(edited.WorkspaceWorkflows, workspace.Trim())?.KindOf(id);
                Say($"daoris: the workspace `{workspace}` declares kind `{id}` — {label}, "
                    + $"{(paths.Count == 0 ? "with no paths" : $"its paths {string.Join(", ", paths.Select(path => $"`{path}`"))}")}.");
                Say(declared?.Workflow is { } workflow
                    ? $"  It maps to `{workflow}` there."
                    : $"  It maps to no workflow there: `daoris driver workflow use <id> --workspace {workspace} --kind {id}` maps one, and "
                        + "`--repository <name>` maps it for one repository.");
            }

            return SavedChoice(edit);
        }

        /// <summary>The choice written, or not with <c>--plan</c>, and said either way, with what reads it: every gate, from a run's first start.</summary>
        private int SavedChoice(WorkflowChoiceEdit edit)
        {
            if (Plan)
            {
                Say("  --plan: nothing was written.");
            }
            else
            {
                StandingChoices.Write(world.ConfigPath, config => WorkflowSelection.Apply(config, edit));
                Say($"  Written to {world.ConfigPath}.");
            }

            Say(WorkflowWords.GateReads);
            return 0;
        }

        /// <summary>The plugins of the home the choices are in, as the driver reads them: the landing's and the opinion's doors with it.</summary>
        private IReadOnlyList<WorkflowPlugin> Plugins() => WorkflowCurrent.PluginsOf(PluginCatalog.Load(_home, AdapterSet.Built().Names));

        /// <summary>
        /// A repository's Current: its workspace is the registry's, read as <c>review</c> reads it; unread, it is drawn only where no rule
        /// in the file is set for a workspace, since none could then reach it.
        /// </summary>
        private async Task<(CurrentWorkflow Drawn, string Where, List<string> Notes, string? InWorkspace)> CurrentOfAsync(string name)
        {
            var read = await world.Registry(ct).ConfigureAwait(false);
            var notes = new List<string>();
            string? inWorkspace = null;
            string where;
            if (read.Checkouts is not { } checkouts)
            {
                if (choices.WorkspaceLandings.Count > 0 || choices.WorkspaceReviews.Count > 0 || choices.WorkspaceOpinions.Count > 0
                    || choices.WorkspaceWorkflows.Count > 0)
                {
                    throw new ChoiceWords($"cannot say which workspace's rules reach `{name}`: the registry could not be read — "
                        + $"{read.Unread} — and this file sets rules for a workspace. Nothing was drawn. `--workspace <name>` draws a "
                        + "workspace's Current from this file alone.");
                }

                where = "its workspace not read";
                notes.Add($"  The registry was not read ({read.Unread}); no rule here is set for a workspace, so none reaches it.");
            }
            else
            {
                var checkout = checkouts.FirstOrDefault(each => InScope(each.Repository, name));
                inWorkspace = checkout.Workspace;
                where = $"in the workspace `{RemoteTarget.Workspace(inWorkspace)}`";
                if (checkout.Repository is null)
                {
                    notes.Add($"  `{name}` is not in the registry, so it is read as in no workspace: the `{RemoteTarget.Workspace(null)}` "
                        + "workspace's rules reach it.");
                }
            }

            return (WorkflowCurrent.Derive(choices, name, inWorkspace, Plugins()), where, notes, inWorkspace);
        }

        private async Task<int> CreateAsync()
        {
            Flags("new", 3, ["--from", "--name"], ["--with-opinion", "--with-look", "--plan"]);
            var id = argv[2];
            if (WorkflowNamed.IdProblem(id) is { } idProblem) throw new ChoiceWords(idProblem, 1);
            var from = FlagValue("--from")
                ?? throw new ChoiceWords($"`driver workflow new` needs --from: a preset, `current:<repository>` or a saved workflow — {Usage}");
            if (Load(id) is { } existing)
            {
                var at = existing.Read.Versions.Count > 0 ? $", at v{existing.Read.Versions[^1].Version}" : "";
                throw new ChoiceWords($"`{id}` is a workflow here already{at}: `daoris driver workflow edit {id} …` adds a version to it.", 1);
            }

            var opinion = argv.Contains("--with-opinion");
            var look = argv.Contains("--with-look");
            var gated = string.Join(" and ", new[] { opinion ? "with a second opinion" : null, look ? "with your look first" : null }.OfType<string>());

            int Save(JsonArray steps, string source, string name)
            {
                var add = new WorkflowVersionAdded(id, FlagValue("--name") ?? name, "terminal", Moment(), WorkflowPresets.WithGates(steps, opinion, look));
                var plan = WorkflowNamed.PlanAdd(null, add, []);
                if (plan.Problem is not null) throw new ChoiceWords(plan.Problem, 1);
                var version = StepsOf(plan.Result!["versions"]![0]!["steps"], 1)!;
                Say($"daoris: `{id}` v1, a new workflow — {(string?)plan.Result["name"]}, from {source}{(gated.Length > 0 ? $", {gated}" : "")}:");
                Said(version, WorkflowDiff.Of(null, version).Said);
                return Saved(id, plan.Result);
            }

            if (from.StartsWith("current:", StringComparison.Ordinal))
            {
                var repository = from["current:".Length..];
                if (repository.Length == 0) throw new ChoiceWords($"`current:` names a repository, such as `current:web-app` — {Usage}");
                var drawn = (await CurrentOfAsync(repository).ConfigureAwait(false)).Drawn;
                return Save(WorkflowWords.StepsOfCurrent(drawn), $"`{repository}`'s Current (version {drawn.Version})", $"{repository}'s Current");
            }

            if (!from.Contains('@', StringComparison.Ordinal) && WorkflowPresets.Of(from) is { } preset)
            {
                // Short enough that every preset's name with both stays within a name's 60 characters.
                var name = $"{preset.Name}{(opinion ? " + second opinion" : "")}{(look ? " + your look" : "")}";
                return Save(preset.Steps(), $"the preset `{preset.Id}`", name);
            }

            var (sourceId, wanted) = Named(from);
            var found = Load(sourceId) ?? throw new ChoiceWords($"`{from}` is no preset, no saved workflow and no `current:<repository>` — the presets are "
                + $"{string.Join(", ", WorkflowPresets.Table.Select(each => $"`{each.Id}`"))}.");
            var (shown, raw) = VersionOf(found, wanted);
            return Save(raw, $"`{sourceId}` v{shown.Version}", found.Read.Name!);
        }

        /// <summary>A saved workflow's version that reads, and its steps as the file holds them; the newest where none is named.</summary>
        private static (NamedVersion Version, JsonArray Raw) VersionOf(Found found, int? wanted)
        {
            if (found.Read.Problem is not null) throw new ChoiceWords($"`{found.Id}` cannot be read: {found.Read.Problem}", 1);
            var versions = found.Read.Versions;
            var version = wanted is null ? versions[^1] : versions.FirstOrDefault(each => each.Version == wanted);
            if (version is null) throw new ChoiceWords($"`{found.Id}` keeps no v{wanted} — it keeps {Kept(found.Read)}.");
            if (version.Graph is not null) throw new ChoiceWords(WorkflowWords.NoSteps(found.Id, version), 1);
            if (version.Steps is null) throw new ChoiceWords($"`{found.Id}` v{version.Version} cannot be read here: {version.Unrun}", 1);
            var index = versions.ToList().IndexOf(version);
            return (version, found.File!["versions"]![index]!["steps"]!.DeepClone().AsArray());
        }

        /// <summary>The base a change is made against: the newest version, which <c>--base</c> must name where it is given (design §8.1).</summary>
        private (NamedVersion Version, JsonArray? Raw) BaseOf(Found found)
        {
            if (found.Read.Problem is not null) throw new ChoiceWords($"`{found.Id}` cannot be read: {found.Read.Problem}", 1);
            var newest = found.Read.Versions[^1];
            if (FlagValue("--base") is { } @base && @base != newest.Version.ToString(CultureInfo.InvariantCulture))
            {
                throw new ChoiceWords($"`{found.Id}` is at v{newest.Version} now, not v{@base}: read it again (`daoris driver workflow "
                    + $"show {found.Id}`) and make the change against v{newest.Version}.", 1);
            }

            return (newest, newest.Steps is null ? null : VersionOf(found, newest.Version).Raw);
        }

        private int Edit()
        {
            var id = Named(argv.ElementAtOrDefault(2)).Id;
            var found = Required(id);
            var (version, raw) = BaseOf(found);
            if (raw is null && version.Graph is not null) throw new ChoiceWords(WorkflowWords.NoSteps(id, version), 1);
            if (raw is null)
            {
                throw new ChoiceWords($"`{id}` v{version.Version} cannot be read here: {version.Unrun} `daoris driver workflow apply "
                    + $"{id} <file>` saves a whole version after it.", 1);
            }

            var tokens = new List<string>();
            for (var at = 3; at < argv.Count; at++)
            {
                var token = argv[at];
                if (token == "--base") at++;
                else if (token != "--plan") tokens.Add(token);
            }

            if (tokens.Count == 0) throw new ChoiceWords($"`driver workflow edit` needs a change: {WorkflowChanges.Said}");
            return Change(found, version, WorkflowChanges.Apply(raw, tokens, version.Version, found.Read));
        }

        private int Apply()
        {
            var asGraph = argv.Contains("--as-graph");
            Flags("apply", asGraph ? 3 : 4, ["--base"], ["--plan", "--as-graph"]);
            var id = Named(argv[2]).Id;
            var found = Required(id);
            var (version, _) = BaseOf(found);
            if (asGraph) return AsGraph(found, version);
            var held = Parsed(FileOperand(argv[3]));
            // A graph (FABRIC-GRAPH1f): the file's own, a version's of schema 2, or a workflow's newest version's.
            var versions = held is JsonObject record && record["versions"] is JsonArray listed ? listed : null;
            if (GraphIn(versions is null ? held : versions.Count > 0 ? versions[^1] : null, out var graph)) return ChangeGraph(found, version, graph);
            JsonNode? steps = null;
            var has = true;
            if (held is JsonArray list) steps = list;
            else if (held is JsonObject holder && holder["steps"] is JsonArray own) steps = own;
            else if (versions is { Count: > 0 } && versions[^1] is JsonObject last && last.TryGetPropertyValue("steps", out var newest)) steps = newest;
            else has = false;
            if (!has)
            {
                throw new ChoiceWords($"`{argv[3]}` holds no steps and no graph: a list of steps, a version's `steps` or its `graph` (schema 2), a graph "
                    + "itself, or a workflow's `versions`, its newest taken.", 1);
            }

            return Change(found, version, steps?.DeepClone());
        }

        /// <summary>A new version made from steps, against the base: judged, compared, said, and saved unless <c>--plan</c>.</summary>
        private int Change(Found found, NamedVersion @base, JsonNode? steps)
        {
            // Every version a bound run names is kept beyond the newest (design §2.6): the driver binds them (WORKFLOW1e).
            var plan = WorkflowNamed.PlanAdd(found.File, new WorkflowVersionAdded(found.Id, null, "terminal", Moment(), steps),
                WorkflowRunBindings.KeptVersions(_home, found.Id).ToList());
            if (plan.Problem is not null) throw new ChoiceWords(plan.Problem, 1);
            var next = StepsOf(steps, plan.Version!.Value, [.. found.Read.Versions.Where(each => each.Steps is not null)])!;
            if (@base.Steps is not null && @base.Digest == WorkflowNamed.Digest(next))
            {
                Say($"daoris: `{found.Id}` v{@base.Version} already says this: nothing was written.");
                return 0;
            }

            Say($"daoris: `{found.Id}` v{@base.Version} → v{plan.Version} — {found.Read.Name}{(@base.Steps is not null ? ""
                : @base.Graph is not null ? $" (v{@base.Version} is a graph, so the change is drawn from nothing)"
                : $" (v{@base.Version} cannot be read here, so the change is drawn from nothing)")}:");
            Said(next, WorkflowDiff.Of(@base.Steps, next).Said);
            return Saved(found.Id, plan.Result!);
        }

        /// <summary>
        /// <c>apply &lt;id&gt; --as-graph</c> (FABRIC-GRAPH1j; D190 point 8, the cutover design §7.2): the base, a steps version, read by its
        /// shape, since one holding a kind schema 1 does not run reads as no steps; decoded and saved as the next version, its synthetic nodes
        /// authored (<see cref="WorkflowGraphs.Saved"/>) and judged as a saved graph is, so two observed nodes of one plugin step are refused
        /// naming both. The steps version stays as it was read. A go-ahead is said, since the graph holds what follows it where the steps went on.
        /// </summary>
        private int AsGraph(Found found, NamedVersion @base)
        {
            if (@base.Graph is not null) throw new ChoiceWords($"`{found.Id}` v{@base.Version} is a graph already: --as-graph saves a version of steps as one.", 1);
            var index = found.Read.Versions.ToList().IndexOf(@base);
            var raw = found.File!["versions"]![index]!["steps"];
            var prior = found.Read.Versions.Take(index).Where(each => each.Steps is not null).ToList();
            var (steps, problem) = WorkflowNamed.ReadShape(raw is null ? null : WorkflowNamed.ElementOf(raw), @base.Version, prior);
            if (steps is null) throw new ChoiceWords($"`{found.Id}` v{@base.Version} cannot be saved as a graph: {problem}", 1);
            var held = steps.Where(step => step.Kind == WorkflowKinds.GoAhead)
                .Select(step => $"  The go-ahead `{step.Id}` now holds what follows it until your yes, or your skip with your words: the steps went on past "
                    + "one that could not start.")
                .ToList();
            return ChangeGraph(found, @base, WorkflowGraphs.Saved(steps), $"v{@base.Version}'s steps as a graph", held);
        }

        /// <summary>
        /// A new version that is a graph (FABRIC-GRAPH1f), against the base: judged whole as the driver judges it, said with its digest,
        /// and saved unless <c>--plan</c>. The same graph as the base's is nothing to do.
        /// </summary>
        /// <param name="what">What the graph is, said after the workflow's name, where it is not a file's.</param>
        /// <param name="lines">Lines said after the header, before what is written.</param>
        private int ChangeGraph(Found found, NamedVersion @base, JsonNode? graph, string? what = null, IReadOnlyList<string>? lines = null)
        {
            var plan = WorkflowNamed.PlanAdd(found.File, new WorkflowVersionAdded(found.Id, null, "terminal", Moment(), null, graph?.DeepClone()),
                WorkflowRunBindings.KeptVersions(_home, found.Id).ToList());
            if (plan.Problem is not null) throw new ChoiceWords(plan.Problem, 1);
            var versions = plan.Result!["versions"]!.AsArray();
            var next = WorkflowNamed.ReadVersion(WorkflowNamed.ElementOf(versions[^1]!), plan.Version!.Value, [], found.Read.Pipeline ?? WorkflowPipelines.Development);
            if (@base.Graph is not null && @base.Digest == next.Digest)
            {
                Say($"daoris: `{found.Id}` v{@base.Version} already says this: nothing was written.");
                return 0;
            }

            Say($"daoris: `{found.Id}` v{@base.Version} → v{plan.Version} — {found.Read.Name}{(what is null ? "" : $", {what}")}: {WorkflowWords.GraphSaid(next)}");
            foreach (var line in lines ?? []) Say(line);
            return Saved(found.Id, plan.Result!);
        }

        private int Export()
        {
            Flags("export", 3, ["--to"], []);
            var id = Named(argv[2]).Id;
            var found = Required(id);
            if (found.File is null) throw new ChoiceWords($"`{id}` cannot be exported: {found.Read.Problem}", 1);
            var text = WorkflowNamed.Text(found.File);
            if (FlagValue("--to") is not { } to)
            {
                Say(text[..^1]);
                return 0;
            }

            var path = FileOperand(to);
            if (File.Exists(path) || Directory.Exists(path)) throw new ChoiceWords($"`{to}` exists already: export writes a new file, and never over one.");
            AtomicFile.WriteText(path, text);
            var count = found.Read.Versions.Count;
            Say($"daoris: `{id}` written to {path}, with {(count == 1 ? "its version" : $"its {count} versions")}: "
                + "`daoris driver workflow import <file>` reads it on another machine.");
            return 0;
        }

        private int Import()
        {
            Flags("import", 3, [], ["--plan"]);
            var from = argv[2];
            var file = Parsed(FileOperand(from));
            var read = file is null ? WorkflowNamed.Read(default) : WorkflowNamed.Read(WorkflowNamed.ElementOf(file));
            if (read.Problem is not null) throw new ChoiceWords($"`{from}` cannot be imported: {read.Problem}", 1);
            // A graph version that holds is a version like another here (FABRIC-GRAPH1f); one that does not read is refused.
            if (read.Versions.FirstOrDefault(each => each.Steps is null && each.Graph is null) is { } unread)
            {
                throw new ChoiceWords($"`{from}` cannot be imported: {unread.Unrun}", 1);
            }

            var id = read.Id!;
            var newest = read.Versions[^1];
            if (Load(id) is { } here)
            {
                var same = here.Read.Problem is null && here.Read.Versions.Count == read.Versions.Count
                    && here.Read.Versions.Select((each, index) => each.Version == read.Versions[index].Version && each.Digest == read.Versions[index].Digest).All(each => each);
                if (same)
                {
                    Say($"daoris: `{id}` is here already, with the same versions: nothing was written.");
                    return 0;
                }

                var at = here.Read.Versions.Count > 0 ? $", at v{here.Read.Versions[^1].Version}" : "";
                throw new ChoiceWords($"`{id}` is a workflow here already{at}, and the file's versions differ: `daoris driver workflow apply {id} {from}` "
                    + "adds the file's newest version to it.", 1);
            }

            // A verification pipeline is made so here, its mark in its file (FABRIC-PIPELINE1a), and said so.
            var marked = read.Pipeline == WorkflowPipelines.Verification ? ", a verification pipeline" : "";
            var header = $"daoris: `{id}` — {read.Name}{marked}, from {from}: {(read.Versions.Count == 1 ? "one version" : $"{read.Versions.Count} versions")}, "
                + $"its newest v{newest.Version}:";
            if (newest.Steps is null)
            {
                Say($"{header} {WorkflowWords.GraphSaid(newest)}");
            }
            else
            {
                Say(header);
                Said(newest.Steps, WorkflowDiff.Of(null, newest.Steps).Said);
            }

            return Saved(id, file!);
        }

        /// <summary>The new version's line, then its change as the card draws it, under the header.</summary>
        private void Said(IReadOnlyList<NamedStep> steps, IReadOnlyList<string> lines)
        {
            Say($"  {WorkflowPresets.Line(steps)}");
            foreach (var line in lines) Say($"  {line}");
        }

        /// <summary>Written, or not with <c>--plan</c>, and said either way, with what reads it: the version a run binds.</summary>
        private int Saved(string id, JsonNode file)
        {
            if (Plan)
            {
                Say("  --plan: nothing was written.");
            }
            else
            {
                // Every value as read (FABRIC-GRAPH1f): a graph version's numbers by their own text and its keys in their order.
                WorkflowPorts.Store(_home).Write(id, file);
                Say($"  Written to {WorkflowStore.PathOf(_home, id)}.");
            }

            Say(WorkflowWords.WhatChooses);
            return 0;
        }

        /// <summary>A file the terminal names: absolute as given, else from the folder it was run in.</summary>
        private string FileOperand(string? token)
        {
            if (token is null || token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords(Usage);
            return System.IO.Path.IsPathFullyQualified(token) ? token : System.IO.Path.GetFullPath(System.IO.Path.Combine(world.Folder, token));
        }

        /// <summary>A file's JSON, every value as written, or the refusal naming it.</summary>
        private static JsonNode? Parsed(string path)
        {
            if (!File.Exists(path)) throw new ChoiceWords($"there is no file `{path}`.");
            try
            {
                return JsonNode.Parse(File.ReadAllText(path)); // read-shares: the person's own file, named on the command line
            }
            catch (JsonException)
            {
                throw new ChoiceWords($"`{path}` is not readable JSON.", 1);
            }
        }

        private string? FlagValue(string flag)
        {
            var at = -1;
            for (var i = 0; i < argv.Count; i++)
            {
                if (argv[i] == flag)
                {
                    at = i;
                    break;
                }
            }

            if (at == -1) return null;
            var value = argv.ElementAtOrDefault(at + 1);
            if (string.IsNullOrEmpty(value) || value.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"{flag} needs a name — e.g. `{flag} aurora`");
            return value;
        }

        private string Moment() => world.Now().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The graph a file's version or a file holds (FABRIC-GRAPH1f): a version of schema 2's <c>graph</c>, or an object that is a graph
    /// itself, its <c>nodes</c> a list or its <c>schemaVersion</c> named, with no <c>steps</c>; else null, and the file is read for steps.
    /// </summary>
    private static bool GraphIn(JsonNode? value, out JsonNode? graph)
    {
        graph = null;
        if (value is not JsonObject record) return false;
        if (record["schema"] is JsonValue schema && schema.TryGetValue<double>(out var number) && number == WorkflowNamed.GraphSchema
            && record.TryGetPropertyValue("graph", out graph))
        {
            return true;
        }

        graph = record;
        return !record.ContainsKey("steps") && (record["nodes"] is JsonArray || record.ContainsKey("schemaVersion"));
    }

    /// <summary>Steps as a version reads them, or null where they do not read.</summary>
    private static IReadOnlyList<NamedStep>? StepsOf(JsonNode? steps, int number, IReadOnlyList<NamedVersion>? prior = null) =>
        WorkflowNamed.ReadSteps(steps is null ? null : WorkflowNamed.ElementOf(steps), number, prior).Steps;

    /// <summary>The versions a workflow keeps, as a refusal names them.</summary>
    private static string Kept(NamedWorkflowRead read) => string.Join(", ", read.Versions.Select(each => $"v{each.Version}"));

    /// <summary>Whether a checkout is the repository named, in any case.</summary>
    private static bool InScope(string? repository, string name) => string.Equals(repository, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>A level's choice by its name in any case, or null.</summary>
    private static WorkflowChoice? ChoiceAt(IReadOnlyDictionary<string, WorkflowChoice> map, string name) =>
        map.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>A choice as the file writes it, to tell an edit that changes nothing.</summary>
    private static string Shape(WorkflowChoice? choice, bool workspace)
    {
        if (choice is null) return "";
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WorkflowSelection.Write(writer, choice, workspace);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
