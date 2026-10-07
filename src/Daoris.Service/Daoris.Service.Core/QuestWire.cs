using System.Text.Json;
using static Daoris.Knowledge.JsonFields;

namespace Daoris.Knowledge;

/// <summary>
/// Quest operations on the wire (D68, sync design §8) — written once, here, and read by both sides: the
/// remote's doors and the machine's client that speaks to them. Two hands rolling the same shape is two
/// shapes that drift.
/// </summary>
/// <remarks>
/// <para>Machine and sequence name an operation anywhere; <c>number</c> is where the remote placed it,
/// absent while it is pending. A publish carries its ask and NO workspace: the receiving side files it
/// by its own wiring (SYNC0a). Files travel by name — there is no field for bytes, a root or a
/// transcript. Absent, not policed (D47 §4).</para>
///
/// <para>Hand-written rather than reflected, for the reason every JSON shape in Core is: nothing here may
/// quietly stop working under AOT.</para>
/// </remarks>
public static class QuestWire
{
    /// <summary>What a door says when an operation arrives half-made.</summary>
    public const string Shape =
        "every operation names its machine, sequence, quest, kind and time; a publish carries from, to, title and "
        + "body, every file its name, sha256 and size, every step its to, title and body, and every requirement "
        + "its quote and check, and any evidence at most 5 items, each a repository-relative path or a gate's name; "
        + "a done's every answer names its requirement and says met or departed, a departure "
        + "with its quote; a decline's whileOpen, where it says one, is true or false; a conflict names "
        + "what it attempted; a dismissal names the conflict's machine and sequence; an evidenced verdict names "
        + "the full commit read, how it was read, and each item's requirement, path or gate, and result, with a "
        + "spelling only on a `case` read";

    /// <summary>A page of what a remote accepted — the answer to a fetch.</summary>
    public static string Page(QuestFetch page) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("through", page.Through);
        writer.WriteBoolean("more", page.More);
        writer.WriteStartArray("operations");
        foreach (var operation in page.Operations) Write(writer, operation);
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>A page read back, or null when it is not one.</summary>
    public static QuestFetch? ReadPage(string json)
    {
        if (ParseObject(json) is not { } root) return null;
        if (Operations(root, numbered: true) is not { } operations) return null;
        return new QuestFetch(
            operations,
            root.TryGetProperty("through", out var through) && through.ValueKind == JsonValueKind.Number ? through.GetInt64() : 0,
            root.TryGetProperty("more", out var more) && more.ValueKind == JsonValueKind.True);
    }

    /// <summary>A push: what is pending, rebased on <paramref name="base"/>.</summary>
    public static string Push(long @base, IReadOnlyList<QuestOperation> operations) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("base", @base);
        writer.WriteStartArray("operations");
        foreach (var operation in operations) Write(writer, operation with { Number = null });
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>A push read back at the remote, or null when any operation in it is not whole.</summary>
    public static (long Base, IReadOnlyList<QuestOperation> Operations)? ReadPush(string json)
    {
        if (ParseObject(json) is not { } root) return null;
        if (Operations(root, numbered: false) is not { } operations) return null;
        return (root.TryGetProperty("base", out var @base) && @base.ValueKind == JsonValueKind.Number ? @base.GetInt64() : 0,
            operations);
    }

