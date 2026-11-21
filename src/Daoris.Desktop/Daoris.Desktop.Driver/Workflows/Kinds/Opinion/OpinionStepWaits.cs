using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What the <c>opinion</c> step says while it stands on someone: with its findings handed to the work's session it waits on that
/// session's answer (<see cref="Answer"/>, naming it), the agent's wait and never <i>working</i>; read and answered but holding the work
/// for the person (<see cref="Disputed"/>, <see cref="CommitsSince"/>), it waits on their answer, said with its count; and a pass whose
/// reviewer ended without giving an opinion, or whose minutes ran out first, fails it, saying so, until another pass reaches it.
/// </summary>
public static class OpinionStepWaits
{
    /// <summary>The opinion's declared wait: its findings are with the work's session, which answers them.</summary>
    public const string Answer = "answer";

    /// <summary>
    /// The opinion's wait where its gate is disputed: a <c>must</c> the working session did not fix and no recheck withdrew, or a recheck's
    /// own, waits on the person's answer (<c>count</c>: the disputed findings).
    /// </summary>
    public const string Disputed = "disputed";

    /// <summary>
    /// The opinion's wait where commits after the read tip wait on the person, the one pass and the one recheck spent (<c>count</c>:
    /// the commits no other agent read).
    /// </summary>
    public const string CommitsSince = "commits-since";

    /// <summary>Every wait the opinion kind declares.</summary>
    public static IReadOnlyList<string> Codes { get; } = [Answer, Disputed, CommitsSince];

    /// <summary>A failed pass's code (the gate's <see cref="OpinionGateState.Code"/>): its reviewer's session ended without giving one.</summary>
    public const string Ended = "ended";

    /// <summary>A failed pass's code: its reviewer's minutes ran out before it gave one.</summary>
    public const string OutOfTime = "out-of-time";

    /// <summary>The words a step a pass left with no opinion fails with; null for every other code, a wait for a reviewer among them.</summary>
    public static string? EndedSaid(string? code) => code switch
    {
        Ended => "Its reviewer's session ended without giving an opinion, so no agent's reading stands for this work: ask again, or pass it over.",
        OutOfTime => "Its reviewer's minutes ran out before it gave an opinion, so no agent's reading stands for this work: ask again, or pass it over.",
        _ => null,
    };

    /// <summary>
    /// The wait of an active <c>opinion</c>: with the work's session (<see cref="OpinionGateStates.WithSession"/>), the session the
    /// findings went to, as the gate read it; disputed or with commits since nobody read, the person's answer, with its count and the
    /// session whose work it is; null for any other state, and where no gate is read.
    /// </summary>
    /// <remarks>A dispute the policy gates on a role names the step's own role (<see cref="StepRoles.Of"/>), the one its wait is held on, never the row's <c>by</c>.</remarks>
    public static WorkflowRunWait? WaitOf(NodeDeclaration node, NodeStatus status, OpinionGateState? gate)
    {
        if (status.State != NodeStates.Active) return null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (status.Holds == OpinionGateStates.WithSession)
        {
            var session = gate?.Opinion?.HandedTo ?? gate?.Opinion?.Working ?? gate?.Session;
            if (session is { Length: > 0 }) values["session"] = session;
            return new WorkflowRunWait(Answer, values, status.Since);
        }

        // A gate that holds for the person's answer is said as their wait, never a silent `working`.
        var (code, count) = (status.Holds, gate) switch
        {
            (OpinionGateStates.Disputed or OpinionGateReading.RoleDisputed, { State: OpinionGateStates.Disputed, Disputes: { } disputes }) => (Disputed, disputes.Count),
            (OpinionGateStates.CommitsSince or OpinionGateReading.RoleCommitsSince, { State: OpinionGateStates.CommitsSince, Since: { } since }) => (CommitsSince, since),
            _ => ((string?)null, 0),
        };
        if (code is null) return null;
        values["count"] = count.ToString(CultureInfo.InvariantCulture);
        if (status.Holds is OpinionGateReading.RoleDisputed or OpinionGateReading.RoleCommitsSince && gate!.Risk is { } verdict)
        {
            values[RoleKey] = StepRoles.Of(node);
            values["why"] = $"{verdict.Reason} (rule {verdict.Source} at {verdict.Revision})";
        }

        var working = gate!.Session ?? gate.Opinion?.Working;
        if (working is { Length: > 0 }) values["session"] = working;
        return new WorkflowRunWait(code, values, status.Since);
    }

    /// <summary>The wait's value naming the role the risk policy gates a dispute on.</summary>
    public const string RoleKey = "role";

    /// <summary>Whether <paramref name="wait"/> is the answer of the role the risk policy gates the dispute on.</summary>
    public static bool OnRole(WorkflowRunWait wait) => wait.Code is Disputed or CommitsSince && wait.Values.ContainsKey(RoleKey);

    /// <summary>Whether <paramref name="wait"/> is the person's answer to the gate, never an agent's.</summary>
    public static bool OnPerson(WorkflowRunWait wait) => wait.Code is Disputed or CommitsSince && !OnRole(wait);

    /// <summary>The terminal's words for the wait: on whose answer, and for a person's wait the doors that answer it.</summary>
    public static string Said(WorkflowRunWait wait)
    {
        var session = wait.Values.GetValueOrDefault("session") is { Length: > 0 } named ? named : "<session>";
        var count = int.TryParse(wait.Values.GetValueOrDefault("count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var read) ? read : 0;
        return wait.Code switch
        {
            Disputed or CommitsSince when OnRole(wait) =>
                $"Waits on the role's answer to {(wait.Code == Disputed ? Plural(count, "disputed finding") : Plural(count, "commit") + " no other agent read")} of the second "
                + $"opinion: `daoris session opinion show {session}` lists them. The risk policy asks the `{wait.Values[RoleKey]}` role, not you, for the answer: {wait.Values.GetValueOrDefault("why")}.",
            Disputed => $"Waits on your answer to {Plural(count, "disputed finding")} of the second opinion: `daoris session opinion show {session}` "
                        + $"lists them; your Accept answers them, `daoris session opinion anyway {session} \"…\"` lands it over them, or "
                        + $"`daoris session say {session} \"…\"` sends it back.",
            CommitsSince => $"Waits on your answer to {Plural(count, "commit")} no other agent read, its one pass and one recheck spent: your Accept "
                            + $"answers them, `daoris session opinion anyway {session} \"…\"` lands them, or `daoris session opinion ask {session}` asks again.",
            _ => session != "<session>"
                ? $"Waits on `{session}`'s answer to the second opinion."
                : "Waits on the work's session's answer to the second opinion.",
        };
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
