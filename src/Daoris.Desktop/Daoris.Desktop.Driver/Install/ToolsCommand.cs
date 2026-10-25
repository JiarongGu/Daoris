using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Where <c>daoris-driver tool</c> acts (PUSH1f2c): the terminal it says its words on and finds the running Daoris from, and what a
/// verb reads the machine by: the folder a relative file is named from, the <c>PATH</c> a system tool is found on, this machine's
/// platform, the network, a named file's answer to its version question, and the process a download stages under. A test hands its
/// own of each.
/// </summary>
public sealed record ToolsWorld
{
    /// <summary>The terminal: its home (null where none is named), its variables, its streams, and where it finds the instance.</summary>
    public required IToolTerminal Instance { get; init; }

    /// <summary>Where the command runs from: a relative file is named from here.</summary>
    public string WorkingDirectory { get; init; } = Environment.CurrentDirectory;

    /// <summary>The <c>PATH</c> a system tool is found on; null for this process's own.</summary>
    public string? SearchPath { get; init; }

    /// <summary>This machine's platform, as a list names one; null where none is.</summary>
    public string? Platform { get; init; } = ToolResources.Current;

    /// <summary>How a host is reached: the network, unless a test holds its own.</summary>
    public HttpMessageHandler? Transport { get; init; }

    /// <summary>How long one location may take to answer a look.</summary>
    public TimeSpan LookBound { get; init; } = ToolInstall.LookBound;

    /// <summary>A named file's answer to its tool's version question.</summary>
    public Func<string, ToolDeclaration, CancellationToken, Task<(string? Version, string? Problem)>> AskVersion { get; init; } =
        ToolQuestions.VersionOfAsync;

    /// <summary>The process a download stages under.</summary>
    public int Process { get; init; } = Environment.ProcessId;

    /// <summary>Whether a process of an id runs here, for the sweep of a dead run's staging; null for the machine's own look.</summary>
    public Func<int, bool>? Running { get; init; }

    /// <summary>The moment: a copy's age, and how long the publication has waited for the lease.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>Called once the publication finds another act holding the home: a test's sign that it waits.</summary>
    public Action? Waiting { get; init; }

    /// <summary>The machine log a download and a look write their events to; none where null.</summary>
    public MachineLog? Log { get; init; }
}

/// <summary>
/// One tool's press (PUSH1f2c, D194 points 4 and 6): the route the window's press sends and the payload the verb writes from its words,
/// the operation it calls alone, the press's own fields read from the route's answer, and the one function that says it from those
/// fields on both paths. <see cref="ToolsCommand.Plan"/> makes it; a test sends its payload through the route.
/// </summary>
/// <param name="Act">The operation, alone: it writes, and answers the press's own fields.</param>
/// <param name="Fields">The press's own fields, read from the route's answer.</param>
/// <param name="Words">The verb's words from those fields, both paths', returning its exit code.</param>
public sealed record ToolPress(
    string Verb, InstanceRoute Route, JsonObject Payload, Func<CancellationToken, Task<object?>> Act, Func<JsonElement, object?> Fields,
    Func<object?, int> Words);

/// <summary>A refusal the command makes before it acts, in its words, with today's exit: its grammar and its own checks.</summary>
public sealed class ToolWords(string message, int exit = 2) : Exception(message)
{
    public int Exit { get; } = exit;
}