    /// <summary>The remote's answer to a push.</summary>
    public static string Pushed(QuestPush push) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("accepted");
        foreach (var acceptance in push.Accepted)
        {
            writer.WriteStartObject();
            writer.WriteString("machine", acceptance.Machine);
            writer.WriteNumber("sequence", acceptance.Sequence);
            writer.WriteNumber("number", acceptance.Number);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("behind");
        foreach (var quest in push.Behind) writer.WriteStringValue(quest);
        writer.WriteEndArray();
        writer.WriteStartArray("refused");
        foreach (var refusal in push.Refused)
        {
            writer.WriteStartObject();
            writer.WriteString("quest", refusal.Quest);
            writer.WriteString("reason", refusal.Reason);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>A push's answer read back, or null when it is not one.</summary>
    public static QuestPush? ReadPushed(string json)
    {
        if (ParseObject(json) is not { } root) return null;
        var accepted = new List<QuestAcceptance>();
        foreach (var item in Items(root, "accepted"))
        {
            if (Text(item, "machine") is not { Length: > 0 } machine || Number(item, "sequence") is not { } sequence
                || Number(item, "number") is not { } number)
            {
                return null;
            }

            accepted.Add(new(machine, sequence, number));
        }

        return new QuestPush(
            accepted,
            Items(root, "behind").Select(b => b.ValueKind == JsonValueKind.String ? b.GetString() : null).OfType<string>().ToList(),
            Items(root, "refused").Select(r => new QuestPushRefusal(Text(r, "quest") ?? "", Text(r, "reason") ?? "")).ToList());
    }

    private static void Write(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Number is { } number) writer.WriteNumber("number", number);
        writer.WriteString("machine", operation.Machine);
        writer.WriteNumber("sequence", operation.Sequence);
        writer.WriteString("quest", operation.Quest);
        writer.WriteString("kind", operation.Kind.ToString().ToLowerInvariant());
        writer.WriteString("at", operation.At.ToString("O"));
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        // Only when set, and only on a decline (PAUSE1c): a plain decline crosses exactly as it did, and an older
        // build reads a flagged one as a plain one.
        if (operation is { WhileOpen: true, Kind: QuestOperationKind.Declined }) writer.WriteBoolean("whileOpen", true);
        // Only when it answers some (DRIFT1d): a done on a quest with none crosses exactly as it did.
        if (operation.Answers is { Count: > 0 } answers)
        {
            writer.WritePropertyName("answers");
            QuestStore.WriteAnswers(writer, answers);
        }

        // An evidenced operation's verdict (EVID1a): names and codes only; its when and machine are the operation's.
        if (operation is { Kind: QuestOperationKind.Evidenced, Evidence: { } verdict })
        {
            writer.WritePropertyName("evidence");
            verdict.Write(writer);
        }

        if (operation.Attempted is { } attempted) writer.WriteString("attempted", attempted.ToString());
        if (operation.Dismisses is { } named)
        {
            writer.WriteStartObject("dismisses");
            writer.WriteString("machine", named.Machine);
            writer.WriteNumber("sequence", named.Sequence);
            writer.WriteEndObject();
        }
        if (operation.Published is { } asked)
        {
            writer.WriteStartObject("asked");
            writer.WriteString("from", asked.From);
            writer.WriteString("to", asked.To);
            writer.WriteString("title", asked.Title);
            writer.WriteString("body", asked.Body);
            if (asked.Parent is not null) writer.WriteString("parent", asked.Parent);
            if (asked.PublishedBy is not null) writer.WriteString("publishedBy", asked.PublishedBy);
            // Only when it names some (D115 §2.2): an older build reads a quest with lanes as a quest to
            // the whole repository, and a quest to the whole repository crosses exactly as it did.
            if (asked.Lanes.Count > 0)
            {
                writer.WriteStartArray("lanes");
                foreach (var lane in asked.Lanes) writer.WriteStringValue(lane);
                writer.WriteEndArray();
            }

            // Only when it names some (DRIFT1c): a quest with none crosses exactly as it did, and an older
            // build reads one with them as the quest it always was.
            if (asked.Requirements.Count > 0)
            {
                writer.WritePropertyName("requirements");
                QuestStore.WriteRequirements(writer, asked.Requirements);
            }

            // Only when its publisher gave one (SESSUX1j): an older build reads the quest as it always did, named by its title.
            if (asked.Short is not null) writer.WriteString("short", asked.Short);

            writer.WriteStartArray("links");
            foreach (var link in asked.Links) writer.WriteStringValue(link);
            writer.WriteEndArray();
            writer.WriteStartArray("attachments");
            foreach (var file in asked.Attachments) file.Write(writer);
            writer.WriteEndArray();
            writer.WriteStartArray("then");
            foreach (var step in asked.Then)
            {
                writer.WriteStartObject();
                writer.WriteString("to", step.To);
                writer.WriteString("title", step.Title);
                writer.WriteString("body", step.Body);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// The operations under <c>operations</c> — null when any is not whole, because an operation half-read
    /// is one a store would replay as something nobody made. A publish arrives in the default circle; the
    /// store files it by the receiving side's wiring.
    /// </summary>
    private static List<QuestOperation>? Operations(JsonElement root, bool numbered)
    {
        var operations = new List<QuestOperation>();
        foreach (var item in Items(root, "operations"))
        {
            if (Read(item, numbered) is not { } operation) return null;
            operations.Add(operation);
        }

        return operations;
    }

    private static QuestOperation? Read(JsonElement item, bool numbered)
    {
        if (item.ValueKind != JsonValueKind.Object
            || Text(item, "machine") is not { Length: > 0 } machine
            || Number(item, "sequence") is not { } sequence
            || Text(item, "quest") is not { Length: > 0 } quest
            || Text(item, "at") is not { } atText || !DateTimeOffset.TryParse(atText, out var at)
            || !Enum.TryParse<QuestOperationKind>(Text(item, "kind") ?? "", ignoreCase: true, out var kind)
            || !Enum.IsDefined(kind))
        {
            return null;
        }

        var number = Number(item, "number");
        if (numbered && number is null) return null;

        QuestStatus? attempted = null;
        if (kind == QuestOperationKind.Conflict)
        {
            if (!Enum.TryParse<QuestStatus>(Text(item, "attempted") ?? "", ignoreCase: true, out var lost)
                || !Enum.IsDefined(lost))
            {
                return null;
            }

            attempted = lost;
        }

        // A dismissal that names no conflict would dismiss nothing on every machine it reached, which is
        // a half-made operation, not a harmless one.
        QuestOperationRef? dismisses = null;
        if (kind == QuestOperationKind.Dismissed)
        {
            if (!item.TryGetProperty("dismisses", out var named) || named.ValueKind != JsonValueKind.Object
                || Text(named, "machine") is not { Length: > 0 } conflictMachine
                || Number(named, "sequence") is not { } conflictSequence)
            {
                return null;
            }

            dismisses = new QuestOperationRef(conflictMachine, conflictSequence);
        }

        Quest? published = null;
        if (kind == QuestOperationKind.Published)
        {
            if (!item.TryGetProperty("asked", out var asked) || asked.ValueKind != JsonValueKind.Object
                || Text(asked, "from") is not { } from || Text(asked, "to") is not { } to
                || Text(asked, "title") is not { } title || Text(asked, "body") is not { } body)
            {
                return null;
            }

            var files = new List<QuestAttachment>();
            foreach (var file in Items(asked, "attachments"))
            {
                if (Text(file, "name") is not { } name || Text(file, "sha256") is not { } sha || Number(file, "bytes") is not { } size)
                {
                    return null;
                }

                files.Add(new(name, sha, size));
            }

            var steps = new List<QuestStep>();
            foreach (var step in Items(asked, "then"))
            {
                if (Text(step, "to") is not { } stepTo || Text(step, "title") is not { } stepTitle
                    || Text(step, "body") is not { } stepBody)
                {
                    return null;
                }

                steps.Add(new(stepTo, stepTitle, stepBody));
            }

            // A lane that is not a string is half an address, and a quest half-addressed is not whole.
            var lanes = Items(asked, "lanes");
            if (lanes.Any(lane => lane.ValueKind != JsonValueKind.String)) return null;

            // A requirement without its words or its check is half of one (DRIFT1c), never replayed blank. So is
            // evidence naming both or neither, or a path that is not repository-relative (EVID1a): a machine's path
            // never crosses (D47 §4).
            var requirements = new List<QuestRequirement>();
            foreach (var requirement in Items(asked, "requirements"))
            {
                if (Text(requirement, "quote") is not { } quote || Text(requirement, "check") is not { } check) return null;
                IReadOnlyList<QuestEvidence> evidence = [];
                if (requirement.TryGetProperty("evidence", out var named))
                {
                    if (QuestEvidence.Judged(named) is not { } judged) return null;
                    evidence = judged;
                }

                requirements.Add(new(quote, check) { Evidence = evidence });
            }

            published = new Quest(quest, from, to, title, body, QuestStatus.Open, null, at, at)
            {
                Links = Items(asked, "links").Select(l => l.ValueKind == JsonValueKind.String ? l.GetString() : null)
                    .OfType<string>().ToList(),
                Attachments = files,
                Then = steps,
                Parent = Text(asked, "parent"),
                PublishedBy = Text(asked, "publishedBy"),
                Lanes = lanes.Select(lane => lane.GetString()!).ToList(),
                Requirements = requirements,
                Short = Text(asked, "short"),
            };
        }

        // An answer that names no requirement, says both or neither, or departs without the person's words is half
        // of one (DRIFT1d), and a replay would hold the quest, or release it, on something nobody said.
        List<QuestAnswer>? answers = null;
        if (item.TryGetProperty("answers", out var answered))
        {
            if (answered.ValueKind != JsonValueKind.Array) return null;
            answers = [];
            foreach (var answer in answered.EnumerateArray())
            {
                if (Number(answer, "requirement") is not { } requirement || requirement < 1 || requirement > int.MaxValue) return null;
                var met = Text(answer, "met");
                var departed = Text(answer, "departed");
                var quote = Text(answer, "quote");
                if ((met is null) == (departed is null) || (departed is not null && quote is null)) return null;
                answers.Add(new QuestAnswer((int)requirement, met, departed, quote));
            }
        }

        // A decline's flag (PAUSE1c) is true or false. Anything else is half of one, and read as plain it would
        // decline over a take — the one thing the flag exists to stop.
        var whileOpen = false;
        if (kind == QuestOperationKind.Declined && item.TryGetProperty("whileOpen", out var open))
        {
            if (open.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
            whileOpen = open.ValueKind == JsonValueKind.True;
        }

        // A verdict is an evidenced operation's whole point (EVID1a): one missing, or not whole, makes the operation half
        // of one, which a replay would read as evidence found or missing on nobody's reading.
        QuestEvidenceVerdict? verdict = null;
        if (kind == QuestOperationKind.Evidenced
            && (!item.TryGetProperty("evidence", out var read) || (verdict = QuestEvidenceVerdict.Judged(read)) is null))
        {
            return null;
        }

        return new QuestOperation(
            quest, kind, machine, sequence, at, Text(item, "note"), published, attempted, numbered ? number : null,
            dismisses, answers is { Count: > 0 } ? answers : null, whileOpen, verdict);
    }

}
