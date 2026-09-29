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
        + "body, every file its name, sha256 and size, and every step its to, title and body; a conflict names "
        + "what it attempted; a dismissal names the conflict's machine and sequence";

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

            published = new Quest(quest, from, to, title, body, QuestStatus.Open, null, at, at)
            {
                Links = Items(asked, "links").Select(l => l.ValueKind == JsonValueKind.String ? l.GetString() : null)
                    .OfType<string>().ToList(),
                Attachments = files,
                Then = steps,
                Parent = Text(asked, "parent"),
                PublishedBy = Text(asked, "publishedBy"),
            };
        }

        return new QuestOperation(
            quest, kind, machine, sequence, at, Text(item, "note"), published, attempted, numbered ? number : null,
            dismisses);
    }

}
