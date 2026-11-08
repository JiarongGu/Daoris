using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The requirements an instruction quotes, with the evidence Daoris reads for each.
/// </summary>
internal static class RequirementsPrompt
{
    /// <summary>
    /// What the person requires of the quest, beneath it and beside their words: each numbered
    /// as the service numbers it, their words quoted verbatim line by line, and its check; then how a `done` answers each.
    /// Past <see cref="RequirementsLimit"/> each one left out is named, with where it is read whole. Nothing for a quest
    /// with none, so its instruction names none.
    /// </summary>
    internal static string Required(SessionTarget target)
    {
        if (target.Requirements.Count == 0) return "";

        var text = new StringBuilder("\n");
        text.Append("What the person requires of this quest, in their own words, each quoted verbatim with the check that ")
            .Append("proves the work meets it. A reading of their words, the body's above included, is someone else's:\n");

        var shown = 0;
        foreach (var requirement in target.Requirements.Take(RequirementsShown(target.Requirements)))
        {
            shown++;
            text.Append($"\n- Requirement {shown}:\n\n")
                .Append(QuoteOf(requirement))
                .Append("\n\n  Check: ")
                .Append(CheckOf(requirement))
                .Append('\n')
                .Append(Evidence(requirement));
        }

        if (shown < target.Requirements.Count)
        {
            text.Append($"\n- Requirements {shown + 1} to {target.Requirements.Count} are left out here to keep this instruction ")
                .Append("bounded: `quest_list` shows each whole, and each still needs its answer.\n");
        }

        text.Append("\nWhen you close it `done`, answer each requirement by its number (`answers`): `met`, with how its check was ")
            .Append("met, or `departed`, with the reason and the person's own words it turns on, quoted exactly (`quote`). A `done` ")
            .Append("that leaves one unanswered is refused. A departure is shown to the person, and what follows this quest ")
            .Append("waits until the person accepts it: say plainly where the work departs from their words, rather than close ")
            .Append("as though they had agreed to your reading of them.\n");
        return text.ToString();
    }

    /// <summary>
    /// What Daoris reads itself of one requirement, beneath its check: the paths it reads in the branch's last
    /// commit when the session ends, so the session can commit a missing one itself, and a gate read from the landing queue.
    /// Nothing for a requirement naming none.
    /// </summary>
    internal static string Evidence(QuestRequirementView requirement)
    {
        if (requirement.Evidence.Count == 0) return "";

        var said = new List<string>();
        var paths = requirement.Evidence.Select(item => item.Path).OfType<string>().ToList();
        if (paths.Count > 0)
        {
            var named = paths.Count == 1
                ? $"`{paths[0]}`"
                : $"{string.Join(", ", paths.SkipLast(1).Select(path => $"`{path}`"))} and `{paths[^1]}`";
            var it = paths.Count == 1 ? "it" : "them";
            said.Add($"Daoris reads {named} in your branch's last commit when you end. A met answer without {it} holds the quest "
                + $"for the person, so commit {it} first.");
        }

        foreach (var gate in requirement.Evidence.Select(item => item.Gate).OfType<string>())
        {
            said.Add($"Gate `{gate}` is read from the landing queue, which this machine does not run for it, so a met answer on it "
                + "waits for the person.");
        }

        return $"\n  Evidence: {string.Join(" ", said)}\n";
    }

    /// <summary>The person's words as the instruction quotes them: each line behind its indent and marker.</summary>
    private static string QuoteOf(QuestRequirementView requirement) => TextBounds.Quote(requirement.Quote, indent: "  ");

    /// <summary>The check as the instruction says it: each line after the first indented under its label.</summary>
    private static string CheckOf(QuestRequirementView requirement) =>
        requirement.Check.ReplaceLineEndings("\n").Replace("\n", "\n  ", StringComparison.Ordinal);

    /// <summary>
    /// How many requirements an instruction quotes whole by <see cref="RequirementsLimit"/>: the first always, then each while
    /// its words, check and evidence fit beside those before it, counted as they render. The instruction and its account count by this one
    /// rule.
    /// </summary>
    internal static int RequirementsShown(IReadOnlyList<QuestRequirementView> requirements)
    {
        var used = 0;
        var shown = 0;
        foreach (var requirement in requirements)
        {
            var cost = QuoteOf(requirement).Length + CheckOf(requirement).Length + requirement.Evidence.Sum(item => item.Named.Length);
            if (shown > 0 && used + cost > TargetPrompt.RequirementsLimit) break;
            used += cost;
            shown++;
        }

        return shown;
    }
}
