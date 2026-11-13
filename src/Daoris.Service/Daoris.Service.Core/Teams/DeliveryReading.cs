namespace Daoris.Knowledge;

/// <summary>A plan's delivery as a caller reads it: each release order with its reason and its evidence, the stale ones named, then the questions the person is asked.</summary>
public static class DeliveryReading
{
    /// <summary>One line per release order and per question; nothing for a plan with no deployables.</summary>
    /// <param name="seen">The blob each evidence place holds now, where the caller read it: an edge whose evidence differs is named stale.</param>
    public static IEnumerable<string> Lines(TeamPlan plan, IReadOnlyDictionary<string, string> seen)
    {
        var stale = DeliveryGraph.Stale(plan.Releases, seen);
        foreach (var edge in plan.Releases)
        {
            var cited = edge.Evidence is { } evidence ? $" ({edge.Source}: {evidence.Repository} `{evidence.File}`{(evidence.Range.Length > 0 ? $":{evidence.Range}" : "")} at {evidence.Blob})" : $" ({edge.Source})";
            var standing = edge.Waived is { } waived ? $" WAIVED by the person: {waived}" : stale.Contains(edge) ? " STALE: its evidence moved; confirm or drop it." : "";
            yield return $"- release `{edge.Deployable}` after `{edge.On}`: {edge.Why}{cited}{standing}";
        }

        foreach (var question in DeliveryGraph.Open(plan, seen))
        {
            yield return $"- question `{question.Key}` ({question.Kind}) for the person: {question.Text}";
        }

        foreach (var answer in plan.Answers)
        {
            yield return $"- answered by the person, `{answer.Key}`: {answer.Words}";
        }
    }
}
