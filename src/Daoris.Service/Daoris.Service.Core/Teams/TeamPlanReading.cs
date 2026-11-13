using System.Text;

namespace Daoris.Knowledge;

/// <summary>An ask's plans as a caller reads them: each part with its quest and what a person sees working, the edges with their reasons, the flags, and the pointers that moved.</summary>
public static class TeamPlanReading
{
    /// <summary>The plans whole as JSON, for a tool that composes from them: <c>{ask, quests: {part: quest}, plans: [...]}</c>, each plan as the store keeps it.</summary>
    public static string Json(string ask, IReadOnlyList<TeamPlan> plans, IReadOnlyList<TeamPartRow> rows) => JsonFields.Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("ask", ask);
        writer.WriteStartObject("quests");
        foreach (var row in rows.Where(row => row.Quest is not null)) writer.WriteString(row.Part, row.Quest);
        writer.WriteEndObject();
        writer.WriteStartArray("plans");
        foreach (var plan in plans) writer.WriteRawValue(TeamPlanCodec.Write(plan));
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>The reading, or a sentence saying the ask has no plan yet.</summary>
    /// <param name="seen">The blob id each path holds now, where the caller read them: a pack's pointer that differs is named stale.</param>
    public static string Of(
        string ask, IReadOnlyList<TeamPlan> plans, IReadOnlyList<TeamPartRow> rows, IReadOnlyDictionary<string, string> seen,
        IReadOnlyList<TeamMember> members, IReadOnlyList<TeamDecision> decisions)
    {
        if (plans.Count == 0) return $"Ask `#{ask}` has no plan yet: write one with parts, each a quest a worker takes.";
        var text = new StringBuilder();
        foreach (var plan in plans.OrderBy(plan => plan.Depth).ThenBy(plan => plan.Scope, StringComparer.Ordinal))
        {
            text.AppendLine(plan.Scope is null
                ? $"Plan for ask `#{plan.Ask}`, revision {plan.Revision}: {plan.Why}"
                : $"Split of part `{plan.Scope}`, revision {plan.Revision}, depth {plan.Depth}: {plan.Why}");
            foreach (var part in plan.Parts)
            {
                var quest = rows.FirstOrDefault(row => row.Part == part.Id)?.Quest;
                text.AppendLine($"- `{part.Id}` in `{QuestAddress.Spell(part.Repository, part.Lanes)}`, {(quest is null ? "no quest yet" : $"quest `#{quest}`")}: {part.Outcome}");
                if (part.Claims.Count > 0) text.AppendLine($"  claims: {string.Join(", ", part.Claims.Select(claim => $"`{claim}`"))}");
                foreach (var moved in PackStaleness.Moved(part.Pack, seen))
                {
                    text.AppendLine($"  stale: `{moved.File}` ({moved.Role}) moved since it was read at {moved.Blob}; ask the scout again for it alone.");
                }
            }

            foreach (var edge in plan.Edges) text.AppendLine($"- `{edge.Part}` {edge.Kind} `{edge.On}`: {edge.Why}");
            foreach (var line in DeliveryReading.Lines(plan, seen)) text.AppendLine(line);
            foreach (var flag in plan.Flags) text.AppendLine($"- flag `{flag.Kind}`: {flag.Text}");
        }

        foreach (var member in members)
        {
            text.AppendLine($"- {member.Role} `{member.Session}` of {(member.Part is null ? "the plan" : $"part `{member.Part}`")}, at revision {member.Revision}");
        }

        foreach (var decision in decisions)
        {
            text.AppendLine($"- decided at revision {decision.Revision}{(decision.Scope is null ? "" : $" of `{decision.Scope}`")}: {decision.Decision} by {decision.Author}, {decision.Reason}");
        }

        return text.ToString().TrimEnd();
    }
}
