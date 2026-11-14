using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A row's <c>passage</c> as the groups' answer writes it: its run, workflow and version, and each step with its mark; null for none.</summary>
internal static class SessionPassageJson
{
    public static void Write(Utf8JsonWriter writer, RunPassage? passage)
    {
        if (passage is null)
        {
            writer.WriteNull("passage");
            return;
        }

        writer.WriteStartObject("passage");
        writer.WriteString("run", passage.Run);
        writer.WriteString("workflow", passage.Workflow);
        if (passage.Version is { } version) writer.WriteNumber("version", version); else writer.WriteNull("version");
        writer.WriteStartArray("steps");
        foreach (var step in passage.Steps)
        {
            writer.WriteStartObject();
            writer.WriteString("node", step.Node);
            writer.WriteString("kind", step.Kind);
            writer.WriteString("mark", step.Mark);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
