using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A brainstorm pass's two result tools: a generator gives its proposals and the synthesis its ranking, each once, into the one answer file the driver's spawn names for the turn. Each answers only a turn of its own kind,
/// judges the answer as the driver will and says a refusal's codes back, and is a pass's only connector tool: nothing here reads,
/// asks, publishes or changes anything else.
/// </summary>
/// <param name="brainstorms">The turn this connector serves, as the driver named it; every other session's names none.</param>
[McpServerToolType]
public sealed class BrainstormTools(BrainstormBox? brainstorms = null)
{
    [McpServerTool(Name = "brainstorm_give")]
    [Description(
        "Only for a turn Daoris started to propose answers to a brainstorm's question: give your proposals, once, before you end, "
        + "at most eight, no idea twice (unknown is an answer on a scale: never guess the best value). With none, say why in `none`. "
        + "A refusal says why by code: give it again, whole. Your proposals are claims a person reads; this changes nothing, "
        + "and nobody can ask you anything after.")]
    public string Give(
        [Description("Your proposals, at most eight, the strongest first; none where you have no proposal.")]
        BrainstormProposalInput[]? proposals,
        [Description("What you would need that the packet lacks, at most five of 200 characters each.")]
        string[]? needs = null,
        [Description("Only with no proposal: why there is none, in at most 200 characters.")]
        string? none = null)
    {
        var text = Composed(writer =>
        {
            writer.WriteStartArray("proposals");
            foreach (var proposal in proposals ?? [])
            {
                if (proposal is null)
                {
                    writer.WriteNullValue();
                    continue;
                }

                writer.WriteStartObject();
                Words(writer, "idea", proposal.Idea);
                Words(writer, "rationale", proposal.Rationale);
                Words(writer, "effort", proposal.Effort);
                Words(writer, "risk", proposal.Risk);
                Words(writer, "reversible", proposal.Reversible);
                Words(writer, "undo", proposal.Undo);
                Words(writer, "confidence", proposal.Confidence);
                List(writer, "assumptions", proposal.Assumptions);
                List(writer, "sources", proposal.Sources);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            List(writer, "needs", needs ?? []);
            Words(writer, "none", none);
        });
        return (brainstorms ?? BrainstormBox.FromEnvironment()).Give(text).Message;
    }

    [McpServerTool(Name = "brainstorm_synthesize")]
    [Description(
        "Only for a turn Daoris started to synthesize a brainstorm's proposals: give your ranking, once, before you end. Group "
        + "proposals that are the same idea by anonymous id (P1 and on); every id appears exactly once, in a group or left out with "
        + "why. A refusal says why by code: give it again, whole. Your ranking is a claim a person reads; this changes nothing, "
        + "and nobody can ask you anything after.")]
    public string Synthesize(
        [Description("Your groups, each with its members by id and its rank.")]
        BrainstormGroupInput[]? groups,
        [Description("The proposals you left out of every group, each by id with why.")]
        BrainstormLeftOutInput[]? leftOut = null)
    {
        var text = Composed(writer =>
        {
            writer.WriteStartArray("groups");
            foreach (var group in groups ?? [])
            {
                if (group is null)
                {
                    writer.WriteNullValue();
                    continue;
                }

                writer.WriteStartObject();
                Words(writer, "title", group.Title);
                Words(writer, "idea", group.Idea);
                List(writer, "members", group.Members);
                if (group.Rank is { } rank) writer.WriteNumber("rank", rank);
                else writer.WriteNull("rank");
                Words(writer, "why", group.Why);
                writer.WriteStartArray("flags");
                foreach (var flag in group.Flags ?? [])
                {
                    writer.WriteStartObject();
                    Words(writer, "flag", flag?.Flag);
                    Words(writer, "why", flag?.Why);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("leftOut");
            foreach (var left in leftOut ?? [])
            {
                writer.WriteStartObject();
                Words(writer, "id", left?.Id);
                Words(writer, "why", left?.Why);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
        return (brainstorms ?? BrainstormBox.FromEnvironment()).Synthesize(text).Message;
    }

    /// <summary>
    /// The answer as the driver reads it, <c>{ "brainstorm": 1, … }</c>, written by hand as every store here is (nothing may stop
    /// working under AOT): a part left out is written null, so the validator names it rather than a default standing in for it.
    /// </summary>
    private static string Composed(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("brainstorm", 1);
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
    }

    private static void Words(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }

    private static void List(Utf8JsonWriter writer, string name, IEnumerable<string?>? values)
    {
        if (values is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }
}

/// <summary>One proposal as a generator gives it: each part nullable, so a part left out is refused by name.</summary>
public sealed record BrainstormProposalInput(
    [property: Description("The idea, in at most 200 characters.")]
    string? Idea,
    [property: Description("Why it answers the question, in at most 1200 characters.")]
    string? Rationale,
    [property: Description("small (a day), medium (within a week), large (longer, or several repositories), or unknown.")]
    string? Effort,
    [property: Description("low (a failure shows at once, touches only the change), medium (reaches other parts or shows late), high (reaches data, other repositories, people or a release), or unknown.")]
    string? Risk,
    [property: Description("yes (a revert undoes it), partly (something stays: a migration, a message sent), no (data removed, history rewritten, a publication), or unknown.")]
    string? Reversible,
    [property: Description("How it would be undone, or why it cannot be, in at most 400 characters; required unless reversible is yes.")]
    string? Undo,
    [property: Description("How sure you are: low, medium or high.")]
    string? Confidence,
    [property: Description("What it assumes, at most five of 200 characters each.")]
    string[]? Assumptions,
    [property: Description("The packet's source ids it rests on (S1 and on), and no other.")]
    string[]? Sources);

/// <summary>One group of a synthesis.</summary>
public sealed record BrainstormGroupInput(
    [property: Description("The group's title, in at most 200 characters.")]
    string? Title,
    [property: Description("The merged idea, in at most 400 characters.")]
    string? Idea,
    [property: Description("Its members, by their anonymous ids (P1 and on).")]
    string[]? Members,
    [property: Description("Its rank: 1 and on, with no gap and no tie.")]
    int? Rank,
    [property: Description("Why this rank, in at most 600 characters.")]
    string? Why,
    [property: Description("Any flag you add, each with why; none where you add none.")]
    BrainstormFlagInput[]? Flags = null);

/// <summary>A flag a synthesis adds to a group, with why. None it adds lifts one Daoris set.</summary>
public sealed record BrainstormFlagInput(
    [property: Description("hard-to-reverse or risky.")]
    string? Flag,
    [property: Description("Why, in at most 600 characters.")]
    string? Why);

/// <summary>A proposal the synthesis left out of every group, with why.</summary>
public sealed record BrainstormLeftOutInput(
    [property: Description("The proposal's anonymous id.")]
    string? Id,
    [property: Description("Why it is left out, in at most 600 characters.")]
    string? Why);
