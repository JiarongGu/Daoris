namespace Daoris.Knowledge;

/// <summary>A remote's say over a publish a machine pushed.</summary>
internal sealed class QuestReceived(QuestCarry carry)
{
    /// <summary>
    /// A remote's say over a publish a machine pushed (design §8) — the judgement the machine's own
    /// exchange already ran, run again where it lands, because a remote takes nobody's word for it.
    /// Null when the quest is fit to keep here.
    /// </summary>
    /// <remarks>
    /// The receiver must be registered here, since a quest nobody here can see has nobody to answer it.
    /// What it carries must be what a record may name: links, and files by name. There are no bytes to
    /// judge, because a pushed operation has no field for them.
    /// </remarks>
    public string? Judge(Quest asked, IReadOnlyList<Registration> registered)
    {
        // Adopted, not merely addressable (D70): a deployment holds no roots, and a repository that
        // has not adopted has no manifest to declare a join with, so its quests never leave the
        // machine that can drive it. A pushed quest to one is a quest nobody here could answer.
        if (!registered.Any(r => r.Adopted && string.Equals(r.Repository, asked.To, StringComparison.OrdinalIgnoreCase)))
        {
            return $"`{asked.To}` is not registered at this deployment, so nobody here could answer quest "
                   + $"`#{asked.Id}`. Its registration travels first; the next sync tries again.";
        }

        // Its lanes were judged against the registration by the machine that published it (D115 §2.2),
        // and a registration here may lag its own; but a lane no registration could declare is no lane.
        if (asked.Lanes.FirstOrDefault(lane => !QuestAddress.IsLaneId(lane)) is { } malformed)
        {
            return $"Quest `#{asked.Id}` names `{Clip(malformed)}` as a lane, and no repository could declare it — a "
                   + "lane is lower-case letters, digits and dashes, starting with a letter.";
        }

        // Whether the person said a requirement's quote was judged where the ask is; a remote holds
        // no asks, but a requirement missing half of itself is no requirement wherever it lands.
        if (QuestRequirementJudge.JudgeShape(asked.Requirements).Refusal is { } unfitRequirement)
        {
            return $"Quest `#{asked.Id}`: {unfitRequirement}";
        }

        // A short title a publish here would refuse is one no record should carry.
        if (QuestTitles.Judge(asked.Short).Refusal is { } unfitShort)
        {
            return $"Quest `#{asked.Id}`: {unfitShort}";
        }

        if (QuestWeights.Judge(asked.Weight).Refusal is { } unfitWeight)
        {
            return $"Quest `#{asked.Id}`: {unfitWeight}";
        }

        if (QuestPaths.Judge(asked.Paths).Refusal is { } unfitPaths)
        {
            return $"Quest `#{asked.Id}`: {unfitPaths}";
        }

        if (QuestRequests.Judge(asked.Request).Refusal is { } unfitRequest)
        {
            return $"Quest `#{asked.Id}`: {unfitRequest}";
        }

        var carried = carry.Judge(new QuestAsk(asked.From, asked.To, asked.Title, asked.Body)
        {
            Links = asked.Links,
            Named = asked.Attachments,
        });
        if (carried.Refusal is not null) return carried.Message;

        return carried.Links.Count == asked.Links.Count && carried.Attachments.Count == asked.Attachments.Count
            ? null
            : $"Quest `#{asked.Id}` carries a link or a file twice, which a publish here would not have kept.";
    }
}
