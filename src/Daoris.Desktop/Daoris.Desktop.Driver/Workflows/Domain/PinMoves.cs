using System.Globalization;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One edge of a family: <paramref name="Consumer"/> pins a commit of <paramref name="Producer"/>. A name is a repository's, never a kind of one.</summary>
public sealed record PinEdge(string Consumer, string Producer);

/// <summary>What a producer's landing owes one edge's quest: opened at a generation, or its words moved to the new commit.</summary>
public enum PinMoveActs
{
    /// <summary>No live quest stands for the edge: one is published under the next key.</summary>
    Open,

    /// <summary>The edge's live quest (open or taken) names an older commit: its title and body now name the new one.</summary>
    Retarget,
}

/// <summary>One act the fan-out makes on an edge: the key and words of the quest, and the quest to move where one stands.</summary>
public sealed record PinMoveAct(PinEdge Edge, PinMoveActs Act, string Commit, string Key, string? Quest, string Title, string Body);

/// <summary>A break quest owed to the producer: asked by the consumer, with the failing case, and the hold its filing keeps.</summary>
public sealed record PinBreak(string Key, string From, string To, string Title, string Body, FamilyHold Hold);

/// <summary>
/// A pin held back: the consumer's pin of the producer stays at its older commit because the move to <paramref name="Commit"/> came out red.
/// <paramref name="Since"/> is the first red of this hold and stays through later reds, so its age is how long the edge has lagged;
/// <paramref name="Generation"/> is the newest pin-move quest that came out red.
/// </summary>
public sealed record FamilyHold(string Consumer, string Producer, string Commit, DateTimeOffset Since, int Generation)
{
    /// <summary>How long the pin has been held back.</summary>
    public TimeSpan Age(DateTimeOffset now) => now - Since;

    /// <summary>The edge it holds.</summary>
    public PinEdge Edge => new(Consumer, Producer);
}

/// <summary>
/// The fan-out of a producer's landing, as pure rules over values the look reads: for each consumer edge one pin-move quest,
/// opened at the first landing the consumer does not pin and retargeted by every later one while it is live, a closed one settling the commit
/// it asked. Nothing is written into the consumer. A quest is the edge's by its request key, so no store of its own is kept; a hold-back
/// is the one record the quests cannot say, its age, and is kept beside them.
/// </summary>
public static partial class PinMoves
{
    /// <summary>What a pin-move quest's body says on its own line, which is how a later look reads the commit it asks.</summary>
    private const string CommitLabel = "Commit: ";

    /// <summary>The key of the edge's pin-move quest of a generation: <c>family.&lt;producer&gt;.&lt;consumer&gt;/pin/&lt;n&gt;</c>.</summary>
    public static string KeyOf(PinEdge edge, int generation) => $"{Stem(edge)}/pin/{generation.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The key of the break quest filed for the edge's pin-move quest of that generation.</summary>
    public static string BreakKeyOf(PinEdge edge, int generation) => $"{Stem(edge)}/break/{generation.ToString(CultureInfo.InvariantCulture)}";

    private static string Stem(PinEdge edge) => $"family.{Safe(edge.Producer)}.{Safe(edge.Consumer)}";

    /// <summary>A name as a request key may hold it: its characters outside the key's alphabet become a dash, so two names never share a key by accident of case.</summary>
    private static string Safe(string name) => KeyAlphabet().Replace(name.Trim(), "-").ToLowerInvariant();

    [GeneratedRegex(@"[^A-Za-z0-9._:-]", RegexOptions.CultureInvariant)]
    private static partial Regex KeyAlphabet();

