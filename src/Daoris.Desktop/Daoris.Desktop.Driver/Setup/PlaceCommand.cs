namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver place &lt;repository&gt;… --preset &lt;id&gt; [--with opinion,look,adoption] [--what-it-builds &lt;kind&gt;] [--drive] (--plan | --apply
/// [--accept &lt;repository&gt;:landed|integration,…])</c> (AGENTSPLACE1a2, AGENTSPLACE1b3; the Agents-place design §4): <c>PLACE_PLAN</c>'s and
/// <c>PLACE_APPLY</c>'s terminal twin, the same plan and the same apply from the same code in English. Through the running Daoris where one answers,
/// alone otherwise. <c>--plan</c> reads and writes nothing. <c>--apply</c> is the person's press: through a running Daoris it waits for their Confirm on
/// the window's card, once, and runs only then; alone it is the person's own terminal acting as the window's press would.
/// </summary>
/// <remarks>
/// <para>Exit codes keep the family contract: 0 a plan nothing refuses, or an apply that is done · 1 refused, or an apply not done · 2 the usage.</para>
/// <para><b>Acceptances are chosen word by word.</b> <c>--accept engine:landed</c> and <c>--accept engine:integration</c> are two choices, each for its own
/// purpose, and neither implies the other; an apply names none unless the person does. The digest is not a word: an apply plans again now, prints the
/// plan it applies, and the window's card pins that plan between its ask and its Confirm (<c>PipeGrounds</c>), refusing a plan that moved.</para>
/// </remarks>
public static class PlaceCommand
{
    public const string Usage =
        "usage: daoris-driver place <repository>… --preset <id> [--with opinion,look,adoption] [--what-it-builds <kind>] [--drive] "
        + "(--plan | --apply [--accept <repository>:landed|integration,…])";

    private const string PlanFlag = "--plan", ApplyFlag = "--apply", AcceptFlag = "--accept";

    private static readonly string[] Riders = ["opinion", "look", "adoption"];

    /// <summary>What the words ask, or what is wrong with them; the preset and what it builds are the plan's to judge.</summary>
    public static (PlaceAsk? Ask, string? Problem) Read(IReadOnlyList<string> args)
    {
        var (words, problem) = Parse(args);
        return (words?.Ask, problem);
    }

    /// <summary>What the words ask and whether they plan or apply it, with the acceptances chosen; or what is wrong with them.</summary>
    public static (PlaceWords? Words, string? Problem) Parse(IReadOnlyList<string> args)
    {
        var names = new List<string>();
        var with = new List<string>();
        var accept = new List<PlaceAccept>();
        string? preset = null, builds = null;
        bool plan = false, apply = false, drive = false, accepting = false;
        for (var at = 0; at < args.Count; at++)
        {
            var arg = args[at];
            string? Value() => at + 1 < args.Count && !args[at + 1].StartsWith('-') ? args[++at] : null;
            switch (arg)
            {
                case PlanFlag:
                    plan = true;
                    break;
                case ApplyFlag:
                    apply = true;
                    break;
                case AcceptFlag:
                    accepting = true;
                    if (Value() is not { } chosen) return (null, AcceptProblem);
                    foreach (var part in chosen.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        var colon = part.LastIndexOf(':');
                        if (colon <= 0 || part[(colon + 1)..] is not (PlacePlans.Landed or PlacePlans.Integration))
                        {
                            return (null, $"`{part}` is not an acceptance. {AcceptProblem}");
                        }

                        accept.Add(new PlaceAccept(part[..colon], part[(colon + 1)..]));
                    }

                    break;
                case "--drive":
                    drive = true;
                    break;
                case "--preset":
                    if ((preset = Value()) is null) return (null, $"`--preset` names a delivery: {Presets()}.");
                    break;
                case SetupCommand.BuildsFlag:
                    if ((builds = Value()) is null)
                    {
                        return (null, $"`{SetupCommand.BuildsFlag}` names what the repository builds: {string.Join(", ", SetupBuilds.All.Select(each => $"`{each}`"))}.");
                    }

                    break;
                case "--with":
                    if (Value() is not { } list) return (null, "`--with` names what rides the delivery: `opinion`, `look` or `adoption`, joined by commas.");
                    foreach (var part in list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!Riders.Contains(part, StringComparer.Ordinal)) return (null, $"`{part}` is not something `--with` takes — `opinion`, `look` or `adoption`.");
                        with.Add(part);
                    }

                    break;
                default:
                    if (arg.StartsWith('-')) return (null, $"`{arg}` is not a word place takes.");
                    names.Add(arg);
                    break;
            }
        }

        if (names.Count == 0) return (null, "name the repositories the plan is for.");
        if (preset is null) return (null, $"name the delivery with `--preset`: {Presets()}.");
        if (plan == apply) return (null, plan ? "`--plan` and `--apply` are two things: read the plan, or apply it." : "name `--plan` to read the plan, or `--apply` to apply it.");
        if (accepting && !apply) return (null, "`--accept` chooses acceptances for an apply, so it goes with `--apply`.");
        return (new PlaceWords(new PlaceAsk(names, preset)
        {
            Opinion = with.Contains("opinion"),
            Look = with.Contains("look"),
            Adoption = with.Contains("adoption"),
            Builds = builds,
            Drive = drive,
        }, apply, accept), null);
    }

    private static string AcceptProblem =>
        $"`{AcceptFlag}` names the acceptances an apply makes, each for its own purpose: `<repository>:{PlacePlans.Landed}` or `<repository>:{PlacePlans.Integration}`, joined by commas.";

    /// <summary>The words that ask <paramref name="ask"/>, the repositories first, as a route hands them back to the verb.</summary>
    public static IReadOnlyList<string> ArgsOf(PlaceAsk ask) => [.. AskWords(ask), PlanFlag];

    /// <summary>The words that apply <paramref name="ask"/> with <paramref name="accept"/> chosen, as a route hands them back to the verb.</summary>
    public static IReadOnlyList<string> ApplyArgsOf(PlaceAsk ask, IReadOnlyList<PlaceAccept> accept) =>
        [.. AskWords(ask), ApplyFlag, .. accept.Count == 0 ? Array.Empty<string>() : [AcceptFlag, string.Join(',', accept.Select(each => $"{each.Repository}:{each.Purpose}"))]];

    private static List<string> AskWords(PlaceAsk ask)
    {
        var words = new List<string>(ask.Repositories) { "--preset", ask.Preset };
        var with = Riders.Where(rider => rider switch { "opinion" => ask.Opinion, "look" => ask.Look, _ => ask.Adoption }).ToList();
        if (with.Count > 0) words.AddRange(["--with", string.Join(',', with)]);
        if (ask.Builds is { } builds) words.AddRange([SetupCommand.BuildsFlag, builds]);
        if (ask.Drive) words.Add("--drive");
        return words;
    }

    /// <summary>
    /// The verb: the usage said first, sending nothing; then the plan, read through the running Daoris where it answers, or the apply, which the
    /// running Daoris asks the person to confirm and which alone runs over <paramref name="apply"/>'s doors.
    /// </summary>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args, TextWriter output, IPlaceWorld world, DriverConfig config, string home, IWorkplacePluginCatalog plugins,
        SessionWire door, InstanceWorld? instance, PlaceApplyDoors? apply = null, CancellationToken ct = default)
    {
        var (words, problem) = Parse(args);
        if (words is null)
        {
            output.WriteLine($"place: {problem}");
            output.WriteLine(Usage);
            return 2;
        }

        var ask = words.Ask;
        if (words.Apply)
        {
            return await InstanceDoor.ThroughAsync(
                    instance, PersonPresses.PlaceApply,
                    InstanceDoor.Said(
                        ("repositories", ask.Repositories), ("preset", ask.Preset), ("opinion", ask.Opinion), ("look", ask.Look), ("adoption", ask.Adoption),
                        ("builds", ask.Builds), ("drive", ask.Drive),
                        ("accept", words.Accept.Select(each => new { repository = each.Repository, purpose = each.Purpose }).ToArray())),
                    () => ApplyAloneAsync(words, output, apply, ct), output, ct)
                .ConfigureAwait(false);
        }

        return await InstanceDoor.ThroughAsync(
                instance, OpenRoutes.PlacePlan,
                InstanceDoor.Said(
                    ("repositories", ask.Repositories), ("preset", ask.Preset), ("opinion", ask.Opinion), ("look", ask.Look), ("adoption", ask.Adoption),
                    ("builds", ask.Builds), ("drive", ask.Drive)),
                async () => Say(await PlacePlans.PlanAsync(world, config, home, plugins, door, ask, ct).ConfigureAwait(false), output), output, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The apply, alone (or as the running Daoris runs it for a terminal): the plan printed as <c>--plan</c> prints it, then the apply of that plan
    /// by its digest with the acceptances the words chose, then the outcome in a sentence. Exit 0 where every operation is done.
    /// </summary>
    private static async Task<int> ApplyAloneAsync(PlaceWords words, TextWriter output, PlaceApplyDoors? doors, CancellationToken ct)
    {
        if (doors is null)
        {
            output.WriteLine("place: applying needs the driver's home and its service, which this run was not given.");
            return 2;
        }

        var plan = await PlacePlans.PlanAsync(doors.Place, doors.Config(), doors.Home, doors.Plugins, doors.Door, words.Ask, ct).ConfigureAwait(false);
        foreach (var line in Lines(plan, applying: true)) output.WriteLine(line);
        var result = await PlaceApplies.ApplyAsync(doors, words.Ask, plan.Digest, words.Accept, ct).ConfigureAwait(false);
        output.WriteLine($"place: {result.Message}");
        foreach (var operation in result.Operations.Where(each => each.Quest is { Length: > 0 })) output.WriteLine($"  quest  #{operation.Quest} for `{operation.Repository}`");
        return result.Done ? 0 : 1;
    }

    /// <summary>A plan's lines, written; its exit code.</summary>
    public static int Say(PlacePlanned plan, TextWriter output)
    {
        foreach (var line in Lines(plan)) output.WriteLine(line);
        return plan.Applies ? 0 : 1;
    }

    /// <summary>A plan in the terminal's words: each repository now and after, the changes each said once, then what the person reads before applying.</summary>
    public static IReadOnlyList<string> Lines(PlacePlanned plan, bool applying = false)
    {
        var ask = plan.Asked;
        var lines = new List<string>();
        var delivery = ask.Preset == SetupPresets.Keep ? SetupPresets.KeepName : SetupPresets.Of(ask.Preset)?.Name ?? $"`{ask.Preset}`";
        var riders = new[] { ask.Opinion ? "a second opinion" : null, ask.Look ? "your look" : null }.OfType<string>().ToList();
        lines.Add($"place: {Names(ask.Repositories)} — {delivery}{(riders.Count == 0 ? "" : $", with {string.Join(" and ", riders)}")}"
            + (ask.Adoption ? $"; adoption{(ask.Builds is { } builds && SetupBuilds.Is(builds) ? $", for {SetupBuilds.Named(builds)}" : "")}" : "")
            + (ask.Drive ? "; general driving" : ""));
        foreach (var row in plan.Repositories)
        {
            lines.Add($"  `{row.Repository}`: now {Matched(row.Before)} · after {Matched(row.After)} · suggested "
                + $"{SetupPresets.Of(row.Suggested.Preset)!.Name}{(row.Suggested.Adoption ? ", with adoption" : "")}");
        }

        lines.Add(plan.Changes.Count == 0 ? "changes: none, each repository already reads as asked." : "changes, each said once:");
        lines.AddRange(plan.Changes.Select(each => $"  {Names(each.Repositories)}: {each.Change.Said}, {each.Change.Before} → {each.Change.After}"));
        Section(lines, "lowers your part:", plan.Repositories.SelectMany(row => row.Lowers.Select(said => $"`{row.Repository}`: {said}")));
        Section(lines, "reach:", plan.Repositories.SelectMany(row => row.Reach.Select(each => each.Said).OfType<string>()));
        Section(lines, "released when driving starts:", plan.Repositories.Where(row => row.Released.Count > 0)
            .Select(row => $"`{row.Repository}`: {string.Join("; ", row.Released.Select(quest => $"#{quest.Quest} {quest.Title}"))}"));
        var declarations = new List<string>();
        foreach (var row in plan.Repositories)
        {
            if (row.Declaration is not { } declaration)
            {
                if (row.Unread is { } unread) declarations.Add($"`{row.Repository}`: its line was not read — {unread}");
                continue;
            }

            if (declaration.Blob == WorkflowSelection.NoGates && declaration.Acceptances.Count == 0) continue;
            declarations.Add($"`{row.Repository}`: `{LandedGates.File}` at `{declaration.Line}` `{TextBounds.ShortCommit(declaration.Commit, 12)}`, "
                + (declaration.Blob == WorkflowSelection.NoGates ? "none held there" : $"blob `{TextBounds.ShortCommit(declaration.Blob, 12)}`"));
            declarations.AddRange(declaration.Acceptances.Select(acceptance => $"  {acceptance.Said} It is {acceptance.Standing} now."));
            if (declaration.Text is { } text) declarations.AddRange(text.TrimEnd().ReplaceLineEndings("\n").Split('\n').Select(line => $"    {line}"));
        }

        Section(lines, "declarations:", declarations);
        if (plan.Refusals.Count > 0)
        {
            lines.Add("refused:");
            lines.AddRange(plan.Refusals.Select(refusal => $"  - {refusal.Sentence}"));
        }

        if (!applying) lines.Add("--plan: nothing was written, and nothing was asked.");
        return lines;
    }

    private static void Section(List<string> lines, string heading, IEnumerable<string> said)
    {
        var each = said.ToList();
        if (each.Count == 0) return;
        lines.Add(heading);
        lines.AddRange(each.Select(line => $"  {line}"));
    }

    private static string Matched(PlaceSide side)
    {
        var own = side.Match.Own;
        var mine = own.Count == 0 ? "" : $"{own.Count} change{(own.Count == 1 ? "" : "s")} of your own ({string.Join(", ", own)})";
        var named = side.Match.Preset is { } preset ? SetupPresets.Of(preset)!.Name + (mine.Length > 0 ? $", {mine}" : "") : mine;
        return side.Held ? $"{named}, held" : named;
    }

    private static string Names(IEnumerable<string> names) => string.Join(", ", names.Select(name => $"`{name}`"));

    private static string Presets() => string.Join(", ", SetupPresets.Asks.Select(each => $"`{each}`"));
}

/// <summary>What <c>place</c>'s words ask: the plan's ask, whether to apply it, and the acceptances chosen for an apply.</summary>
public sealed record PlaceWords(PlaceAsk Ask, bool Apply, IReadOnlyList<PlaceAccept> Accept);
