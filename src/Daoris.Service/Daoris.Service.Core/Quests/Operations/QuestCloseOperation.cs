using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A done: its taker's, or a closure graph's close, which names every quest it closes on its submission and is
/// kept once under its key. A done that loses a rebase takes the follow-ups only it published with it.
/// </summary>
[QuestOperation("done", QuestOperationPhase.Log)]
internal sealed class QuestCloseOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Done;

    /// <summary>
    /// Its note beside its parts, its answers, a graph's close's provenance and the session that wrote its handback, each only when set,
    /// so a done without one reads as it did before it.
    /// </summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        if (operation.Answers is { Count: > 0 } answers)
        {
            writer.WritePropertyName("answers");
            QuestRecordCodec.WriteAnswers(writer, answers);
        }

        if (operation.Close is { } close) QuestRecordCodec.WriteClose(writer, close);
        if (operation.Session is { } session) writer.WriteString("session", session);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        Close = payload.TryGetProperty("close", out var close) ? QuestRecordCodec.ReadClose(close) : null,
        Session = QuestRecordCodec.ReadSession(payload),
    };

    /// <summary>
    /// What this machine did on a follow-up only the lost close published goes with it: a take of it kept as a conflict naming the close,
    /// so its session is stopped.
    /// </summary>
    public Task<IReadOnlyList<QuestOperation>> LostAsync(
        QuestTx tx, QuestOperation operation, Quest? standing, bool takeLost, QuestRebaseLosses losses) =>
        tx.ForgetFollowUpsAsync(operation.Quest, takeLost, "close");

    /// <summary>
    /// The graph's close: every named quest Done, each on its named submission, in one write. Each quest is judged
    /// on its history replayed inside the write: bound to a closure graph, held by this machine's take, its named submission the current
    /// one, nothing holding it, and its chain's next step published from it. Any failed check writes nothing, and answers which check on
    /// which quest. Only then is Done appended to each, in the order named, with its submission's note and answers and the close's key
    /// and token as provenance.
    /// </summary>
    /// <remarks>
    /// The key is what makes a close once. Each outcome is kept under its key, a refusal as much as a close, beside the
    /// token and the submissions named, and never as an operation. The same close sent again after a timeout or a lost
    /// answer is answered with that first outcome, writing nothing, though what refused it is gone since; anything else sent with that
    /// key is not that close, and is refused.
    /// <para>The close holds its own scope before any quest's state, so no driver's filter is its only guard: every
    /// quest named asks <paramref name="at"/>'s repository, is <paramref name="at"/> or one it grew from, and is bound to its graph; else
    /// the close is refused on that quest, kept under its key, and nothing is written.</para>
    /// </remarks>
    /// <param name="at">The quest the close is at, the route's: its chain's last quest here, which must be among those named.</param>
    internal async Task<QuestClosing> CloseAsync(
        QuestTx tx, string at, IReadOnlyList<(string Id, QuestOperationRef Submission)> named, QuestClose close, DateTimeOffset now)
    {
        // The exchange refuses such a call as malformed before it gets here, keeping no key.
        if (!named.Any(each => string.Equals(each.Id, at, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"A close at `#{at}` names `#{at}` among its quests.", nameof(at));
        }

        var judged = new List<(Quest Quest, IReadOnlyList<QuestOperation> History)>();
        string? missing = null;
        foreach (var (id, _) in named)
        {
            var history = await tx.HistoryAsync(id).ConfigureAwait(false);
            if (QuestLog.Replay(history) is not { } quest)
            {
                missing = id;
                break;
            }

            judged.Add((quest, history));
        }

        var standing = judged.Select(each => each.Quest).ToList();
        var invocation = Invocation(named);
        if (await CloseKeptAsync(tx, close.Key).ConfigureAwait(false) is { } first)
        {
            if (first.Token != close.Token || first.Named != invocation)
            {
                return new QuestClosing(QuestCloseRefusal.KeyReused, null, standing);
            }

            return new QuestClosing(
                first.Refusal, standing.FirstOrDefault(quest => quest.Id == first.On), standing, Replayed: true, On: first.On);
        }

        // Kept under its key in the same write as what it judged, so a retry answers this outcome and no other.
        async Task<QuestClosing> Refuse(QuestCloseRefusal refusal, Quest? quest, string on)
        {
            await KeepCloseAsync(tx, close, invocation, refusal, on, now).ConfigureAwait(false);
            return new QuestClosing(refusal, quest, standing, On: on);
        }

        if (missing is not null) return await Refuse(QuestCloseRefusal.NotFound, null, missing).ConfigureAwait(false);

        // The close is one repository's, one chain's and one graph's: those of the quest it is at. A quest outside it is refused whatever
        // its state, so a close naming one is never made in part.
        var terminal = standing.First(quest => string.Equals(quest.Id, at, StringComparison.Ordinal));
        IReadOnlySet<string>? lineage = null;
        foreach (var quest in standing)
        {
            if (ReferenceEquals(quest, terminal)) continue;
            lineage ??= await LineageAsync(tx, terminal).ConfigureAwait(false);
            var scope = quest switch
            {
                // The service's own rule for one tree, as a chain's next step reads it (NextStep).
                _ when !string.Equals(quest.To, terminal.To, StringComparison.OrdinalIgnoreCase) => QuestCloseRefusal.OtherRepository,
                _ when !lineage.Contains(quest.Id) => QuestCloseRefusal.NotInChain,
                // An unbound quest is the state check's NotBound, below: it names the bind missing, not another graph.
                { Closure: { } graph } when terminal.Closure is { } own && !string.Equals(graph, own, StringComparison.Ordinal)
                    => QuestCloseRefusal.OtherGraph,
                _ => QuestCloseRefusal.None,
            };

            if (scope != QuestCloseRefusal.None) return await Refuse(scope, quest, quest.Id).ConfigureAwait(false);
        }

        foreach (var ((quest, history), (_, submission)) in judged.Zip(named))
        {
            var done = new QuestOperation(quest.Id, QuestOperationKind.Done, tx.Machine, 0, now);
            var refusal = quest switch
            {
                { Status: QuestStatus.Done or QuestStatus.Declined } => QuestCloseRefusal.Closed,
                { Closure: null } => QuestCloseRefusal.NotBound,
                _ when QuestLog.OnALostTake(quest, history, done)
                       || QuestLog.Claim(history, tx.Machine) is not (QuestClaim.Held or QuestClaim.Unconfirmed) => QuestCloseRefusal.NotHeld,
                { Submission: null } => QuestCloseRefusal.Stale,
                _ when quest.Submission!.Machine != submission.Machine || quest.Submission.Sequence != submission.Sequence
                    => QuestCloseRefusal.Stale,
                { Held: true } => QuestCloseRefusal.Held,
                _ => QuestCloseRefusal.None,
            };

            if (refusal == QuestCloseRefusal.None && quest.Then.Count > 0 && await tx.SealingAsync(quest).ConfigureAwait(false) is null)
            {
                refusal = QuestCloseRefusal.Unsealed;
            }

            if (refusal != QuestCloseRefusal.None) return await Refuse(refusal, quest, quest.Id).ConfigureAwait(false);
        }

        var closed = new List<Quest>();
        foreach (var (quest, history) in judged)
        {
            var operation = await tx.AppendAsync(new QuestOperation(
                quest.Id, QuestOperationKind.Done, tx.Machine, 0, now, quest.Submission!.Note, Answers: quest.Answers, Close: close))
                .ConfigureAwait(false);
            var done = QuestLog.Replay([.. history, operation])!;
            await tx.WriteCacheAsync(done).ConfigureAwait(false);
            closed.Add(done);
        }

        await KeepCloseAsync(tx, close, invocation, QuestCloseRefusal.None, null, now).ConfigureAwait(false);
        return new QuestClosing(QuestCloseRefusal.None, null, closed);
    }

    /// <summary>
    /// What became of a close sent with <paramref name="close"/>'s key, answered from what is kept under the
    /// key, never by judging or writing a Done: the first outcome where the key is taken (a close, a refusal, or
    /// <see cref="QuestCloseRefusal.KeyReused"/> where this is not that close), replayed as a send of it again would be. A key never taken
    /// is <see cref="QuestCloseRefusal.Withdrawn"/>, kept under it in the same write with the token and submissions named, so the close it
    /// names never runs: a send of it still on its way is answered that refusal.
    /// </summary>
    /// <remarks>
    /// The driver asks this of every close whose answer it does not hold, before anything else, and never sends that close again: one
    /// never delivered, judged on evidence that may have moved since, and a key's idempotency protects an outcome already kept, not a
    /// key never used. Its close is judged again on what is there now, under a new key.
    /// </remarks>
    internal async Task<QuestClosing> CloseOutcomeAsync(
        QuestTx tx, IReadOnlyList<(string Id, QuestOperationRef Submission)> named, QuestClose close, DateTimeOffset now)
    {
        var standing = new List<Quest>();
        foreach (var (id, _) in named)
        {
            if (QuestLog.Replay(await tx.HistoryAsync(id).ConfigureAwait(false)) is { } quest) standing.Add(quest);
        }

        var invocation = Invocation(named);
        if (await CloseKeptAsync(tx, close.Key).ConfigureAwait(false) is not { } first)
        {
            await KeepCloseAsync(tx, close, invocation, QuestCloseRefusal.Withdrawn, null, now).ConfigureAwait(false);
            return new QuestClosing(QuestCloseRefusal.Withdrawn, null, standing);
        }

        if (first.Token != close.Token || first.Named != invocation) return new QuestClosing(QuestCloseRefusal.KeyReused, null, standing);
        return new QuestClosing(first.Refusal, standing.FirstOrDefault(quest => quest.Id == first.On), standing, Replayed: true, On: first.On);
    }

    /// <summary>
    /// The ids of <paramref name="quest"/> and every quest it grew from, walked through its parents in every repository as
    /// the driver's <c>QuestClosing.Lineage</c> walks the quests this store lists: the chain a close at it may name. A parent this store does
    /// not hold ends the walk, so what lies above it is not shown to be of the chain; a parent seen twice, which no chain makes, ends it too.
    /// </summary>
    private static async Task<IReadOnlySet<string>> LineageAsync(QuestTx tx, Quest quest)
    {
        var lineage = new HashSet<string>(StringComparer.Ordinal) { quest.Id };
        for (var at = quest; at.Parent is { Length: > 0 } parent && lineage.Add(parent);)
        {
            if (await tx.FindAsync(parent).ConfigureAwait(false) is not { } before) break;
            at = before;
        }

        return lineage;
    }

    /// <summary>
    /// What a close named, as its record compares it: each quest with its submission, in the quests' order, so the same
    /// close sent again names the same whatever order it lists them in.
    /// </summary>
    private static string Invocation(IReadOnlyList<(string Id, QuestOperationRef Submission)> named) =>
        JsonFields.Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var (id, submission) in named.OrderBy(each => each.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("quest", id);
                writer.WriteString("machine", submission.Machine);
                writer.WriteNumber("sequence", submission.Sequence);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });

    /// <summary>A close's first outcome, as kept under its key: its token, what it named, how it ended and on which quest.</summary>
    private sealed record KeptClose(string Token, string Named, QuestCloseRefusal Refusal, string? On);

    /// <summary>The outcome kept under <paramref name="key"/>, or null where no close is sent with it.</summary>
    private static async Task<KeptClose?> CloseKeptAsync(QuestTx tx, string key)
    {
        await using var command = tx.Command();
        command.CommandText = "SELECT token, named, refusal, quest FROM quest_closes WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        await using var reader = await command.ExecuteReaderAsync(tx.Ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(tx.Ct).ConfigureAwait(false)) return null;

        // A refusal this build does not name is still a refusal: answered as a key that is not this close's, never as a close.
        var refusal = Enum.TryParse<QuestCloseRefusal>(reader.GetString(2), out var kept) ? kept : QuestCloseRefusal.KeyReused;
        return new KeptClose(reader.GetString(0), reader.GetString(1), refusal, reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    /// <summary>Keeps a close's outcome under its key: the store's own, never pushed or served.</summary>
    private static async Task KeepCloseAsync(
        QuestTx tx, QuestClose close, string invocation, QuestCloseRefusal refusal, string? on, DateTimeOffset now)
    {
        await using var command = tx.Command();
        command.CommandText = """
            INSERT INTO quest_closes (key, token, named, refusal, quest, at) VALUES ($key, $token, $named, $refusal, $quest, $at)
            """;
        command.Parameters.AddWithValue("$key", close.Key);
        command.Parameters.AddWithValue("$token", close.Token);
        command.Parameters.AddWithValue("$named", invocation);
        command.Parameters.AddWithValue("$refusal", refusal.ToString());
        command.Parameters.AddWithValue("$quest", (object?)on ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", Moments.Write(now));
        await command.ExecuteNonQueryAsync(tx.Ct).ConfigureAwait(false);
    }
}
