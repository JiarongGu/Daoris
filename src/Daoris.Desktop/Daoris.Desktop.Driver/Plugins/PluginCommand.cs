using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Where <c>daoris-driver plugin</c> acts (PUSH1f2f): the terminal it says its words on and finds the running Daoris from, the folder a
/// relative folder is named from, and the harness names this build carries. A test hands its own of each.
/// </summary>
public sealed record PluginWorld
{
    /// <summary>The terminal: its home (null where none is named), its variables, its streams, and where it finds the instance.</summary>
    public required InstanceWorld Instance { get; init; }

    /// <summary>Where the command runs from: a relative folder is named from here.</summary>
    public string WorkingDirectory { get; init; } = Environment.CurrentDirectory;

    /// <summary>The harness names no plugin may declare; null for this build's (<see cref="AdapterSet.Built"/>).</summary>
    public IReadOnlyList<string>? Reserved { get; init; }

    /// <summary>The install's offers folder; null for the one beside the home, where the CLI looked (<see cref="PluginOffers.BesideHome"/>).</summary>
    public string? Offers { get; init; }
}

/// <summary>
/// One plugin's press (PUSH1f2f, D194 points 4 and 6): the route the window's press sends, or none where the terminal's verb alone does it
/// (a replace), the payload the verb writes from its words, the operation it calls alone, and the one function that says it on both paths.
/// <see cref="PluginCommand.Plan"/> makes it; a test sends its payload through the route.
/// </summary>
/// <param name="Route">The route, or null where none takes the press: it then acts alone, on the terminal's word.</param>
/// <param name="Act">The operation, alone: it writes.</param>
/// <param name="Words">The verb's words, both paths', returning its exit code; what they say was read before the press.</param>
public sealed record PluginPress(string Verb, InstanceRoute? Route, JsonObject Payload, Action Act, Func<int> Words)
{
    /// <summary>The end state asked for is already so (a remove of what is not there): said, and nothing pressed or written.</summary>
    public bool Nothing { get; init; }
}

/// <summary>A refusal the command makes before it acts, in its words, with today's exit: its grammar and its own checks.</summary>
public sealed class PluginWords(string message, int exit = 2) : Exception(message)
{
    public int Exit { get; } = exit;
}

/// <summary>
/// <c>daoris-driver plugin</c> (PUSH1f2f, D194, the alone-path design §1.2): this machine's plugins from a terminal, the words
/// <c>daoris plugin …</c> hands this program, beside the host's own <c>plugins</c> (the kit, a package, a plugin's page); no sub-verb
/// is shared. Each change is its route through <see cref="InstanceDoor.PressAsync"/> where the running Daoris answers, confirmed in its
/// window; else its operation, on the terminal's word (D50), refused from a session's terminal (D194 point 5). A replace has no route: the
/// window's add adds and never replaces, so the terminal's replace is this program's alone. Its words are one function per verb on both
/// paths, today's text kept (<c>fixtures/plugins-text.json</c>).
/// </summary>
/// <remarks>
/// Exit codes are the CLI's, case by case: 0 said or done, a remove of what is not there among them · 1 an update's plan without
/// <c>--yes</c>, the person's refusal in the window, or a session's terminal · 2 a refusal of the words, the operation or the route.
/// </remarks>
public static class PluginCommand
{
    /// <summary>The verbs this program has: every one the CLI's <c>daoris plugin</c> had, which it hands on whole.</summary>
    public static IReadOnlyList<string> Verbs { get; } = ["list", "add", "update", "remove", "enable", "disable"];

    public const string Usage =
        """
        usage: daoris-driver plugin [list]
               daoris-driver plugin add <folder> · add --offer <id>
               daoris-driver plugin update <id> [--yes] · remove <id> · enable|disable <id>
        `daoris plugin …` runs these; the kit is `daoris-driver plugins new|try`, a package `daoris-driver plugins install`.
        """;

    private const string Prefix = "daoris";

    /// <summary>
    /// Whether the words are a short change, which holds the home's lease from the top: an add, an update with <c>--yes</c>, a remove and a
    /// switch. A list, an update's plan, the kit's pointers and the usage hold none (the alone-path design §1.2).
    /// </summary>
    public static bool Changes(IReadOnlyList<string> args)
    {
        if (args.Any(arg => arg is "--help" or "-h")) return false;
        var verb = args.Count > 0 ? args[0] : "list";
        return verb switch
        {
            "add" or "remove" or "enable" or "disable" => true,
            "update" => args.Contains("--yes"),
            _ => false,
        };
    }

