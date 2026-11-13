using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>A plan revision as the store keeps it and the wire carries it: written and read by hand, one shape.</summary>
internal static class TeamPlanCodec
{
    public static string Write(TeamPlan plan) => JsonFields.Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("ask", plan.Ask);
        if (plan.Scope is { } scope) writer.WriteString("scope", scope);
        writer.WriteNumber("revision", plan.Revision);
        if (plan.By is { } by) writer.WriteString("by", by);
        writer.WriteString("why", plan.Why);
        writer.WriteString("at", Moments.Write(plan.At));
        if (plan.Base is { } baseCommit) writer.WriteString("base", baseCommit);
        writer.WriteNumber("depth", plan.Depth);
        Array(writer, "parts", plan.Parts, WritePart);
        Array(writer, "edges", plan.Edges, WriteEdge);
        Array(writer, "flags", plan.Flags, (w, flag) =>
        {
            w.WriteStartObject();
            w.WriteString("kind", flag.Kind);
            if (flag.Part is { } part) w.WriteString("part", part);
            w.WriteString("text", flag.Text);
            w.WriteEndObject();
        });
        writer.WriteEndObject();
    });

    public static TeamPlan? Read(string json)
    {
        if (JsonFields.ParseObject(json) is not { } root || JsonFields.Text(root, "ask") is not { } ask) return null;
        return new TeamPlan(
            ask, JsonFields.Text(root, "scope"), (int)(JsonFields.Number(root, "revision") ?? 1), JsonFields.Text(root, "by"),
            JsonFields.Text(root, "why") ?? "", Moments.Read(root, "at") ?? DateTimeOffset.UnixEpoch)
        {
            Base = JsonFields.Text(root, "base"),
            Depth = (int)(JsonFields.Number(root, "depth") ?? 1),
            Parts = [.. JsonFields.Items(root, "parts").Select(ReadPart)],
            Edges = [.. JsonFields.Items(root, "edges").Select(edge => new TeamEdge(
                JsonFields.Text(edge, "part") ?? "", JsonFields.Text(edge, "on") ?? "", JsonFields.Text(edge, "kind") ?? TeamEdge.After,
                JsonFields.Text(edge, "milestone"), JsonFields.Text(edge, "why") ?? ""))],
            Flags = [.. JsonFields.Items(root, "flags").Select(flag => new TeamFlag(
                JsonFields.Text(flag, "kind") ?? "", JsonFields.Text(flag, "part"), JsonFields.Text(flag, "text") ?? ""))],
        };
    }

    private static void WritePart(Utf8JsonWriter writer, TeamPart part)
    {
        writer.WriteStartObject();
        writer.WriteString("id", part.Id);
        writer.WriteString("repository", part.Repository);
        writer.WriteString("title", part.Title);
        writer.WriteString("body", part.Body);
        writer.WriteString("outcome", part.Outcome);
        if (part.Parent is { } parent) writer.WriteString("parent", parent);
        if (part.Weight is { } weight) writer.WriteString("weight", weight);
        Strings(writer, "lanes", part.Lanes);
        Strings(writer, "claims", part.Claims);
        Strings(writer, "reads", part.Reads);
        Strings(writer, "proof", part.Proof);
        writer.WritePropertyName("requirements");
        QuestRecordCodec.WriteRequirements(writer, part.Requirements);
        if (part.Pack is { } pack)
        {
            writer.WritePropertyName("pack");
            writer.WriteStartObject();
            Array(writer, "pointers", pack.Pointers, (w, pointer) =>
            {
                w.WriteStartObject();
                w.WriteString("role", pointer.Role);
                w.WriteString("file", pointer.File);
                w.WriteString("range", pointer.Range);
                w.WriteString("blob", pointer.Blob);
                w.WriteEndObject();
            });
            Strings(writer, "tests", pack.Tests);
            Strings(writer, "words", pack.Words);
            Strings(writer, "generated", pack.Generated);
            Strings(writer, "shared", pack.Shared);
            Strings(writer, "unknowns", pack.Unknowns);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static TeamPart ReadPart(JsonElement part) =>
        new(JsonFields.Text(part, "id") ?? "", JsonFields.Text(part, "repository") ?? "", JsonFields.Text(part, "title") ?? "",
            JsonFields.Text(part, "body") ?? "", JsonFields.Text(part, "outcome") ?? "")
        {
            Parent = JsonFields.Text(part, "parent"),
            Weight = JsonFields.Text(part, "weight"),
            Lanes = Strings(part, "lanes"),
            Claims = Strings(part, "claims"),
            Reads = Strings(part, "reads"),
            Proof = Strings(part, "proof"),
            Requirements = part.TryGetProperty("requirements", out var requirements) && requirements.ValueKind == JsonValueKind.Array
                ? QuestRecordCodec.ReadRequirements(requirements)
                : [],
            Pack = part.TryGetProperty("pack", out var pack) && pack.ValueKind == JsonValueKind.Object
                ? new PartPack
                {
                    Pointers = [.. JsonFields.Items(pack, "pointers").Select(pointer => new PackPointer(
                        JsonFields.Text(pointer, "role") ?? "", JsonFields.Text(pointer, "file") ?? "",
                        JsonFields.Text(pointer, "range") ?? "", JsonFields.Text(pointer, "blob") ?? ""))],
                    Tests = Strings(pack, "tests"),
                    Words = Strings(pack, "words"),
                    Generated = Strings(pack, "generated"),
                    Shared = Strings(pack, "shared"),
                    Unknowns = Strings(pack, "unknowns"),
                }
                : null,
        };

    private static void WriteEdge(Utf8JsonWriter writer, TeamEdge edge)
    {
        writer.WriteStartObject();
        writer.WriteString("part", edge.Part);
        writer.WriteString("on", edge.On);
        writer.WriteString("kind", edge.Kind);
        if (edge.Milestone is { } milestone) writer.WriteString("milestone", milestone);
        writer.WriteString("why", edge.Why);
        writer.WriteEndObject();
    }

    private static void Array<T>(Utf8JsonWriter writer, string name, IEnumerable<T> items, Action<Utf8JsonWriter, T> write)
    {
        writer.WriteStartArray(name);
        foreach (var item in items) write(writer, item);
        writer.WriteEndArray();
    }

    private static void Strings(Utf8JsonWriter writer, string name, IEnumerable<string> items) =>
        Array(writer, name, items, (w, item) => w.WriteStringValue(item));

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        [.. JsonFields.Items(element, name).Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? "")];
}
