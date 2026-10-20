namespace Daoris.Driver;

/// <summary>
/// What a plugin's answer may establish before it moves a node (FABRIC-PERFORM1c; the fabric events design §C4's five belief rules, §D4's
/// binding, §D6's outputs, §E2's authority by kind). Pure: the performer judges each completion here before the one table sees it, and
/// each start's inputs again at dispatch.
/// </summary>
/// <remarks>
/// <para><b>A completion is believed only where all five hold</b>, judged on the attempt its invocation names: (1) its sender is the attempt's
/// plugin's current process, never what the frame says; (2) it names the attempt's step, the node is still the sender's to reach (its kind
/// that plugin's, its plugin still declaring it, the governing cycle still including it), and the kind is one reached on the plugin's word
/// (<see cref="NodeAuthority.OnPluginWord"/>); (3) its unit is one Daoris tells about, and a repository or branch the frame names is that
/// unit's; (4) a commit the frame names is the attempt's revision; (5) its time is no more than a minute ahead. Anything else is no answer,
/// counted by its code and never applied.</para>
/// <para><b>What the plugin's word establishes</b> is its own node's outcome, bound to the work by its attempt (§D4): the reach's evidence is
/// the plugin's reference at the schema the node declares, kept as its word (D143), never a deployment Daoris has proved.</para>
/// </remarks>
public static class WorkBelief
{
    /// <summary>A completion judged against the attempt its invocation names: null where it is believed, else the code it is not taken with.</summary>
    /// <param name="current">The generation of the attempt's plugin's process now, or null where none runs.</param>
    /// <param name="node">The attempt's node as the cycle governing its unit declares it now, under its plugin's step; null where neither does.</param>
    /// <param name="unit">The unit as Daoris tells its plugins about it now, or null where it is told to none.</param>
    public static string? Completion(WorkStepHeard heard, NodeAttempt attempt, int? current, NodeDeclaration? node, WatchedUnit? unit, DateTimeOffset now)
    {
        var frame = heard.Step;
        if (!string.Equals(heard.Plugin, attempt.Plugin, StringComparison.Ordinal) || current != heard.Generation) return WorkRefusals.NotSender;
        if (!Names(frame.Step, heard.Plugin, attempt.Call.Step)) return WorkRefusals.NotNamed;
        if (node is null) return WorkRefusals.NotDeclared;
        if (!node.Kind.StartsWith(attempt.Plugin + "/", StringComparison.Ordinal) || NodeAuthorities.Of(node) is not { OnPluginWord: true })
        {
            return WorkRefusals.NotNamed;
        }

        if (unit is null || Other(frame.Repository, unit.Repository) || Other(frame.Branch, unit.Branch)) return WorkRefusals.NotWatched;
        if (Other(frame.Commit, attempt.Call.SubjectRevision)) return WorkRefusals.Stale;
        return frame.At > now + WorkBounds.Minute ? WorkRefusals.Shape : null;
    }

    /// <summary>
    /// Whether the work has moved past the revision the attempt was dispatched on (§C4's fourth rule, §D4's lapse): its outcome, though its
    /// own, is a stale attempt's, evidence for no current binding.
    /// </summary>
    public static bool Lapsed(NodeAttempt attempt, WatchedUnit unit) => !string.Equals(attempt.Call.SubjectRevision, unit.Tip, StringComparison.Ordinal);

    /// <summary>
    /// The input a start's binding names that the record no longer holds as its producer's current acceptance exposes it, or null (§D6: at
    /// dispatch each input resolves to the ref on the producing node's current, valid acceptance; a stale or invalidated producer leaves the
    /// consumer ineligible, never given an old ref). A producer is on the consumer's record (a <c>data</c> wire names a node of its own graph),
    /// so its acceptance is current where it is reached and is the acceptance the port names; on the consumer's subject, at the consumer's
    /// revision. On another subject (FABRIC-CUTOVER1a: a run's quest-side node feeding a branch-side one, whose revisions differ by design, a
    /// quest's moment and a branch's tip) it is judged on its own, which the evaluation did in keeping it reached.
    /// </summary>
    public static string? Unresolved(NodeRecord record, NodeAcceptance binding)
    {
        foreach (var (name, port) in binding.Inputs)
        {
            var producer = port.Acceptance is { } named ? record.Statuses.Values.FirstOrDefault(status => status.Acceptance?.Id == named) : null;
            if (producer is not { State: NodeStates.Reached, Acceptance: { } accepted }
                || (string.Equals(accepted.Observes, binding.Observes, StringComparison.Ordinal)
                    && !string.Equals(accepted.SubjectRevision, binding.SubjectRevision, StringComparison.Ordinal))
                || !accepted.Outputs.Values.Append(accepted.Evidence).Any(exposed => exposed == port))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// The reach's evidence (§D4, §E2's ports): the plugin's reference at the schema it names (<c>&lt;schema&gt;:&lt;id&gt;</c>), so the one
    /// table refuses one of another schema than the node declares (<see cref="NodeRefusal.NoEvidence"/>), and none where the node declares a
    /// schema the plugin gave no reference of. A node declaring none takes the answer itself, its invocation, as the plugin's word.
    /// </summary>
    public static NodePort? Evidence(NodeDeclaration node, string invocation, string? revision, WorkEvidence? evidence) =>
        evidence?.Ref is { } reference ? new NodePort(SchemaOf(reference) ?? node.Kind, reference, revision, null)
        : node.Evidence is null ? new NodePort(node.Kind, invocation, revision, null)
        : null;

    /// <summary>The schema a reference names (§D6's <c>&lt;schema&gt;:&lt;opaque id&gt;</c>), or null where it names none.</summary>
    public static string? SchemaOf(string reference) => reference.IndexOf(':') is > 0 and var colon && colon < reference.Length - 1 ? reference[..colon] : null;

    /// <summary>The frame's step is the attempt's: its plugin's own id, or <c>&lt;plugin&gt;/&lt;id&gt;</c> (PERFORM1a's wire).</summary>
    private static bool Names(string step, string plugin, string kind) =>
        string.Equals(step, kind, StringComparison.Ordinal) || string.Equals(plugin + "/" + step, kind, StringComparison.Ordinal);

    /// <summary>A part the frame names that is not the one Daoris holds: a part it leaves out is bound by its invocation.</summary>
    private static bool Other(string? said, string held) => said is not null && !string.Equals(said, held, StringComparison.Ordinal);
}
