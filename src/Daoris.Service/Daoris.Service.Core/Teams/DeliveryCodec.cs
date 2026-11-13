using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>The delivery part of a plan revision, written and read by hand beside the rest of the plan's record.</summary>
internal static class DeliveryCodec
{
    public static void Write(Utf8JsonWriter writer, TeamPlan plan)
    {
        if (plan.Deployables.Count == 0 && plan.Releases.Count == 0 && plan.Questions.Count == 0) return;
        Array(writer, "deployables", plan.Deployables, (w, each) =>
        {
            w.WriteStartObject();
            Ref(w, each.Ref);
            Evidence(w, "declared", each.Declared);
            Array(w, "provides", each.Provides, Use);
            Array(w, "consumes", each.Consumes, Use);
            w.WriteEndObject();
        });
        Array(writer, "releases", plan.Releases, (w, edge) =>
        {
            w.WriteStartObject();
            w.WritePropertyName("deployable");
            RefObject(w, edge.Deployable);
            w.WritePropertyName("on");
            RefObject(w, edge.On);
            w.WriteString("why", edge.Why);
            w.WriteString("source", edge.Source);
            if (edge.Evidence is { } evidence) Evidence(w, "evidence", evidence);
            w.WriteEndObject();
        });
        Array(writer, "questions", plan.Questions, (w, question) =>
        {
            w.WriteStartObject();
            w.WriteString("key", question.Key);
            w.WriteString("kind", question.Kind);
            w.WriteString("text", question.Text);
            w.WriteString("yes", question.Yes);
            w.WriteString("blocks", question.Blocks);
            w.WriteString("default", question.Default);
            w.WriteEndObject();
        });
    }

    public static TeamPlan Read(JsonElement root, TeamPlan plan) => plan with
    {
        Deployables = [.. JsonFields.Items(root, "deployables").Select(each => new DeliveryDeployable(ReadRef(each), ReadEvidence(each, "declared") ?? new("", "", "", ""))
        {
            Provides = [.. JsonFields.Items(each, "provides").Select(ReadUse)],
            Consumes = [.. JsonFields.Items(each, "consumes").Select(ReadUse)],
        })],
        Releases = [.. JsonFields.Items(root, "releases").Select(edge => new DeliveryEdge(
            ReadRefObject(edge, "deployable"), ReadRefObject(edge, "on"), JsonFields.Text(edge, "why") ?? "",
            JsonFields.Text(edge, "source") ?? DeliveryEdge.Policy, ReadEvidence(edge, "evidence")))],
        Questions = [.. JsonFields.Items(root, "questions").Select(question => new DeliveryQuestion(
            JsonFields.Text(question, "key") ?? "", JsonFields.Text(question, "kind") ?? DeliveryQuestion.Open, JsonFields.Text(question, "text") ?? "")
        {
            Yes = JsonFields.Text(question, "yes") ?? "", Blocks = JsonFields.Text(question, "blocks") ?? "", Default = JsonFields.Text(question, "default") ?? "",
        })],
    };

    private static void Ref(Utf8JsonWriter writer, DeliveryRef each)
    {
        writer.WriteString("repository", each.Repository);
        writer.WriteString("id", each.Id);
    }

    private static void RefObject(Utf8JsonWriter writer, DeliveryRef each)
    {
        writer.WriteStartObject();
        Ref(writer, each);
        writer.WriteEndObject();
    }

    private static void Evidence(Utf8JsonWriter writer, string name, DeliveryEvidence evidence)
    {
        writer.WriteStartObject(name);
        writer.WriteString("repository", evidence.Repository);
        writer.WriteString("file", evidence.File);
        writer.WriteString("range", evidence.Range);
        writer.WriteString("blob", evidence.Blob);
        writer.WriteEndObject();
    }

    private static void Use(Utf8JsonWriter writer, DeliveryUse use)
    {
        writer.WriteStartObject();
        writer.WriteString("resource", use.Resource);
        writer.WriteString("mode", use.Mode);
        writer.WriteEndObject();
    }

    private static DeliveryRef ReadRef(JsonElement each) => new(JsonFields.Text(each, "repository") ?? "", JsonFields.Text(each, "id") ?? "");

    private static DeliveryRef ReadRefObject(JsonElement each, string name) =>
        each.TryGetProperty(name, out var inner) && inner.ValueKind == JsonValueKind.Object ? ReadRef(inner) : new("", "");

    private static DeliveryEvidence? ReadEvidence(JsonElement each, string name) =>
        each.TryGetProperty(name, out var inner) && inner.ValueKind == JsonValueKind.Object
            ? new(JsonFields.Text(inner, "repository") ?? "", JsonFields.Text(inner, "file") ?? "", JsonFields.Text(inner, "range") ?? "", JsonFields.Text(inner, "blob") ?? "")
            : null;

    private static DeliveryUse ReadUse(JsonElement use) => new(JsonFields.Text(use, "resource") ?? "", JsonFields.Text(use, "mode") ?? "");

    private static void Array<T>(Utf8JsonWriter writer, string name, IEnumerable<T> items, Action<Utf8JsonWriter, T> write)
    {
        writer.WriteStartArray(name);
        foreach (var item in items) write(writer, item);
        writer.WriteEndArray();
    }
}
