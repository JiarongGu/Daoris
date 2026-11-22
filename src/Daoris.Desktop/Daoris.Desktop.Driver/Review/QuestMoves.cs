using System.Security.Cryptography;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a quests' move changed: the quests whose records differ and the chains they sit in, and the repositories they are in.</summary>
public sealed record QuestMoveNames(IReadOnlyList<string> Quests, IReadOnlyList<string> Repositories);

/// <summary>
/// What changed between two reads of the quest list, for the fact the change stream's wake tees (<see cref="OpinionBusFacts.QuestMoved"/>): a
/// quest whose record differs, one published and one gone, each with every quest of its chain (its parents and children, which a step
/// opinion reads) and its repositories (which a landing opinion reads whole: every open set-up step there).
/// </summary>
/// <remarks>
/// A change that cannot be named whole names nothing, and the gate then judges every owed opinion: no earlier read, no difference found
/// (the wake moved something the list does not carry), more than a fact lists, or an id a fact cannot carry.
/// </remarks>
public static class QuestMoves
{
    /// <summary>The names of what moved from <paramref name="before"/> to <paramref name="now"/>; null where they cannot be named whole.</summary>
    public static QuestMoveNames? Between(IReadOnlyList<QuestView>? before, IReadOnlyList<QuestView> now)
    {
        if (before is null) return null;
        var was = Prints(before);
        var is_ = Prints(now);
        var changed = new List<QuestView>();
        foreach (var quest in now)
        {
            if (!was.TryGetValue(quest.Id, out var print) || print != is_[quest.Id]) changed.Add(quest);
        }

        changed.AddRange(before.Where(quest => !is_.ContainsKey(quest.Id)));
        if (changed.Count == 0) return null;

        var quests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var repositories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        QuestChains after = new(now), earlier = new(before);
        foreach (var quest in changed)
        {
            quests.Add(quest.Id);
            repositories.Add(quest.To);
            foreach (var each in after.Of(quest.Id).Concat(earlier.Of(quest.Id))) quests.Add(each.Id);
            if (was.ContainsKey(quest.Id)) repositories.Add(before.First(each => string.Equals(each.Id, quest.Id, StringComparison.OrdinalIgnoreCase)).To);
        }

        if (quests.Count > BusShapes.MostListed || repositories.Count > BusShapes.MostListed) return null;
        if (!quests.All(id => id.Length > 0 && BusShapes.Short(id)) || !repositories.All(name => name.Length > 0 && BusShapes.Short(name))) return null;
        return new QuestMoveNames([.. quests.Order(StringComparer.Ordinal)], [.. repositories.Order(StringComparer.Ordinal)]);
    }

    /// <summary>Whether a move that named <paramref name="quests"/> and <paramref name="repositories"/> reaches <paramref name="due"/>: a chat run's opinion reads no quest.</summary>
    public static bool Reaches(OpinionDue due, ISet<string> quests, ISet<string> repositories) =>
        !due.OnRun && (quests.Contains(due.Quest) || (due.Occasion == OpinionRules.Landing && repositories.Contains(due.Repository)));

    private static Dictionary<string, string> Prints(IReadOnlyList<QuestView> quests)
    {
        var prints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var quest in quests) prints[quest.Id] = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(quest)));
        return prints;
    }
}
