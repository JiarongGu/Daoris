using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A design pass's comparison in the brainstorm's document: its JSON beside the rows, and its section of the Markdown.</summary>
public static class DesignDocuments
{
    /// <summary>The comparison as <c>comparison</c>; nothing for a brainstorm's document, so an older reader reads it as it did.</summary>
    public static void Json(Utf8JsonWriter writer, DesignComparison? comparison)
    {
        if (comparison is null) return;
        writer.WriteStartObject("comparison");
        writer.WriteString("route", comparison.Route);
        writer.WriteString("why", comparison.Why);
        Strings(writer, "flips", comparison.Flips);
        writer.WriteStartObject("trial");
        writer.WriteString("area", comparison.Trial.Area);
        writer.WriteString("baseline", comparison.Trial.Baseline);
        Strings(writer, "keep", comparison.Trial.Keep);
        Strings(writer, "retire", comparison.Trial.Retire);
        writer.WriteEndObject();
        writer.WriteStartArray("questions");
        foreach (var question in comparison.Questions)
        {
            writer.WriteStartObject();
            writer.WriteString("key", question.Key);
            writer.WriteString("question", question.Question);
            writer.WriteString("yes", question.Yes);
            writer.WriteString("no", question.No);
            writer.WriteString("where", question.Where);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>The comparison's section, in Daoris's fixed words around the reader's own: a claim, never the decision.</summary>
    public static void Markdown(StringBuilder text, DesignComparison? comparison)
    {
        if (comparison is null) return;
        text.Append("\n## The comparison\n\n");
        text.Append("The comparison reader's claim, never a decision: each question below is the owner's, and its answer is recorded as one.\n\n");
        text.Append("**Route:** ").Append(comparison.Route).Append("\n\n").Append(comparison.Why).Append("\n\n");
        text.Append("**What would flip it:**\n\n");
        foreach (var flip in comparison.Flips) text.Append("- ").Append(flip).Append('\n');
        text.Append("\n**The trial,** written before it starts: on ").Append(comparison.Trial.Area).Append(", against ")
            .Append(comparison.Trial.Baseline).Append(".\n\nKept if:\n\n");
        foreach (var keep in comparison.Trial.Keep) text.Append("- ").Append(keep).Append('\n');
        text.Append("\nRetired if:\n\n");
        foreach (var retire in comparison.Trial.Retire) text.Append("- ").Append(retire).Append('\n');
        if (comparison.Questions.Count == 0)
        {
            text.Append("\nThe reader asks the owner nothing.\n");
            return;
        }

        text.Append("\n**The owner's questions:**\n\n");
        foreach (var question in comparison.Questions)
        {
            text.Append("- `").Append(question.Key).Append("` ").Append(question.Question).Append(" Yes: ").Append(question.Yes)
                .Append(" No: ").Append(question.No).Append(" Where to look: ").Append(question.Where).Append('\n');
        }
    }

    private static void Strings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }
}
