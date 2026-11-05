using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A quest asked: the first operation of every history, carrying the quest's words and everything it carries, all a replay needs to
/// make the quest on any machine. The same ask published first elsewhere is the quest, so a second copy is forgotten in a rebase. The
/// chain's next step a close or a submission lets go is published here too, in the same transaction.
/// </summary>
[QuestOperation("published", QuestOperationPhase.Log)]
internal sealed class QuestPublishOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Published;

    /// <summary>
    /// The quest as asked, then its note. Each optional field is written only when set, so a publish from before it reads as it did,
    /// and an older build reads a newer one as asked without it.
    /// </summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Published is { } published)
        {
            writer.WriteString("from", published.From);
            writer.WriteString("to", published.To);
            writer.WriteString("title", published.Title);
            writer.WriteString("body", published.Body);
            writer.WriteString("workspace", published.Workspace);
            writer.WritePropertyName("links");
            QuestRecordCodec.WriteLinks(writer, published.Links);
            writer.WritePropertyName("attachments");
            QuestRecordCodec.WriteAttachments(writer, published.Attachments);
            writer.WritePropertyName("then");
            QuestRecordCodec.WriteSteps(writer, published.Then);
            if (published.Parent is not null) writer.WriteString("parent", published.Parent);
            if (published.PublishedBy is not null) writer.WriteString("publishedBy", published.PublishedBy);
            if (published.Lanes.Count > 0)
            {
                writer.WritePropertyName("lanes");
                QuestRecordCodec.WriteLinks(writer, published.Lanes);
            }

            if (published.Requirements.Count > 0)
            {
                writer.WritePropertyName("requirements");
                QuestRecordCodec.WriteRequirements(writer, published.Requirements);
            }

            // A derived short name is never written.
            if (published.Short is not null) writer.WriteString("short", published.Short);
            if (published.Weight is not null) writer.WriteString("weight", published.Weight);
            if (published.Paths.Count > 0)
            {
                writer.WritePropertyName("paths");
                QuestRecordCodec.WriteLinks(writer, published.Paths);
            }

            if (published.Request is not null) writer.WriteString("request", published.Request);
            if (published.Review is { } chosen)
            {
                writer.WritePropertyName("review");
                Reviews.Write(writer, chosen);
            }

            if (published.SetUpIn is not null) writer.WriteString("setUpIn", published.SetUpIn);
            if (published.GrewFrom is { } from) QuestRecordCodec.WriteRef(writer, "grewFrom", from);
        }

        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        writer.WriteEndObject();
    }

    /// <summary>The quest as asked, filed and updated at the moment of the row; each optional field is absent from a row before it.</summary>
    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        Published = new Quest(
            read.Quest,
            payload.GetProperty("from").GetString() ?? "",
            payload.GetProperty("to").GetString() ?? "",
            payload.GetProperty("title").GetString() ?? "",
            payload.GetProperty("body").GetString() ?? "",
            QuestStatus.Open, null, read.At, read.At,
            Workspace: Workspaces.Normalize(payload.GetProperty("workspace").GetString()))
        {
            Links = QuestRecordCodec.ReadLinks(payload.GetProperty("links")),
            Attachments = QuestRecordCodec.ReadAttachments(payload.GetProperty("attachments")),
            Then = QuestRecordCodec.ReadSteps(payload.GetProperty("then")),
            Parent = payload.TryGetProperty("parent", out var parent) ? parent.GetString() : null,
            PublishedBy = payload.TryGetProperty("publishedBy", out var by) ? by.GetString() : null,
            Lanes = payload.TryGetProperty("lanes", out var lanes) ? QuestRecordCodec.ReadLinks(lanes) : [],
            Requirements = payload.TryGetProperty("requirements", out var required) ? QuestRecordCodec.ReadRequirements(required) : [],
            Short = payload.TryGetProperty("short", out var shortTitle) ? shortTitle.GetString() : null,
            Weight = payload.TryGetProperty("weight", out var weight) ? weight.GetString() : null,
            Paths = payload.TryGetProperty("paths", out var paths) ? QuestRecordCodec.ReadLinks(paths) : [],
            Request = payload.TryGetProperty("request", out var request) ? request.GetString() : null,
            Review = payload.TryGetProperty("review", out var chosen) ? Reviews.JudgedReview(chosen) : null,
            SetUpIn = payload.TryGetProperty("setUpIn", out var setUpIn) ? setUpIn.GetString() : null,
            GrewFrom = payload.TryGetProperty("grewFrom", out var grew) ? QuestRecordCodec.ReadRef(grew) : null,
        },
    };

    /// <summary>Publish a quest. Returns the existing one unchanged if it was already asked.</summary>
    /// <remarks>
    /// The workspace is the circle both sides share, decided by <see cref="QuestExchange"/>, which is where the same-workspace clause
    /// lives; every other field is already judged by the exchange. A set-up step's <paramref name="parent"/> derives its id as a chain
    /// step's does, so pressing twice is one step.
    /// </remarks>
    internal async Task<Quest> PublishAsync(
        QuestTx tx, string from, string to, string title, string body, DateTimeOffset now, string? workspace,
        IReadOnlyList<string>? links, IReadOnlyList<QuestAttachment>? attachments, IReadOnlyList<QuestStep>? then, string? publishedBy,
        IReadOnlyList<string>? lanes, IReadOnlyList<QuestRequirement>? requirements, string? shortTitle, QuestReview? review,
        string? setUpIn, string? parent, string? weight, IReadOnlyList<string>? paths, string? request)
    {
        var sorted = QuestStore.Sorted(lanes);
        var id = QuestStore.MakeId(from, to, title, parent, sorted, request);

        // The same ask already held is the answer.
        if (await tx.FindAsync(id).ConfigureAwait(false) is { } existing) return existing;

        var asked = new Quest(
            id, from, to, title, body, QuestStatus.Open, null, now, now,
            Workspace: Workspaces.Normalize(workspace))
        {
            Links = links ?? [],
            Attachments = attachments ?? [],
            Then = then ?? [],
            PublishedBy = string.IsNullOrWhiteSpace(publishedBy) ? null : publishedBy.Trim(),
            Lanes = sorted,
            Requirements = requirements ?? [],
            Short = string.IsNullOrWhiteSpace(shortTitle) ? null : shortTitle.Trim(),
            Review = review,
            SetUpIn = setUpIn,
            Parent = parent,
            Weight = string.IsNullOrWhiteSpace(weight) ? null : weight.Trim(),
            Paths = paths ?? [],
            Request = request,
        };

        return await PublishInAsync(tx, asked).ConfigureAwait(false);
    }

    /// <summary>
    /// Append a quest's `published` and write its row from the replay, inside the write. A <paramref name="note"/> stays in the history
    /// and never on the quest: what a chain step's publish says of what it inherited (EVID1a).
    /// </summary>
    private static async Task<Quest> PublishInAsync(QuestTx tx, Quest asked, string? note = null)
    {
        var published = await tx.AppendAsync(
            new QuestOperation(asked.Id, QuestOperationKind.Published, tx.Machine, 0, asked.Filed, note, asked)).ConfigureAwait(false);
        var quest = QuestLog.Replay([published])!;
        await tx.WriteCacheAsync(quest).ConfigureAwait(false);
        return quest;
    }

    /// <summary>
    /// The chain's next step after <paramref name="closed"/>, published in the caller's transaction, or the one already published,
    /// since the id is the ask; null when the chain ends here.
    /// </summary>
    internal async Task<Quest?> PublishNextAsync(QuestTx tx, Quest closed, DateTimeOffset now)
    {
        if (NextStep(closed, now) is not { } next) return null;
        if (await tx.FindAsync(next.Step.Id).ConfigureAwait(false) is not { } standing)
        {
            return await PublishInAsync(tx, next.Step, next.Left).ConfigureAwait(false);
        }

        // FABRIC-CLOSE1c: a step its source's reopen withdrew, never taken, is the same step again: rebound to the submission that
        // publishes it now, keeping its id and its history, so no second quest is asked and no withdrawal outlives the work it named.
        // A started one waits for the person's word (ContinueAsync). FABRIC-CLOSE1c15: the rebind is judged on the step this machine
        // reads open, so it is marked while-open, and never lets go a step another machine's take reached first, here or at the remote.
        if (standing is { Status: QuestStatus.Open, SourceReopened: not null } && next.Step.GrewFrom is { } from)
        {
            return await tx.ReboundInAsync(standing.Id, from, whileOpen: true, now).ConfigureAwait(false) ?? standing;
        }

        return standing;
    }

    /// <summary>The chain's next step <paramref name="quest"/>'s close or submission published, where it stands; else null.</summary>
    internal async Task<Quest?> SuccessorAsync(QuestTx tx, Quest quest) =>
        NextStep(quest, quest.Updated) is { } next ? await tx.FindAsync(next.Step.Id).ConfigureAwait(false) : null;

    private static (Quest Step, string? Left)? NextStep(Quest parent, DateTimeOffset now)
    {
        if (parent.Then.Count == 0) return null;
        var step = parent.Then[0];
        var title = step.Title.Replace("{parent}", $"#{parent.Id}", StringComparison.Ordinal);
        var sameTree = string.Equals(step.To, parent.To, StringComparison.OrdinalIgnoreCase);
        var left = sameTree
            ? []
            : parent.Requirements.Select((requirement, index) => (requirement, Number: index + 1))
                .Where(each => each.requirement.Evidence.Count > 0).Select(each => each.Number).ToList();
        var next = new Quest(
            QuestStore.MakeId(parent.From, step.To, title, parent.Id), parent.From, step.To, title,
            step.Body.Replace("{parent}", $"#{parent.Id}", StringComparison.Ordinal),
            QuestStatus.Open, null, now, now, Workspace: parent.Workspace)
        {
            Then = parent.Then.Skip(1).ToList(),
            Parent = parent.Id,
            // The submission that publishes it (FABRIC-CLOSE1c, §E5): what a reopen names it by, and what seals it. A done names none.
            GrewFrom = parent is { Status: QuestStatus.Taken, Submission: { } from } ? new QuestOperationRef(from.Machine, from.Sequence) : null,
            Requirements = left.Count == 0
                ? parent.Requirements
                : [.. parent.Requirements.Select(requirement => requirement with { Evidence = [] })],
            // The chain's review choice is every step's (REVIEWENV1b, D154 point 3), and a set-up step is one by its step.
            Review = parent.Review,
            SetUpIn = step.SetUpIn,
        };

        return (next, left.Count == 0
            ? null
            : $"{(left.Count == 1 ? "requirement" : "requirements")} {string.Join(", ", left)} named evidence in `{parent.To}`'s "
              + $"tree; this step asks `{step.To}`, so it inherits {(left.Count == 1 ? "it" : "them")} without the evidence.");
    }
}