/// <summary>
/// <c>daoris-driver tool</c> (PUSH1f2c, D194, the alone-path design §1.2): the programs Daoris runs beside its agents from a terminal,
/// the words <c>daoris tool …</c> hands this program. Each short change is its route through <see cref="InstanceDoor.PressAsync"/>
/// where the running Daoris answers, confirmed in its window; else its operation, on the terminal's word (D50), refused from a
/// session's terminal (D194 point 5). A download, an update and a managed use are long: they hold nothing while they fetch, stage
/// under this process's name, and publish under the home's lease, waited for and never refused (§1.4). Its words are one function per
/// verb on both paths, today's text kept (<c>fixtures/tools-text.json</c>).
/// </summary>
/// <remarks>
/// Exit codes are the CLI's, case by case: 0 said or set · 1 a tool refused in the list, a path that resolves to no file, a look a
/// location did not answer, a plan or a download's check, the person's refusal in the window, or a session's terminal · 2 the usage,
/// a refusal of the words, the operation or the route, and the person's Ctrl+C.
/// </remarks>
public static class ToolsCommand
{
    /// <summary>The verbs this program has: every one the CLI's <c>daoris tool</c> had, which it hands on whole.</summary>
    public static IReadOnlyList<string> Verbs { get; } = ["list", "path", "use", "download", "update", "delete", "locations", "look"];

    public const string Usage =
        """
        usage: daoris-driver tool [list]
               daoris-driver tool path <tool>
               daoris-driver tool use <tool> system | managed [<version>] | file <path>
               daoris-driver tool download <tool> [<version>] · update <tool> · delete <tool> <version>
               daoris-driver tool locations [add|remove <address>] · look
        `daoris tool …` runs these; the tools are git, node, pwsh, gh and az.
        Each change takes --wait <minutes> (0 to 30, the default 30) or --no-wait: how long to wait on its Confirm in Daoris's window.
        """;

    private const string Prefix = "daoris";

    /// <summary>
    /// Whether the words are a short change, which holds the home's lease from the top: a way set to the system's or a file, a delete,
    /// a location added or removed, and a look, whose copies are the home's. A read, the usage and the long verbs hold none: a download
    /// takes the lease itself to publish (the alone-path design §1.2, §1.4).
    /// </summary>
    public static bool Changes(IReadOnlyList<string> args)
    {
        if (args.Any(arg => arg is "--help" or "-h")) return false;
        var words = Operands(args);
        var verb = words.Count > 0 ? words[0] : "list";
        return verb switch
        {
            "use" => words.Count > 2 && words[2] is "system" or "file",
            "delete" or "look" => true,
            "locations" => words.Count > 1 && words[1] is "add" or "remove",
            _ => false,
        };
    }

    /// <summary>The verb's run: a read said, a change pressed (its route where the instance answers, its operation alone), or its usage.</summary>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, ToolsWorld world, CancellationToken ct)
    {
        var error = world.Instance.Error;
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            await world.Instance.Out.WriteLineAsync(Usage).ConfigureAwait(false);
            return 0;
        }

