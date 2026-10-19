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
    /// <paramref name="home"/>, or the repository marks no heavy gate.
    /// </summary>
    public static string? For(string root, string? home)
    {
        if (!Reaches(home)) return null;
        var gates = Marked(root);
        return gates.Count == 0 ? null : Words(gates);
    }

    /// <summary>The words, naming the gates in the order the repository declares them.</summary>
    public static string Words(IReadOnlyList<string> gates) =>
        "While you work, run the checks that cover what you change. Once, when the work is done, run this repository's heavy gates "
        + $"({string.Join(", ", gates.Select(gate => $"`{gate}`"))}) through `{Verb} run-heavy`, from this tree's root: it waits its "
        + "turn behind another session's run of this repository rather than running beside it. Never run those gates whole another way.";

    /// <summary>Whether a session's PATH finds the verb: an install's launcher for it beside the home.</summary>
    private static bool Reaches(string? home) =>
        home is not null && Tools.InstallBin(home) is { } bin && Launchers.Any(name => File.Exists(Path.Combine(bin, name)));

    /// <summary>
    /// The heavy gates the repository's own file marks, in its order; none where it has no file or one <c>run-heavy</c> would refuse,
    /// whose session runs its gates as it would. HEAVYRUN1b2 adds the machine-local mapping here, for a repository that marks none.
    /// </summary>
    private static IReadOnlyList<string> Marked(string root)
    {
        if (!File.Exists(Path.Combine(root, HeavyGates.FileName))) return [];
        try
        {
            return [.. HeavyGates.Read(root).Where(gate => gate.Heavy).Select(gate => gate.Name)];
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
