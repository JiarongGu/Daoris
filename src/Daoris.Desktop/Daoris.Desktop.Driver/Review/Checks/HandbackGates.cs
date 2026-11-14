namespace Daoris.Driver;

/// <summary>
/// The gates a repository requires before its work is handed back, declared on the gate in <c>daoris.gates.json</c> as <c>"handback"</c>:
/// <c>true</c> requires its <c>run</c>, a string requires that command in its place (the gate's own run judged against a base the
/// session's tree names). The brief lists them for the agent to run itself; a check step whose choice is <c>reached</c> verifies them at the tip
/// while the session is open, and runs only what has no green verdict there.
/// </summary>
internal static class HandbackGates
{
    /// <summary>The required gates among <paramref name="gates"/>, each with the command it is required by.</summary>
    internal static IReadOnlyList<DeclaredGate> Of(IReadOnlyList<DeclaredGate> gates) => [.. gates.Where(gate => gate.Handback).Select(ForHandback)];

    internal static DeclaredGate ForHandback(DeclaredGate gate) => gate with { Run = gate.HandbackRun ?? gate.Run };

    /// <summary>The required gates of the declaration in <paramref name="root"/>, none where it declares none.</summary>
    internal static IReadOnlyList<DeclaredGate> OfFolder(string root)
    {
        var declared = Path.Combine(root, DeclarationNames.Gates);
        return Of(DeclaredGates.Read(File.Exists(declared) ? File.ReadAllText(declared) : null));
    }

    /// <summary>The brief's words naming the required commands, null where the declaration requires none.</summary>
    internal static string? Told(string root)
    {
        var gates = OfFolder(root);
        return gates.Count == 0 ? null
            : "Before done or ready, run every required hand-back check from this tree, and fix what your change reddens: "
              + string.Join("; ", gates.Select(gate => $"`{gate.Run}`" + (gate.Cwd is { } cwd ? $" in `{cwd}`" : "")))
              + ". All must finish green. These commands select the checks the change reaches; a list of classes you chose does not replace them. "
              + "Use `check_run` to keep a verdict at your tip; the workflow's check step verifies the same commands at your tip while you are open, "
              + "and runs again only what has no green verdict there.";
    }

    /// <summary>
    /// Whether <paramref name="ask"/> asked what <paramref name="gate"/> requires: its whole command, or its name where the gate's own run is the
    /// requirement. A gate required by another command than its run is never met by a verdict of its run.
    /// </summary>
    internal static bool Matches(DeclaredGate gate, CheckAsk ask) =>
        ask.Command is { } command
            ? gate.Cwd is null && HeavyRun.CommandText(command) == ForHandback(gate).Run
            : ask.Gate == gate.Name && gate.HandbackRun is null && gate.Cwd is null;

    /// <summary>Whether a check of the home's broker finished green at <paramref name="commit"/> for <paramref name="gate"/>: nothing runs it again.</summary>
    internal static bool Verified(CheckFiles files, DeclaredGate gate, string commit) =>
        files.Asks().Any(ask => Matches(gate, ask) && files.ReadState(ask.Id) is { State: CheckStates.Reached, Tip: { } tip }
            && string.Equals(tip, commit, StringComparison.OrdinalIgnoreCase));
}