    [GeneratedRegex(@"^Commit: ([0-9a-f]{40})\s*$", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex CommitLine();

    /// <summary>The generation a request key of this edge's pin-move quests carries, or null for any other key.</summary>
    private static int? GenerationOf(PinEdge edge, string? request, string kind)
    {
        var head = $"{Stem(edge)}/{kind}/";
        return request is not null && request.StartsWith(head, StringComparison.Ordinal)
               && int.TryParse(request[head.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var generation) ? generation : null;
    }

    /// <summary>The edge's pin-move quests among <paramref name="quests"/>, oldest generation first.</summary>
    public static IReadOnlyList<QuestView> OfEdge(PinEdge edge, IEnumerable<QuestView> quests) =>
        [.. quests.Where(quest => GenerationOf(edge, quest.Request, "pin") is not null).OrderBy(quest => GenerationOf(edge, quest.Request, "pin"))];

    /// <summary>The commit a pin-move quest asks, read from its own words; null for words that name none.</summary>
    public static string? CommitOf(QuestView quest) => CommitLine().Match(quest.Body) is { Success: true } line ? line.Groups[1].Value : null;

    private static bool Live(QuestView quest) => quest.Status is "Open" or "Taken";

    /// <summary>The title and body of an edge's pin-move quest at a commit: what is asked, how little, and what red does.</summary>
    public static (string Title, string Body) Words(PinEdge edge, string commit) =>
        ($"Move the pin of {edge.Producer} to {Short(commit)}",
         $"{edge.Producer} landed on its line, and `{edge.Consumer}` pins one commit of it in `daoris.family.lock`. Move that pin to this commit.\n\n"
         + $"{CommitLabel}{commit}\n\n"
         + "Change only that lock and what moving the pin forces in this repository: run the gates a pin move runs at that commit, and adapt only what the "
         + "commit breaks, never a feature. Land it as a change of its own.\n\n"
         + $"If the gates stay red, leave the pin where it is and decline this quest with the failing case as the reason: the pin then holds, "
         + $"a break goes to {edge.Producer} with that case, and the person is told only once the hold outlasts the edge's lag bound. "
         + $"A newer landing of {edge.Producer} retargets this quest to its commit.");

    private static string Short(string commit) => commit.Length > 12 ? commit[..12] : commit;

    /// <summary>
    /// What a landing owes the edge now that <paramref name="tip"/> is the producer's line and the consumer pins <paramref name="pinned"/>
    /// (null for no pin yet): nothing where the consumer pins the tip, or a quest already asks it or settled it, the live quest retargeted
    /// where it asks another commit, else the next generation opened.
    /// </summary>
    public static PinMoveAct? Plan(PinEdge edge, string? pinned, string tip, IReadOnlyList<QuestView> quests)
    {
        if (string.Equals(pinned, tip, StringComparison.OrdinalIgnoreCase)) return null;
        var mine = OfEdge(edge, quests);
        var (title, body) = Words(edge, tip);
        if (mine.FirstOrDefault(Live) is { } live)
        {
            return string.Equals(CommitOf(live), tip, StringComparison.OrdinalIgnoreCase)
                ? null
                : new PinMoveAct(edge, PinMoveActs.Retarget, tip, live.Request!, live.Id, title, body);
        }

        if (mine.Any(quest => string.Equals(CommitOf(quest), tip, StringComparison.OrdinalIgnoreCase))) return null;
        var next = (mine.Count == 0 ? 0 : mine.Max(quest => GenerationOf(edge, quest.Request, "pin")!.Value)) + 1;
        return new PinMoveAct(edge, PinMoveActs.Open, tip, KeyOf(edge, next), null, title, body);
    }

    /// <summary>
    /// The breaks the edge's declined pin-move quests owe the producer, oldest first, each with the hold it leaves: a quest whose break is not
    /// filed yet. A hold stands from its first red and moves to the newest red commit; a quest older than the hold's own is already counted.
    /// </summary>
    /// <param name="now">The age a declined quest that carries no time starts its hold at.</param>
    public static IReadOnlyList<PinBreak> Breaks(PinEdge edge, IReadOnlyList<QuestView> quests, IReadOnlyList<FamilyHold> holds, DateTimeOffset now = default)
    {
        var filed = quests.Select(quest => GenerationOf(edge, quest.Request, "break")).Where(each => each is not null).Select(each => each!.Value).ToHashSet();
        var current = holds.FirstOrDefault(hold => hold.Edge == edge);
        var owed = new List<PinBreak>();
        foreach (var quest in OfEdge(edge, quests).Where(quest => quest.Status == "Declined"))
        {
            var generation = GenerationOf(edge, quest.Request, "pin")!.Value;
            if (filed.Contains(generation) || CommitOf(quest) is not { } commit) continue;
            var since = current?.Since ?? quest.Updated ?? now;
            current = new FamilyHold(edge.Consumer, edge.Producer, commit, since, generation);
            var (title, body) = BreakWords(edge, commit, quest);
            owed.Add(new PinBreak(BreakKeyOf(edge, generation), edge.Consumer, edge.Producer, title, body, current));
        }

        return owed;
    }

    private static (string Title, string Body) BreakWords(PinEdge edge, string commit, QuestView declined) =>
        ($"{Short(commit)} of {edge.Producer} breaks {edge.Consumer}",
         $"`{edge.Consumer}` ran its gates at {edge.Producer}'s commit {commit}, and they stayed red, so its pin holds where it was and `{edge.Producer}`'s line "
         + $"stays green for it.\n\n{CommitLabel}{commit}\nPin move: #{declined.Id.TrimStart('#')}\n\nThe failing case, as `{edge.Consumer}`'s session wrote it:\n\n"
         + (string.IsNullOrWhiteSpace(declined.Note) ? "(no case was written)" : declined.Note.Trim())
         + $"\n\nFix it in `{edge.Producer}` or say what `{edge.Consumer}` must change; the next landing of `{edge.Producer}` retargets the pin move.");

    /// <summary>Whether the hold ended: a pin-move quest of a later generation than its newest red came out done, so the pin moved on.</summary>
    public static bool Released(FamilyHold hold, IReadOnlyList<QuestView> quests) =>
        OfEdge(hold.Edge, quests).Any(quest => quest.Status == "Done" && GenerationOf(hold.Edge, quest.Request, "pin")!.Value > hold.Generation);

    /// <summary>Whether the hold has outlasted the edge's lag bound: never where the family declares none for the edge.</summary>
    public static bool Overdue(FamilyHold hold, TimeSpan? lag, DateTimeOffset now) => lag is { } bound && hold.Age(now) > bound;
}
