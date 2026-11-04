namespace Daoris.Knowledge;

/// <summary>What the person requires of a quest (D133 §3): each requirement's shape, its evidence, and whether the person said its quote.</summary>
internal sealed class QuestRequirementJudge(AskStore? asks)
{
    /// <summary>
    /// The requirements as a quest keeps them: each trimmed at its ends, with both its
    /// quote and its check, at most <see cref="QuestExchange.MaxRequirements"/> of them, each half at most
    /// <see cref="QuestExchange.MaxRequirementLength"/> characters — or why not, naming which.
    /// </summary>
    /// <remarks>
    /// The shape alone, which a remote judges of a pushed quest too: whether the person said the quote is
    /// judged where the ask is (<see cref="JudgeQuotesAsync"/>), and a remote holds no asks.
    /// </remarks>
    public static (IReadOnlyList<QuestRequirement> Requirements, string? Refusal) JudgeShape(IReadOnlyList<QuestRequirement> given)
    {
        if (given.Count > QuestExchange.MaxRequirements)
        {
            return ([], $"A quest carries at most {QuestExchange.MaxRequirements} requirements — this one was given {given.Count}. "
                        + "Quote the words the work is measured by; everything the person said stays on the ask.");
        }

        var kept = new List<QuestRequirement>();
        foreach (var (requirement, index) in given.Select((requirement, index) => (requirement, index + 1)))
        {
            var quote = requirement.Quote?.Trim() ?? "";
            var check = requirement.Check?.Trim() ?? "";
            if (quote.Length == 0 || check.Length == 0)
            {
                return ([], $"Requirement {index} needs both halves: the person's own words, quoted, and the check "
                            + "that proves the work meets them.");
            }

            if (quote.Length > QuestExchange.MaxRequirementLength || check.Length > QuestExchange.MaxRequirementLength)
            {
                return ([], $"Requirement {index}'s {(quote.Length > QuestExchange.MaxRequirementLength ? "quote" : "check")} is longer "
                            + $"than {QuestExchange.MaxRequirementLength} characters — quote the words the work turns on, and say the "
                            + "check in a few lines.");
            }

            var (evidence, unfit) = JudgeEvidence(index, requirement.Evidence ?? []);
            if (unfit is not null) return ([], unfit);
            kept.Add(new QuestRequirement(quote, check) { Evidence = evidence });
        }

        return (kept, null);
    }

    /// <summary>
    /// A requirement's evidence as a quest keeps it (D144 §2): at most <see cref="QuestEvidence.MaxItems"/> items,
    /// each exactly one of a path or a gate, a path trimmed at its ends and judged (<see cref="QuestEvidence.JudgePath"/>),
    /// a gate's name judged — or why not, naming the requirement. A gate is refused for now, naming the queue it waits for.
    /// </summary>
    /// <remarks>
    /// The shape alone, which a remote judges of a pushed quest too: written by whoever writes the requirement, never by
    /// the session it will judge, and read later by the driver, never here.
    /// </remarks>
    private static (IReadOnlyList<QuestEvidence> Evidence, string? Refusal) JudgeEvidence(int index, IReadOnlyList<QuestEvidence> given)
    {
        if (given.Count > QuestEvidence.MaxItems)
        {
            return ([], $"Requirement {index} names {given.Count} items of evidence, and a requirement names at most "
                        + $"{QuestEvidence.MaxItems}: the facts its check turns on, not an inventory of the work.");
        }

        var kept = new List<QuestEvidence>();
        foreach (var (item, number) in given.Select((item, number) => (item, number + 1)))
        {
            var which = $"Requirement {index}'s evidence item {number}";
            var path = item.Path?.Trim();
            var gate = item.Gate?.Trim();
            if (string.IsNullOrEmpty(path) == string.IsNullOrEmpty(gate))
            {
                return ([], $"{which} names exactly one of `path` or `gate`: a file or folder the done's commit must hold, "
                            + "or a gate the receiving repository declares.");
            }

            if (!string.IsNullOrEmpty(path))
            {
                if (QuestEvidence.JudgePath(path) is { } why)
                {
                    return ([], $"{which}, `{Clip(path)}`, is not a path a commit can be asked for: {why}. Name it from the "
                                + "receiving repository's root with forward slashes, e.g. `docs/report.md`.");
                }

                if (!kept.Any(each => each.Path == path)) kept.Add(new QuestEvidence(path));
                continue;
            }

            if (QuestEvidence.JudgeGate(gate!) is { } unfit)
            {
                return ([], $"{which}, `{Clip(gate!)}`, is not a gate's name: {unfit}.");
            }

            // Until the landing queue reads gates (D144 point 5), nothing here could read one: refused, not kept unread.
            return ([], $"{which} names the gate `{gate}`. A gate is read from the landing queue's verdict, and this build "
                        + "has no landing queue to read it from yet, so a gate cannot be named. Say the gate in the check, "
                        + "and name a path the work leaves if there is one.");
        }

        return (kept, null);
    }

    /// <summary>
    /// Whether the person said what each requirement quotes (D133 §3): somewhere in one of their
    /// words on the ask that asks this quest — its sentence, an answer to a session, or a message added to
    /// one — verbatim, whitespace and case aside. Null when every quote is theirs, or there are none.
    /// </summary>
    /// <remarks>
    /// <para><b>Only the asker's ask.</b> A quest one repository asks of another is asked on no ask, so there
    /// are no words of the person's to quote; which ask a repository's session works for is its record's, and
    /// is not read here.</para>
    ///
    /// <para><b>A fact, with no model</b> (D24, D54): a reading of the person's words is the body's, never a
    /// requirement, so a quote that is not theirs is refused rather than compared by meaning.</para>
    /// </remarks>
    public async Task<(QuestPublishRefusal Refusal, string Message)?> JudgeQuotesAsync(
        string from, IReadOnlyList<QuestRequirement> requirements, CancellationToken ct)
    {
        if (requirements.Count == 0) return null;

        if (AskDesk.AskOf(from) is not { } askId)
        {
            return (QuestPublishRefusal.BadRequirement,
                $"A requirement quotes the person's own words on the ask a quest is asked by, and this quest is asked "
                + $"by `{from}`, on no ask — so there are no words of theirs to quote. Say what is needed in the body.");
        }

        if (asks is null || await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } held)
        {
            return (QuestPublishRefusal.BadRequirement,
                $"This quest is asked by ask `#{askId}`, which this host does not hold, so there are no words to check "
                + "a requirement's quote against. Say what is needed in the body.");
        }

        var unsaid = requirements
            .Select((requirement, index) => (requirement.Quote, Index: index + 1))
            .Where(quoted => !held.Words.Any(word => QuestRequirement.QuotedIn(quoted.Quote, word.Text)))
            .ToList();
        if (unsaid.Count == 0) return null;

        var since = held.WordsKeptFrom is { } keptFrom
            ? " Its words are kept from "
              + keptFrom.UtcDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
              + " UTC: what the person said on it before then is in its sessions' records, not on the ask."
            : "";
        return (QuestPublishRefusal.NotQuoted,
            (unsaid.Count == 1 ? "A requirement quotes" : $"{unsaid.Count} requirements quote")
            + $" words the person never said on ask `#{held.Id}`:\n\n"
            + string.Join("\n", unsaid.Select(quoted => $"- requirement {quoted.Index}: \"{Clip(quoted.Quote)}\""))
            + "\n\nA requirement is the person's own words, verbatim (whitespace and case aside), from the ask or from "
            + "what they said on it since: an answer to a session, or a message added to one. Copy their words; your "
            + "reading of them belongs in the body." + since);
    }
}
