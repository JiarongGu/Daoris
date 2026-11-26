using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What one look's fan-out did: the lines it says, and the holds that have outlasted their edge's lag bound, for the person to be told of.</summary>
public sealed record FamilyRound(IReadOnlyList<string> Said, IReadOnlyList<FamilyHold> Told);

/// <summary>
/// A family's fan-out at a look: where a producer's line moved past what a consumer pins, the edge's one pin-move quest is opened or
/// retargeted; a pin move that came out red has its break filed to the producer; and the holds past their lag bound are returned. A family
/// is a <c>daoris.family.json</c> in the folder its members' registered checkouts share, and a consumer's pin is read from its own line's
/// lock. Nothing is written into a checkout: the consumer's session moves the pin, as its own change.
/// </summary>
public static class FamilyFanout
{
    /// <param name="registry">The registered repositories: a member is the one whose name the family file lists and whose checkout stands beside it.</param>
    /// <param name="quests">Every quest, closed ones included: the edge's quests, their generations and their ends are read from them.</param>
    /// <param name="lineTip">The commit a producer's line stands at, or null where it reads none.</param>
    /// <param name="lockAt">A consumer's lock as its own line holds it, so a pin edited in a checkout and not landed is no pin.</param>
    public static async Task<FamilyRound> RunAsync(
        IReadOnlyList<RepoView> registry, IReadOnlyList<QuestView> quests, IFamilyDoors doors,
        Func<RepoView, CancellationToken, Task<string?>> lineTip, Func<RepoView, CancellationToken, Task<LockText>> lockAt,
        DateTimeOffset now, CancellationToken ct)
    {
        var said = new List<string>();
        var told = new List<FamilyHold>();
        foreach (var share in registry.Where(row => row.Root is { Length: > 0 }).GroupBy(row => ParentOf(row.Root!), StringComparer.OrdinalIgnoreCase))
        {
            if (Read(Path.Combine(share.Key, FamilyDeclaration.FileName)) is not { } family) continue;
            var members = share.Where(row => family.Members.Any(name => Same(name, row.Repository))).ToList();
            foreach (var producer in members)
            {
                var edges = family.ConsumersOf(producer.Repository, name => UsesOf(members.FirstOrDefault(row => Same(row.Repository, name)))).ToList();
                var consumers = edges.Select(edge => (Edge: new PinEdge(edge.Consumer, producer.Repository), Row: members.FirstOrDefault(row => Same(row.Repository, edge.Consumer)))).Where(each => each.Row is not null).ToList();
                if (consumers.Count == 0) continue;
                var tip = await lineTip(producer, ct).ConfigureAwait(false);
                foreach (var (edge, row) in consumers)
                {
                    var pinned = PinIn(await lockAt(row!, ct).ConfigureAwait(false), producer.Repository);
                    if (tip is not null && pinned.Readable && PinMoves.Plan(edge, pinned.Commit, tip, quests) is { } act)
                    {
                        said.Add(await ActAsync(act, doors, ct).ConfigureAwait(false));
                    }

                    foreach (var broke in PinMoves.Breaks(edge, quests))
                    {
                        said.Add(await BreakAsync(edge, broke, doors, ct).ConfigureAwait(false));
                    }

                    if (pinned.Readable && PinMoves.Hold(edge, quests, pinned.Commit, tip, now) is { } hold && PinMoves.Overdue(hold, family.LagOf(edge), now)) told.Add(hold);
                }
            }
        }

        return new FamilyRound(said, told);
    }

    private static async Task<string> ActAsync(PinMoveAct act, IFamilyDoors doors, CancellationToken ct)
    {
        var edge = act.Edge;
        var at = act.Commit.Length > 12 ? act.Commit[..12] : act.Commit;
        try
        {
            if (act.Act == PinMoveActs.Open)
            {
                var made = await doors.PublishAsync(new RequestAsk(act.Key, edge.Producer, edge.Consumer, act.Title, act.Body), ct).ConfigureAwait(false);
                return made.Ok
                    ? $"family  {edge.Producer} landed {at}: the pin move of {edge.Consumer} is asked (#{made.Quest?.Id})."
                    : $"family  {edge.Producer} landed {at}: the pin move of {edge.Consumer} was refused ({made.Code}: {made.Message}).";
            }

            var moved = await doors.ReviseWordsAsync(act.Quest!, act.Title, act.Body, ct).ConfigureAwait(false);
            return moved.Ok
                ? $"family  {edge.Producer} landed {at}: the pin move of {edge.Consumer} (#{act.Quest}) now asks it."
                : $"family  {edge.Producer} landed {at}: the pin move of {edge.Consumer} (#{act.Quest}) was refused ({moved.Message}).";
        }
        catch (HttpRequestException)
        {
            return $"family  {edge.Producer} landed {at}: the host did not answer for {edge.Consumer}'s pin move; it is sent again at the next look.";
        }
    }

    private static async Task<string> BreakAsync(PinEdge edge, PinBreak broke, IFamilyDoors doors, CancellationToken ct)
    {
        try
        {
            var made = await doors.PublishAsync(new RequestAsk(broke.Key, broke.From, broke.To, broke.Title, broke.Body), ct).ConfigureAwait(false);
            return made.Ok
                ? $"family  {edge.Consumer}'s pin of {edge.Producer} holds: the break is filed to {edge.Producer} (#{made.Quest?.Id})."
                : $"family  {edge.Consumer}'s pin of {edge.Producer} holds: its break was refused ({made.Code}: {made.Message}).";
        }
        catch (HttpRequestException)
        {
            return $"family  {edge.Consumer}'s pin of {edge.Producer} holds: the host did not answer for the break; it is sent again at the next look.";
        }
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string ParentOf(string root) => Path.GetDirectoryName(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "";

    private static FamilyDeclaration? Read(string file)
    {
        try
        {
            return File.Exists(file) ? FamilyDeclaration.Read(File.ReadAllText(file)) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>A member's <c>uses</c>, read from its checkout's manifest by the registry's rule; none where it has no manifest or none that reads.</summary>
    private static IReadOnlyList<string> UsesOf(RepoView? row)
    {
        if (row?.Root is not { Length: > 0 } root) return [];
        try
        {
            return JsonNode.Parse(File.ReadAllText(Path.Combine(root, DeclarationNames.Manifest)))?["domain"] is JsonObject domain
                ? LineRegistration.UsesOf(domain["uses"], row.Repository)
                : [];
        }
        catch (Exception problem) when (problem is IOException or System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>The commit a lock's text pins of the producer: none where there is no lock or no pin yet, and unreadable where it is no lock, which is left for the check.</summary>
    private static (bool Readable, string? Commit) PinIn(LockText lockText, string producer)
    {
        if (!lockText.Read) return (false, null);
        if (lockText.Text is null) return (true, null);
        return FamilyLock.Read(lockText.Text) is { } pins ? (true, pins.GetValueOrDefault(producer.ToLowerInvariant())) : (false, null);
    }

    /// <summary>Whether a registered checkout stands in a folder holding a family file: where none does, a look reads no quest for it.</summary>
    public static bool HasFamily(IReadOnlyList<RepoView> registry) =>
        registry.Any(row => row.Root is { Length: > 0 } root && File.Exists(Path.Combine(ParentOf(root), FamilyDeclaration.FileName)));
}

/// <summary>A consumer's lock as its line holds it: <see cref="Read"/> false where the line could not be read, else the text, null where the line has none.</summary>
public readonly record struct LockText(bool Read, string? Text);
