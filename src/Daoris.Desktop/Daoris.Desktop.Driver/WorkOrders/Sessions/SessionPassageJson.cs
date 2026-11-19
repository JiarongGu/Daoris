using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A row's <c>passage</c> as the groups' answer writes it: its run, workflow and version, and each step with its mark, its role, its newest
/// attempt's number, start and failure and the attempts before it; null for none.
/// </summary>
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
            if (step.Role is { } role) writer.WriteString("role", role);
            if (step.Attempt is { } attempt) writer.WriteNumber("attempt", attempt);
            if (step.Since is { } since) writer.WriteString("since", since);
            if (step.Code is { } code) writer.WriteString("code", code);
            if (step.Earlier is { Count: > 0 } earlier)
            {
                writer.WriteStartArray("earlier");
                foreach (var each in earlier)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("attempt", each.Attempt);
                    writer.WriteString("at", each.At);
                    if (each.Outcome is { } outcome) writer.WriteString("outcome", outcome);
                    if (each.Code is { } failed) writer.WriteString("code", failed);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