        try
        {
            return await Run(args, world, ct).ConfigureAwait(false);
        }
        catch (ToolWords refused)
        {
            await error.WriteLineAsync($"{Prefix}: {refused.Message}").ConfigureAwait(false);
            return refused.Exit;
        }
    }

    /// <summary>The words read: the verb (<c>list</c> where none), the tool or a location's sub-verb, the way or version, and what follows.</summary>
    private sealed record Spoken(string Verb, string? Id, string? Way, string? Named)
    {
        public static Spoken Of(IReadOnlyList<string> args)
        {
            var words = Operands(args);
            string? At(int at) => words.Count > at ? words[at] : null;
            return new(At(0) ?? "list", At(1), At(2), At(3));
        }

        public ToolDeclaration NeedTool(string said)
        {
            if (Id is null) throw new ToolWords($"`tool {said}` needs a tool — one of: {string.Join(", ", Tools.Declared.Select(tool => tool.Id))}");
            return Tools.Find(Id) ?? throw new ToolWords(Tools.Undeclared(Id));
        }
    }

    private static string RequireHome(ToolsWorld world) =>
        world.Instance.Home ?? throw new ToolWords($"{DaorisHome.Sentence} (wanted: {Tools.FileName})");

    private static async Task<int> Run(IReadOnlyList<string> args, ToolsWorld world, CancellationToken ct)
    {
        if (Plan(args, world) is { } press) return await PressAsync(world, press, ct).ConfigureAwait(false);

        var words = Spoken.Of(args);
        void Say(string line) => world.Instance.Out.WriteLine(line);
        switch (words.Verb)
        {
            case "list":
                return List(RequireHome(world), world);

            case "path":
            {
                var tool = words.NeedTool("path");
                var resolution = Tools.Resolve(RequireHome(world), tool.Id, world.SearchPath);
                if (resolution.File is null)
                {
                    Say($"{Prefix}: {resolution.Problem}");
                    return 1;
                }

                // Alone on its line, for a script to take.
                Say(resolution.File);
                return 0;
            }

            case "use":
            {
                // A way set to the system's or a file is a press (Plan); a managed use answers once the driver's download starts, and
                // its end is an event a terminal hears only by subscribing (RPC1g): this verb's alone, as a download and an update are.
                var tool = words.NeedTool("use");
                var home = RequireHome(world);
                if (words.Way == "managed") return await ManagedAsync(world, home, tool, "use", words.Named, ct).ConfigureAwait(false);
                throw new ToolWords($"`tool use {tool.Id}` takes `system`, `managed [<version>]` or `file <path>`");
            }

            case "download":
            {
                var home = RequireHome(world);
                return await ManagedAsync(world, home, words.NeedTool("download"), "download", words.Way, ct).ConfigureAwait(false);
            }

            case "update":
            {
                var home = RequireHome(world);
                return await ManagedAsync(world, home, words.NeedTool("update"), "update", null, ct).ConfigureAwait(false);
            }

            case "locations":
                return Locations(world, RequireHome(world), words.Id);

            default:
                throw new ToolWords($"unknown tool verb '{words.Verb}' — one of: {string.Join(", ", Verbs)}");
        }
    }

    /// <summary>
    /// The press <paramref name="args"/> ask for, where they name a change a route takes: a way set to the system's or a file, a
    /// delete, a location added or removed, a look. Null for a read and a long verb; a refusal of the words is <see cref="ToolWords"/>,
    /// nothing done. A test sends its payload through the route.
    /// </summary>
    public static ToolPress? Plan(IReadOnlyList<string> args, ToolsWorld world)
    {
        var words = Spoken.Of(args);
        void Say(string line) => world.Instance.Out.WriteLine(line);
        switch (words.Verb)
        {
            case "use":
            {
                var tool = words.NeedTool("use");
                var home = RequireHome(world);
                var written = DaorisHome.Entry(home, HomeEntry.Tools);
                if (words.Way == "system")
                {
                    int Said()
                    {
                        var resolution = Tools.Resolve(home, tool.Id, world.SearchPath);
                        Say($"{Prefix}: {tool.Name} is set to the system's, from PATH — written to {written}.");
                        Say(resolution.File is not null
                            ? $"  `daoris tool path {tool.Id}` answers {resolution.File}."
                            : $"  `daoris tool path {tool.Id}` answers: {resolution.Problem}.");
                        return 0;
                    }

                    return new ToolPress("use", ToolRoutes.Use, new JsonObject { ["tool"] = tool.Id, ["action"] = "system" }, _ =>
                    {
                        Tools.UseSystem(home, tool.Id);
                        return Task.FromResult<object?>(null);
                    }, _ => null, _ => Said());
                }

                if (words.Way == "file")
                {
                    var named = words.Named ?? throw new ToolWords($"`tool use {tool.Id} file` needs a path — the executable to run");
                    var file = Tools.IsWholePath(named) ? named : Path.GetFullPath(Path.Combine(world.WorkingDirectory, named));
                    int Said()
                    {
                        Say($"{Prefix}: {tool.Name} is set to the file {file} — written to {written}.");
                        Say($"  `daoris tool path {tool.Id}` answers {file}; `daoris tool use {tool.Id} system` puts it back on PATH.");
                        return 0;
                    }

                    // The file is asked its version before it is written, here as the instance asks it (ToolChoices.UseFileAsync).
                    return new ToolPress("use", ToolRoutes.Use, new JsonObject { ["tool"] = tool.Id, ["action"] = "file", ["file"] = file },
                        async stop => await ToolChoices.UseFileAsync(home, tool.Id, file, world.AskVersion, stop).ConfigureAwait(false),
                        _ => null, _ => Said());
                }

                return null;
            }

            case "delete":
            {
                var tool = words.NeedTool("delete");
                var version = words.Way ?? throw new ToolWords($"`tool delete {tool.Id}` needs a version — `daoris tool list` names the ones downloaded");
                var home = RequireHome(world);
                int Said(object? folder)
                {
                    Say($"{Prefix}: {tool.Name} {version} is deleted — {folder}.");
                    return 0;
                }

                // KEEP1b: alone, no Daoris runs to say what a session was handed, so the home's process markers say whether one runs at all.
                return new ToolPress("delete", ToolRoutes.Delete, new JsonObject { ["tool"] = tool.Id, ["version"] = version },
                    async stop => (object?)await ToolInstall.DeleteAsync(home, tool.Id, version,
                        token => ToolDownloads.Current.RunningAsync(home, token), ct: stop).ConfigureAwait(false),
                    answer => Text(answer, "folder") ?? "", Said);
            }

            case "locations" when words.Id is "add" or "remove":
            {
                var home = RequireHome(world);
                var sub = words.Id;
                var address = words.Way ?? throw new ToolWords($"`tool locations {sub}` needs an address — https://, or http:// to this machine");
                // Refused here in this verb's words before anything is sent, as the instance would refuse it in its own.
                if (sub == "add" && Tools.LocationProblem(address) is { } problem) throw new ToolWords($"{problem} — nothing was written.");
                int Said(object? changed)
                {
                    var done = changed is true;
                    if (sub == "add")
                    {
                        Say(done
                            ? $"{Prefix}: {address} is added, read before the list built in — `daoris tool look` fetches it."
                            : $"{Prefix}: {address} is already a location — nothing was written.");
                    }
                    else
                    {
                        Say(done
                            ? $"{Prefix}: {address} is removed: its versions are no longer offered, and a version already downloaded stays."
                            : $"{Prefix}: {address} is not a location — nothing was written.");
                    }

                    return 0;
                }

                return new ToolPress("locations", ToolRoutes.Location, new JsonObject { ["action"] = sub, ["address"] = address },
                    _ => Task.FromResult<object?>(sub == "add" ? ToolChoices.AddLocation(home, address) : Tools.RemoveLocation(home, address)),
                    answer => answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty(sub == "add" ? "added" : "removed", out var flag)
                              && flag.ValueKind == JsonValueKind.True,
                    Said);
            }

            case "look":
            {
                var home = RequireHome(world);
                // The look's copies are kept under the home, so while Daoris runs it is the instance's own look, which this terminal reads.
                return new ToolPress("look", ToolRoutes.Look, new JsonObject(), async stop =>
                        await ToolInstall.LookAsync(home, world.Platform, stop, world.Transport, world.LookBound, world.Log, world.Now())
                            .ConfigureAwait(false),
                    answer => answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("looks", out var looks)
                              && looks.ValueKind == JsonValueKind.Array
                        ? looks.EnumerateArray().Select(each => new LocationLook(
                            Text(each, "address") ?? "", Text(each, "outcome") ?? "", Text(each, "sentence") ?? "", [], [])).ToList()
                        : new List<LocationLook>(),
                    fields => Looked(world, home, (IReadOnlyList<LocationLook>)fields!));
            }

            default:
                return null;
        }
    }

    /// <summary>Every tool as it runs, the versions downloaded, and what the file keeps without applying: exit 1 where one is refused.</summary>
    private static int List(string home, ToolsWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var read = Tools.Read(home);
        Say(read.Exists ? $"{Prefix}: {read.Path}" : $"{Prefix}: {read.Path} — no file, so every tool is the system's, from PATH.");
        var refused = false;
        foreach (var tool in Tools.Declared)
        {
            var resolution = Tools.Resolve(read, home, tool.Id, world.SearchPath);
            refused |= resolution.Refused;
            Say(Row(tool, resolution));
            var kept = ToolInstall.Downloaded(home, tool.Id);
            if (kept.Count > 0) Say($"        downloaded: {string.Join(", ", kept)}");
        }

        foreach (var note in read.Notes) Say($"  {note}");
        Say("");
        Say("  `daoris tool use <tool> system|managed [<version>]|file <path>` sets one; nothing switches on its own.");
        return refused ? 1 : 0;
    }

    /// <summary>How one tool reads on a row of <c>list</c>.</summary>
    private static string Row(ToolDeclaration tool, ToolResolution resolution)
    {
        var lead = $"  {tool.Id,-5} {tool.Name,-10} ";
        var how = resolution.Way == ToolWay.Managed ? $"managed {resolution.Version}" : resolution.Way is { } way ? Tools.Words(way) : null;
        if (how is null) return $"{lead}refused — {resolution.Problem}";
        if (resolution.File is not null) return $"{lead}{how}: {resolution.File}";
        return $"{lead}{how}: {(resolution.Refused ? "refused" : "not found")} — {resolution.Problem}";
    }

    /// <summary><c>locations</c> alone lists them in read order (§3.3); <c>add</c> and <c>remove</c> are presses (<see cref="Plan"/>).</summary>
    private static int Locations(ToolsWorld world, string home, string? sub)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        if (sub is not null) throw new ToolWords("`tool locations` takes `add <address>` or `remove <address>`, or nothing to list them");

        var read = Tools.Read(home);
        Say($"{Prefix}: {read.Path} — the lists versions come from, read in this order:");
        var now = world.Now();
        for (var at = 0; at < read.Locations.Count; at++)
        {
            var each = read.Locations[at];
            var list = ToolResources.ReadFile(ToolResources.LocationCopy(home, each), each);
            Say(list.Exists
                ? $"  {at + 1}. {each} — fetched {ToolInstall.Age(list.Path!, now)} ago, vouched for by {list.Integrity}, sha256 {list.Sha256}"
                : $"  {at + 1}. {each} — never fetched, so it names nothing yet");
        }

        var built = ToolResources.ReadFile(ToolResources.BuiltInFor(home, world.Instance.BaseDirectory), ToolResources.BuiltIn);
        Say(built.Exists
            ? $"  {read.Locations.Count + 1}. {ToolResources.BuiltIn} — {built.Path}, sha256 {built.Sha256}"
            : $"  {read.Locations.Count + 1}. {ToolResources.BuiltIn} — none beside this home ({built.Path})");
        foreach (var note in read.Notes) Say($"  {note}");
        return 0;
    }

    /// <summary>What a look found, each location's sentence, then what each tool runs and the newest the lists name: exit 1 where one was not fetched.</summary>
    private static int Looked(ToolsWorld world, string home, IReadOnlyList<LocationLook> looks)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var platform = world.Platform;
        Say(looks.Count == 0
            ? $"{Prefix}: no resource location is listed — only the list built in is read. `daoris tool locations add <address>` adds one."
            : $"{Prefix}: looked at {looks.Count} location{(looks.Count == 1 ? "" : "s")}:");
        foreach (var each in looks) Say($"  {each.Sentence}");

        var read = Tools.Read(home);
        var merged = ToolResources.Merge(ToolResources.ReadLists(home, world.Instance.BaseDirectory, read), platform);
        Say("");
        foreach (var tool in Tools.Declared)
        {
            var entry = read.Entries[tool.Id];
            var runs = entry.Problem is not null ? "refused" : entry.Way == ToolWay.Managed ? $"managed {entry.Version}" : Tools.Words(entry.Way!.Value);
            var offers = merged.Tools.First(each => each.Tool == tool.Id);
            var newest = offers.Versions.Count > 0 ? offers.Versions[0] : null;
            var newer = newest is not null && entry.Way == ToolWay.Managed && entry.Version is not null
                && ToolResources.CompareVersions(newest.Version, entry.Version) > 0;
            Say($"  {tool.Id,-5} {tool.Name,-10} runs {runs}; {(newest is not null
                ? $"the lists name {newest.Version} ({newest.Lists[0]}){(newer ? $" — `daoris tool update {tool.Id}` moves to it" : "")}"
                : $"no list names a version for {platform ?? "this machine"}")}");
        }

        foreach (var note in merged.Notes) Say($"  {note}");
        return looks.All(each => each.Outcome == "fetched") ? 0 : 1;
    }

    /// <summary>
    /// Download, use or update a managed version (§3.6, §3.7; the alone-path design §1.4): planned first, so a refusal fetches nothing;
    /// staged under this process's name holding nothing; then published, and for a use or an update written, under the home's lease,
    /// waited for and never refused. The person's Ctrl+C takes the staging with it, and nothing switches.
    /// </summary>
    private static async Task<int> ManagedAsync(ToolsWorld world, string home, ToolDeclaration tool, string action, string? asked, CancellationToken ct)
    {
        var error = world.Instance.Error;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        if (world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
        {
            await error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
            return 1;
        }

        var platform = world.Platform;
        var plan = ToolInstall.Plan(home, ToolResources.Merge(ToolResources.ReadLists(home, world.Instance.BaseDirectory), platform), tool.Id, action, asked);
        if (plan.Problem is not null)
        {
            await error.WriteLineAsync($"{Prefix}: {plan.Problem}").ConfigureAwait(false);
            return 1;
        }

        if (plan.Nothing is not null)
        {
            Say($"{Prefix}: {plan.Nothing}.");
            return 0;
        }

        try
        {
            if (!plan.Fetch) return await FinishAsync(world, home, tool, plan, fetched: null, published: false, ct).ConfigureAwait(false);

            var offered = plan.Offered!;
            var licence = plan.Licence is { } named ? $"{named.Id}{(named.Url is { } url ? $" ({url})" : "")}" : "no licence named";
            Say($"{Prefix}: {tool.Name} {offered.Version} for {platform} — {Megabytes(offered.Size)}, {licence}"
                + $"{(plan.Source is { } source ? $", from {source}" : "")}.");
            Say("  Daoris redistributes nothing: it downloads from the addresses the lists name, as you, and checks the bytes.");

            ToolStaged? staged;
            try
            {
                staged = await ToolInstall.StageAsync(home, tool.Id, offered, platform!, Say, ct, world.Transport, world.Log, world.Process, world.Running)
                    .ConfigureAwait(false);
            }
            catch (ToolRefusal refusal)
            {
                await error.WriteLineAsync($"{Prefix}: {refusal.Message}").ConfigureAwait(false);
                return 1;
            }

            // Published meanwhile, between the plan and the stage: nothing was fetched.
            if (staged is null) return await FinishAsync(world, home, tool, plan, fetched: null, published: false, ct).ConfigureAwait(false);

            HomeLease lease;
            try
            {
                lease = await Publication(world, home, action, ct).ConfigureAwait(false);
            }
            catch
            {
                ToolInstall.Discard(staged);
                throw;
            }

            using (lease)
            {
                var (exe, published) = ToolInstall.Publish(staged, Say, world.Log);
                return Finish(world, home, tool, plan, exe, published);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await error.WriteLineAsync($"{Prefix}: stopped at your Ctrl+C — nothing was kept, and nothing switched.").ConfigureAwait(false);
            return 2;
        }
        catch (DriverException refused)
        {
            await error.WriteLineAsync($"{Prefix}: {refused.Message}").ConfigureAwait(false);
            return 2;
        }
    }

    /// <summary>A version already downloaded: a download says so; a use or an update writes it under the lease, waited for.</summary>
    private static async Task<int> FinishAsync(
        ToolsWorld world, string home, ToolDeclaration tool, ToolPlan plan, string? fetched, bool published, CancellationToken ct)
    {
        if (plan.Action == "download") return Finish(world, home, tool, plan, fetched, published);
        using var lease = await Publication(world, home, plan.Action, ct).ConfigureAwait(false);
        return Finish(world, home, tool, plan, fetched, published);
    }

    /// <summary>
    /// The home's lease for a publication (the alone-path design §1.4): waited for, saying once its hold has lasted thirty seconds who
    /// holds it; beside an instance, holding nothing.
    /// </summary>
    private static Task<HomeLease> Publication(ToolsWorld world, string home, string action, CancellationToken ct) =>
        HomeLease.ForPublicationAsync(home, $"daoris-driver tool {action}",
            holder => world.Instance.Error.WriteLine(
                $"{Prefix}: waiting for {holder.Named} to let this home go; what was downloaded is kept until then, and Ctrl+C stops."),
            ct, world.Waiting, world.Now);

    /// <summary>What a download, a use or an update ends with: <c>use</c> and <c>update</c> switch to the version, <c>download</c> never does.</summary>
    private static int Finish(ToolsWorld world, string home, ToolDeclaration tool, ToolPlan plan, string? fetched, bool published)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var version = plan.Version!;
        var exe = fetched ?? Tools.ManagedExecutable(home, tool.Id, version).File!;
        if (plan.Action == "download")
        {
            Say(fetched is null
                ? $"{Prefix}: {tool.Name} {version} is already downloaded — nothing was fetched, and nothing switches."
                : published
                    ? $"{Prefix}: {tool.Name} {version} is downloaded and verified — nothing switches."
                    : $"{Prefix}: {tool.Name} {version} is already downloaded, by another run meanwhile — nothing switches.");
            Say($"  {exe}");
            Say($"  `daoris tool use {tool.Id} managed {version}` runs it.");
            return 0;
        }

        Tools.UseManaged(home, tool.Id, version);
        Say($"{Prefix}: {tool.Name} is set to managed {version}{(plan.Current is { } current && current != version ? $" (it was {current})" : "")} — "
            + $"written to {DaorisHome.Entry(home, HomeEntry.Tools)}.");
        Say($"  `daoris tool path {tool.Id}` answers {exe}; `daoris tool use {tool.Id} system` puts it back on PATH.");
        return 0;
    }

    /// <summary>
    /// A press as the alone-path design §1.1 takes it: its route through the instance that answers, its words read from the answer's
    /// fields; else its operation here, refused from a session's terminal where it is the person's (D194 point 5), its refusal one line
    /// with its code (D194 point 6).
    /// </summary>
    private static async Task<int> PressAsync(ToolsWorld world, ToolPress press, CancellationToken ct)
    {
        var error = world.Instance.Error;
        async Task<int> Alone()
        {
            if (press.Route.Class == RouteClass.Person && world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
            {
                await error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
                return 1;
            }

            object? done;
            try
            {
                done = await press.Act(ct).ConfigureAwait(false);
            }
            catch (DriverException refused)
            {
                await error.WriteLineAsync($"{Prefix}: {refused.Message} ({ToolCodes.Of(refused)})").ConfigureAwait(false);
                return 2;
            }
            catch (IOException held)
            {
                // The file's edit lock, held by another writer for the whole of its wait: nothing was written.
                await error.WriteLineAsync($"{Prefix}: {held.Message}").ConfigureAwait(false);
                return 2;
            }

            return press.Words(done);
        }

        return await world.Instance.PressAsync(press.Route, press.Payload, Alone, answer => press.Words(press.Fields(answer)), ct)
            .ConfigureAwait(false);
    }

    /// <summary>The words, the flags left out: what the CLI's <c>operands</c> read.</summary>
    private static List<string> Operands(IReadOnlyList<string> args) => [.. args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal))];

    private static string Megabytes(long bytes) => $"{(bytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} MB";

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
