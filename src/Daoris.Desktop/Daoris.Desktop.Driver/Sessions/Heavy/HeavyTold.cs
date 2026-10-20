namespace Daoris.Driver;

/// <summary>
/// What a driven session is told of its repository's heavy gates (HEAVYRUN1b1, D180's HEAVYRUN1 notes): to run the checks that cover
/// what it changes while it works, and those gates once, at its end, through <c>daoris-driver run-heavy</c>, which holds the
/// repository's one heavy-run slot, so two sessions reaching their ends together run them one after the other. Said before how to
/// take the quest (<see cref="SessionTarget.Heavy"/>), on a first start, a resume and a carry-on alike.
/// </summary>
/// <remarks>
/// <para><b>Reached by its bare name.</b> Every session's PATH starts with the install's launchers wherever they are beside the home
/// (<see cref="Tools.ChildPath"/>, WSSETUP3), wiring step or none, and the session inherits the home the desktop runs under (D63),
/// which the installed host adopts from its own install where it is missing (FINDDAORIS1). So nothing new is handed: where no
/// launcher is beside the home (a workspace build), nothing promises a verb the session cannot run.</para>
///
/// <para>Project-agnostic, as the instruction is: the gates' names are the repository's own data, and no decision is named.</para>
/// </remarks>
public static class HeavyTold
{
    /// <summary>The verb a session runs, by its bare name on its PATH.</summary>
    public const string Verb = "daoris-driver";

    /// <summary>The launchers an install's <c>app/bin/</c> carries for <see cref="Verb"/>, either of which its PATH finds.</summary>
    private static readonly string[] Launchers = [Verb + ".cmd", Verb];

    /// <summary>
    /// What a session in <paramref name="root"/> is told, or null: where its PATH finds no <see cref="Verb"/> beside
    /// <paramref name="home"/>, or the repository marks no heavy gate and this machine sets none for it.
    /// </summary>
    /// <param name="root">The tree the session works in.</param>
    /// <param name="home">The driver's home, whose launchers and driver file are read.</param>
    /// <param name="repository">
    /// The repository the session is handed by name, which this machine's mapping is keyed by (HEAVYRUN1b2a); null names it by the
    /// folder its git folder is in, as a run a person starts does.
    /// </param>
    public static string? For(string root, string? home, string? repository = null)
    {
        if (home is null || !Reaches(home)) return null;
        var gates = Marked(root, home, repository);
        return gates.Count == 0 ? null : Words(gates);
    }

    /// <summary>The words, naming the gates in the order the repository declares them.</summary>
    public static string Words(IReadOnlyList<string> gates) =>
        "While you work, run the checks that cover what you change. Once, when the work is done, run this repository's heavy gates "
        + $"({string.Join(", ", gates.Select(gate => $"`{gate}`"))}) through `{Verb} run-heavy`, from this tree's root: it waits its "
        + "turn behind another session's run of this repository rather than running beside it. Never run those gates whole another way.";

    /// <summary>
    /// What a session in <paramref name="root"/> is told and handed under its run's <paramref name="checks"/> (FABRIC-PIPELINE1b, D192 point
    /// 5; the pipeline design §3.2's first and third layers), composed from the repository's declaration. Nothing set is today's words
    /// (<see cref="For(string, string?, string?)"/>). <c>heavy-at-end</c> says the heavy gates once, at the end, as the pipeline's, where the
    /// verb is found. <c>light</c> names the light gates and the heavy gates as the verification's, said whether or not the verb is found
    /// since it asks nothing of it; where the agent takes Daoris's rules, each heavy gate's command is denied exactly and the classifier is
    /// told; where it takes none, the words say so and nothing is handed.
    /// </summary>
    /// <param name="takesRules">Whether the session's agent takes Daoris's rules (<see cref="ISessionAdapter.TakesSettings"/>).</param>
    public static HeavyHanded Hand(string root, string? home, string? repository, string? checks, bool takesRules)
    {
        if (checks is null) return new HeavyHanded(For(root, home, repository), [], null);
        var (light, heavy) = Gates(root, home, repository);
        if (heavy.Count == 0) return HeavyHanded.None;
        if (checks == SessionChecks.HeavyAtEnd)
        {
            return new HeavyHanded(home is not null && Reaches(home) ? AtEndWords(Names(heavy)) : null, [], null);
        }

        var said = LightWords(Names(light), Names(heavy), takesRules);
        return takesRules ? new HeavyHanded(said, DenyRules(heavy, light), HardDeny(Names(heavy))) : new HeavyHanded(said, [], null);
    }

    /// <summary>The words under <c>light</c>: the light gates, the checks a change reaches, and the heavy gates as the verification's.</summary>
    public static string LightWords(IReadOnlyList<string> light, IReadOnlyList<string> heavy, bool takesRules) =>
        "This repository's development pipeline is light. While you work, run "
        + (light.Count > 0 ? $"its light gates ({Ticked(light)}) and " : "")
        + "the checks your change reaches, filtered: a test class, a test file, a census. "
        + $"Its heavy gates ({Ticked(heavy)}) are not this session's: they run in the repository's verification after the work lands. "
        + $"Never run them, whole or another way: `{Verb} run-heavy` refuses this tree"
        + (takesRules
            ? ", and your harness refuses each one's declared command."
            : ". Your agent takes no rules from Daoris, so nothing refuses their commands in your shell: they are said to you instead.");