    /// <summary>The verb's run: a read said, a change pressed (its route where the instance answers, its operation alone), or its usage.</summary>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, PluginWorld world, CancellationToken ct)
    {
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            await world.Instance.Out.WriteLineAsync(Usage).ConfigureAwait(false);
            return 0;
        }

        try
        {
            if (Plan(args, world) is { } press) return await PressAsync(world, press, ct).ConfigureAwait(false);
            return Read(args, world);
        }
        catch (PluginWords refused)
        {
            await world.Instance.Error.WriteLineAsync($"{Prefix}: {refused.Message}").ConfigureAwait(false);
            return refused.Exit;
        }
    }

    private static string RequireHome(PluginWorld world) =>
        world.Instance.Home ?? throw new PluginWords($"{DaorisHome.Sentence} (wanted: plugins/)");

    private static IReadOnlyList<string> Reserved(PluginWorld world) => world.Reserved ?? AdapterSet.Built().Names;

    /// <summary>The install's offers, beside the home, where the CLI looked for them: in an install, its <c>app/plugin-offers/</c>.</summary>
    private static string Offers(PluginWorld world, string home) => world.Offers ?? PluginOffers.BesideHome(home);

    /// <summary>The id a verb names, its second word, refused before it becomes a path (REV3: `remove ..` was the whole home).</summary>
    private static string RequireId(IReadOnlyList<string> args, string verb)
    {
        var id = args.Count > 1 ? args[1] : null;
        if (id is null || id.StartsWith("--", StringComparison.Ordinal))
        {
            throw new PluginWords($"`plugin {verb}` needs a plugin id — e.g. `daoris plugin {verb} acme.quiet-hours`.");
        }

        if (!PluginCatalog.IsId(id.ToLowerInvariant())) throw new PluginWords(PluginInstall.NotAnId(id));
        return id;
    }

    /// <summary>The reads and the words that are no press: a list, an update's plan without <c>--yes</c>, the kit's pointers, the rest refused.</summary>
    private static int Read(IReadOnlyList<string> args, PluginWorld world)
    {
        var verb = args.Count > 0 ? args[0] : "list";
        switch (verb)
        {
            case "list":
                return List(RequireHome(world), world);

            // The plan, said and nothing replaced; with --yes the update is a press (Plan).
            case "update":
            {
                var home = RequireHome(world);
                Planned(home, RequireId(args, "update"), world);
                Say(world, "  Not updated: this is what would change. Run it again with --yes to update it.");
                return 1;
            }

            // The kit a plugin is made with lives with the code that starts plugins (PLUG8, D101): asked for here, it says where it is.
            case "new" or "try":
                throw new PluginWords(
                    $"the plugin kit is `daoris-driver plugins {verb}`, or the Plugins place — it starts a plugin as the driver would, so it "
                    + "lives with the driver. `daoris-driver plugins` says what each takes.");

            // A package is read by the host's `plugins install` (PLUGDIST1a, D120 §4).
            case "install":
                throw new PluginWords(
                    "a plugin package is installed by `daoris-driver plugins install <file.nupkg>` — the driver reads a package and checks it "
                    + "before anything is extracted. `daoris plugin add <folder>` installs a plugin's folder.");

            default:
                throw new PluginWords($"unknown plugin verb '{verb}' — one of: list, add, update, remove, enable, disable (and new, try: the kit)");
        }
    }

    /// <summary>
    /// The press <paramref name="args"/> ask for, where they name a change: an add (a route where nothing of its id is installed, else the
    /// terminal's replace alone), an update with <c>--yes</c>, a remove, a switch. Null for a read; a refusal of the words or of the
    /// command's own checks is <see cref="PluginWords"/>, nothing done. A test sends its payload through the route.
    /// </summary>
    public static PluginPress? Plan(IReadOnlyList<string> args, PluginWorld world)
    {
        var verb = args.Count > 0 ? args[0] : "list";
        switch (verb)
        {
            case "add":
                return Add(args, world);

            case "update" when args.Contains("--yes"):
            {
                var home = RequireHome(world);
                var plan = Planned(home, RequireId(args, "update"), world);
                int Said()
                {
                    Say(world, $"{Prefix}: updated plugin `{plan.Id}` from {plan.From}.");
                    var data = DaorisHome.Entry(home, HomeEntry.Plugins, PluginCatalog.DataFolder, plan.Id);
                    if (PluginFolders.There(data)) Say(world, $"  {data} kept, untouched.");
                    Say(world, "  It takes effect at the driver's next look — its hook, if it speaks, restarted at the next tick.");
                    return 0;
                }

                // While Daoris runs, the update's press stops the plugin's process before it swaps the folder; alone, none runs.
                return new PluginPress("update", PluginRoutes.Update, new JsonObject { ["id"] = plan.Id, ["apply"] = true },
                    () => PluginInstall.Update(home, plan.Id, Reserved(world), Offers(world, home)), Said);
            }

            case "remove":
            {
                var home = RequireHome(world);
                var id = RequireId(args, "remove");
                if (!PluginFolders.There(DaorisHome.Entry(home, HomeEntry.Plugins, id)))
                {
                    // The end state asked for: said, and nothing pressed.
                    return new PluginPress("remove", null, [], () => { }, () =>
                    {
                        Say(world, $"{Prefix}: no plugin `{id}` on this machine — nothing to remove.");
                        return 0;
                    }) { Nothing = true };
                }

                var data = DaorisHome.Entry(home, HomeEntry.Plugins, PluginCatalog.DataFolder, id);
                int Said()
                {
                    Say(world, $"{Prefix}: plugin `{id}` removed from this machine.");
                    if (PluginFolders.There(data)) Say(world, $"  What it kept is still at {data} — yours to delete, never Daoris's.");
                    return 0;
                }

                // While Daoris runs, the row's remove stops the plugin's process before the folder goes; alone, the same operation.
                return new PluginPress("remove", PluginRoutes.Action, new JsonObject { ["action"] = "remove", ["id"] = id },
                    () => PluginInstall.Remove(home, id), Said);
            }

            case "enable" or "disable":
            {
                var home = RequireHome(world);
                var id = RequireId(args, verb);
                var entry = PluginCatalog.Load(home, Reserved(world)).Plugins
                    .FirstOrDefault(each => string.Equals(each.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PluginWords($"no plugin `{id}` on this machine — `daoris plugin list` shows what there is.");
                var on = verb == "enable";
                int Said()
                {
                    Say(world, $"{Prefix}: plugin `{entry.Manifest.Id}` is {(on ? "on" : "off")} on this machine.");
                    if (!on) Say(world, "  It stays where it is, and so does what it kept; the driver stops asking it at its next look.");
                    else if (entry.Problem is { } problem) Say(world, $"  ⚠ {problem}");
                    return 0;
                }

                return new PluginPress(verb, PluginRoutes.Action, new JsonObject { ["action"] = verb, ["id"] = entry.Manifest.Id }, () =>
                {
                    if (on) PluginState.Enable(home, entry.Manifest.Id);
                    else PluginState.Disable(home, entry.Manifest.Id);
                }, Said);
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// An add (PLUG9): a folder named, or one of the install's offers by its id; judged first by the driver's own add (where it is, then
    /// its manifest read as the catalogue reads it, with nothing this build refuses), so a refusal copies nothing. Where nothing of its id
    /// is installed it is the window's add; where something is, the terminal's replace, which no route makes.
    /// </summary>
    private static PluginPress Add(IReadOnlyList<string> args, PluginWorld world)
    {
        var home = RequireHome(world);
        string from;
        PluginSource recorded;
        string? offer = null;
        var offerAt = args.ToList().IndexOf("--offer");
        if (offerAt != -1)
        {
            var wanted = offerAt + 1 < args.Count ? args[offerAt + 1] : null;
            if (wanted is null || wanted.StartsWith("--", StringComparison.Ordinal))
            {
                throw new PluginWords("`--offer` names one of Daoris's own plugins by its id — `daoris plugin list` shows what this install offers.");
            }

            if (!PluginCatalog.IsId(wanted.ToLowerInvariant())) throw new PluginWords(PluginInstall.NotAnId(wanted));
            var offers = PluginOffers.Load(Offers(world, home), home, Reserved(world));
            var found = offers.FirstOrDefault(each => string.Equals(each.Id, wanted, StringComparison.OrdinalIgnoreCase))
                ?? throw new PluginWords($"this install offers no plugin `{wanted}` — "
                    + (offers.Count > 0 ? $"it offers {string.Join(", ", offers.Select(each => $"`{each.Id}`"))}." : "it offers none."));
            (from, recorded, offer) = (found.Folder, PluginSource.FromOffer(found.Id), found.Id);
        }
        else
        {
            var named = args.Count > 1 ? args[1] : null;
            if (named is null || named.StartsWith("--", StringComparison.Ordinal))
            {
                throw new PluginWords("`plugin add` needs a folder holding a `plugin.json` — e.g. `daoris plugin add ./acme.quiet-hours`.");
            }

            from = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.IsPathRooted(named) ? named : Path.Combine(world.WorkingDirectory, named)));
            recorded = PluginSource.FromFolder(from);
        }

        if (!PluginFolders.HasFile(Path.Combine(from, PluginCatalog.ManifestName)))
        {
            throw new PluginWords($"no `{PluginCatalog.ManifestName}` in {from} — a plugin is a folder with a manifest at its root.");
        }

        if (PluginInstall.Placement(home, from) is { } misplaced) throw new PluginWords($"{misplaced} Nothing was copied.");
        var reserved = Reserved(world);
        var (read, refusal) = PluginInstall.Read(from, reserved);
        if (refusal is not null) throw new PluginWords($"{refusal} Nothing was copied.");
        var manifest = read!;

        var target = DaorisHome.Entry(home, HomeEntry.Plugins, manifest.Id);
        var replacing = PluginFolders.There(target);
        var data = DaorisHome.Entry(home, HomeEntry.Plugins, PluginCatalog.DataFolder, manifest.Id);
        int Said()
        {
            Say(world, $"{Prefix}: {(replacing ? "replaced" : "added")} plugin `{manifest.Id}` at {target}");
            Say(world, $"  {PluginPackageCommand.Describe(manifest)}.");
            Say(world, $"  From {recorded.Said}; `daoris plugin update {manifest.Id}` takes a newer one from there.");
            if (PluginFolders.There(data)) Say(world, $"  {data} kept, untouched.");
            Say(world, "  It takes effect at the driver's next look — a harness on the roster, a hook at the next tick.");
            return 0;
        }

        // The window's install adds and never replaces: a replace is this verb's alone, on the terminal's word.
        if (replacing) return new PluginPress("add", null, [], () => PluginInstall.Replace(home, from, recorded, reserved), Said);
        return offer is not null
            ? new PluginPress("add", PluginRoutes.Install, new JsonObject { ["offer"] = offer },
                () => PluginInstall.AddOffer(home, Offers(world, home), offer, reserved), Said)
            : new PluginPress("add", PluginRoutes.Add, new JsonObject { ["folder"] = from },
                () => PluginInstall.Add(home, from, reserved), Said);
    }

    /// <summary>What updating would change, said line by line before anything is pressed; a refusal is the plan's, nothing replaced.</summary>
    private static PluginUpdatePlan Planned(string home, string id, PluginWorld world)
    {
        var (plan, refusal) = PluginInstall.PlanUpdate(home, id, Reserved(world), Offers(world, home));
        if (plan is null) throw new PluginWords($"{refusal} Nothing was replaced.");
        Say(world, $"{Prefix}: plugin `{plan.Id}`, from {plan.Source.Said}:");
        if (plan.Changes.Count == 0) Say(world, "  What it declares does not change; its files are replaced from there.");
        foreach (var change in plan.Changes)
        {
            Say(world, $"  {change.What,-10} {(change.Was.Length > 0 ? change.Was : "(none)")} → {(change.Now.Length > 0 ? change.Now : "(none)")}");
        }

        return plan;
    }

    /// <summary>Every plugin, what it declares and speaks, its tools, where it came from and its icon's problem; then the offers not installed.</summary>
    private static int List(string home, PluginWorld world)
    {
        var reserved = Reserved(world);
        var catalog = PluginCatalog.Load(home, reserved);
        var root = DaorisHome.Entry(home, HomeEntry.Plugins);
        var pad = new string(' ', 24);
        if (catalog.Plugins.Count == 0)
        {
            Say(world, $"{Prefix}: no plugins on this machine — a plugin is a folder under {root}");
            Say(world, "  with a `plugin.json`; `daoris plugin add <folder>` copies one in.");
        }
        else
        {
            Say(world, $"{Prefix}: {root}");
            foreach (var entry in catalog.Plugins)
            {
                var manifest = entry.Manifest;
                Say(world, $"  {manifest.Id,-24} {manifest.Name}{Version(manifest)}{(entry.Enabled ? "" : "  (off)")}");
                Say(world, $"  {pad} {(entry.Problem is { } problem ? $"⚠ {problem}" : PluginPackageCommand.Describe(manifest))}");
                foreach (var line in ToolLines(manifest)) Say(world, $"  {pad} {line}");
                Say(world, $"  {pad} {SourceLine(entry.Folder)}");
                if (PluginIcon.Read(entry.Folder, manifest).Problem is { } icon) Say(world, $"  {pad} icon not drawn: {icon}");
            }
        }

        var folder = Offers(world, home);
        var offered = PluginOffers.Load(folder, home, reserved).Where(offer => !offer.Installed).ToList();
        if (offered.Count > 0)
        {
            Say(world, $"{Prefix}: Daoris's own plugins, offered by this install, not installed ({folder}):");
            foreach (var offer in offered)
            {
                Say(world, $"  {offer.Id,-24} {offer.Manifest.Name}{Version(offer.Manifest)}");
                Say(world, $"  {pad} {(offer.Problem is { } problem ? $"⚠ {problem}" : PluginPackageCommand.Describe(offer.Manifest))}");
                foreach (var line in ToolLines(offer.Manifest)) Say(world, $"  {pad} {line}");
                if (offer.Needs.Count > 0) Say(world, $"  {pad} needs: {string.Join("; ", offer.Needs)}");
                if (PluginIcon.Read(offer.Folder, offer.Manifest).Problem is { } icon) Say(world, $"  {pad} icon not drawn: {icon}");
                if (offer.Problem is null) Say(world, $"  {pad} `daoris plugin add --offer {offer.Id}` installs it; nothing runs before that.");
            }
        }

        return 0;
    }

    private static string Version(PluginManifest manifest) => manifest.Version.Length > 0 ? $" {manifest.Version}" : "";

    /// <summary>What the list says of a plugin's tools: the ones it runs, then each problem, never a refusal (PLUGTOOL1a).</summary>
    private static IEnumerable<string> ToolLines(PluginManifest manifest)
    {
        var read = manifest.Tools.Where(tool => tool.Problem is null).ToList();
        if (read.Count > 0)
        {
            yield return $"runs {string.Join(", ", read.Select(tool => string.Join(' ',
                [tool.Name!, .. (tool.Versions ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)])))}";
        }

        if (manifest.ToolsProblem is { } whole) yield return $"tool not read: {whole}";
        foreach (var tool in manifest.Tools.Where(tool => tool.Problem is not null)) yield return $"tool not read: {tool.Problem}";
    }

    /// <summary>Where an installed plugin came from, for its line in the list: never a guess where there is no record.</summary>
    private static string SourceLine(string installFolder)
    {
        var (source, problem) = PluginSource.Read(installFolder);
        if (problem is not null) return $"⚠ {problem}";
        if (source is null)
        {
            return "no record of where it came from — added before Daoris kept one, or copied in by hand; `daoris plugin add <folder>` records it";
        }

        // A package is installed whole, so `update` is not offered for one it would refuse (PLUGDIST1a).
        return source.Package is not null
            ? $"from {source.Said}"
            : $"from {source.Said}; `daoris plugin update {Path.GetFileName(installFolder)}` takes a newer one";
    }

    private static void Say(PluginWorld world, string line) => world.Instance.Out.WriteLine(line);

    /// <summary>
    /// A press as the alone-path design §1.1 takes it: its route through the instance that answers, its words said once it answers; else
    /// its operation here, refused from a session's terminal, every plugin press being the person's (D194 point 5), its refusal one line
    /// with its code (D194 point 6).
    /// </summary>
    private static async Task<int> PressAsync(PluginWorld world, PluginPress press, CancellationToken ct)
    {
        if (press.Nothing) return press.Words();
        var error = world.Instance.Error;
        async Task<int> Alone()
        {
            if (world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
            {
                await error.WriteLineAsync($"{Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
                return 1;
            }

            try
            {
                press.Act();
            }
            catch (DriverException refused)
            {
                await error.WriteLineAsync($"{Prefix}: {refused.Message} ({PluginCodes.Of(refused)})").ConfigureAwait(false);
                return 2;
            }
            catch (IOException held)
            {
                // The file's edit lock, held by another writer for the whole of its wait: nothing was written.
                await error.WriteLineAsync($"{Prefix}: {held.Message}").ConfigureAwait(false);
                return 2;
            }

            return press.Words();
        }

        if (press.Route is not { } route) return await Alone().ConfigureAwait(false);
        return await InstanceDoor.PressAsync(route, press.Payload, Alone, _ => press.Words(), world.Instance, ct).ConfigureAwait(false);
    }
}
