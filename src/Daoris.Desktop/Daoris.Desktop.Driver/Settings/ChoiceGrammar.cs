namespace Daoris.Driver;

/// <summary>
/// Which words of <c>daoris-driver driver</c> change the home (PUSH1f2b; FOUNDATION1 mod-iii): the grammar the settings' command and the
/// lease a change holds (<see cref="VerbLeases"/>) read before any verb is planned, kept with the settings so that it names no verb a
/// subdomain above owns. A verb's own reading of the same words (<see cref="ChoiceVerb"/>) answers through these.
/// </summary>
public static class ChoiceGrammar
{
    /// <summary>The sub-verbs of <c>workflow</c> that write a workflow or the choice; with <c>--plan</c> each only says what it would write.</summary>
    public static IReadOnlySet<string> WorkflowWrites { get; } = new HashSet<string>(StringComparer.Ordinal) { "new", "edit", "apply", "import", "use", "kind" };

    /// <summary>Whether the words (after <c>workflow</c>) write: a writing sub-verb with no <c>--plan</c>.</summary>
    public static bool WorkflowChanges(IReadOnlyList<string> args) =>
        args.Count > 0 && WorkflowWrites.Contains(args[0]) && !args.Contains("--plan");
}