    /// <summary>The words under <c>heavy-at-end</c>: today's, said as the pipeline's.</summary>
    public static string AtEndWords(IReadOnlyList<string> heavy) =>
        "This repository's development pipeline runs its heavy gates once, at a session's end. While you work, run the checks that cover "
        + $"what you change. Once, when the work is done, run its heavy gates ({Ticked(heavy)}) through `{Verb} run-heavy`, from this tree's "
        + "root: it waits its turn behind another session's run of this repository rather than running beside it. Never run those gates "
        + "whole another way.";

    /// <summary>
    /// What auto mode's classifier is told under <c>light</c> (UNBLOCK4's channel): the heavy gates by name as the verification's, and that
    /// a filtered check is none of them, so a check <c>light</c> allows is never taken for one.
    /// </summary>
    public static string HardDeny(IReadOnlyList<string> heavy) =>
        $"Running this repository's heavy gates ({Ticked(heavy)}) whole, in this session: under its light development pipeline they run in "
        + "its verification after the work lands. A check filtered to a test class, a test file or a census is not one of them.";

    /// <summary>
    /// 🔴 Each heavy gate's declared command as a deny rule, exactly, never as a prefix (the pipeline design §3.2's third layer): a gate's
    /// command can begin a filtered check <c>light</c> allows (<c>dotnet test src/Service</c> before its <c>--filter</c>), and a rule with
    /// no wildcard matches its command as written alone. A command a light gate also runs is never denied, and one a rule cannot name
    /// exactly (a <c>*</c>, which the harness reads as any text, or a line break) is left to the words and the classifier: HEAVYRUN1's
    /// rejection of guessing a heavy run from a command's text stands.
    /// </summary>
    public static IReadOnlyList<string> DenyRules(IReadOnlyList<HeavyGate> heavy, IReadOnlyList<HeavyGate> light)
    {
        var allowed = light.Select(gate => gate.Run.Trim()).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. heavy.Select(gate => gate.Run.Trim())
                .Where(run => run.Length > 0 && !allowed.Contains(run) && run.IndexOfAny(['*', '\r', '\n']) < 0)
                .Distinct(StringComparer.Ordinal)
                .Select(run => $"Bash({run})"),
        ];
    }

    /// <summary>Whether a session's PATH finds the verb: an install's launcher for it beside the home.</summary>
    private static bool Reaches(string home) =>
        Tools.InstallBin(home) is { } bin && Launchers.Any(name => File.Exists(Path.Combine(bin, name)));

    /// <summary>
    /// The heavy gates <c>run-heavy</c> would run there, in its order (<see cref="HeavyGates.Resolve"/>): those the repository's own file
    /// marks, else this machine's mapping for it (HEAVYRUN1b2a); none where it has neither, or a file or a driver file <c>run-heavy</c>
    /// would refuse, whose session runs its gates as it would.
    /// </summary>
    private static IReadOnlyList<string> Marked(string root, string home, string? repository) => Names(Gates(root, home, repository).Heavy);

    /// <summary>
    /// The light gates and the heavy ones, each in declared order: the heavy those <c>run-heavy</c> would run (<see cref="Marked"/>), the
    /// light every gate the repository's own file declares that is not among them. None of either where the file or the driver file does
    /// not read, whose session runs its gates as it would.
    /// </summary>
    private static (IReadOnlyList<HeavyGate> Light, IReadOnlyList<HeavyGate> Heavy) Gates(string root, string? home, string? repository)
    {
        var named = repository?.Trim() is { Length: > 0 } handed ? handed : HeavySlot.RepositoryOf(root) ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        try
        {
            var declared = HeavyGates.Resolve(
                root, named, () => home is null ? null : HeavyMapping.For(DriverConfig.Load(DriverConfig.ResolvePath(home)), named));
            var heavy = HeavyGates.Choose(declared, null).Gates;
            var own = declared.Mapped ? File.Exists(Path.Combine(root, HeavyGates.FileName)) ? HeavyGates.Read(root) : [] : declared.Gates;
            var names = heavy.Select(gate => gate.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return ([.. own.Where(gate => !gate.Heavy && !names.Contains(gate.Name))], heavy);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return ([], []);
        }
    }

    private static IReadOnlyList<string> Names(IReadOnlyList<HeavyGate> gates) => [.. gates.Select(gate => gate.Name)];

    private static string Ticked(IReadOnlyList<string> names) => string.Join(", ", names.Select(name => $"`{name}`"));
}

/// <summary>
/// What a session is told and handed under its run's checks (FABRIC-PIPELINE1b): its words, said before how to take the quest; the deny
/// rules beside <c>no-push</c>; and the sentence auto mode's classifier is told. Rules only where its agent takes Daoris's rules.
/// </summary>
/// <param name="Words">What it is told, or null for nothing.</param>
/// <param name="Deny">Each heavy gate's declared command, exactly, as a harness deny rule.</param>
/// <param name="HardDeny">What the classifier is told, or null.</param>
public sealed record HeavyHanded(string? Words, IReadOnlyList<string> Deny, string? HardDeny)
{
    /// <summary>Nothing told, nothing handed.</summary>
    public static HeavyHanded None { get; } = new(null, [], null);
}
