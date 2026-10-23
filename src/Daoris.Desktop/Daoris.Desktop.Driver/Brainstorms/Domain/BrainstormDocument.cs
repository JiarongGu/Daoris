using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One pass of a brainstorm as its record keeps it (D176 point 8): its role, who ran it, how it ended and its codes, and how many
/// turns it took (one, or two with its repair).
/// </summary>
/// <param name="Role"><c>generate</c> or <c>synthesize</c>.</param>
/// <param name="State"><c>answered</c>, <c>failed</c> or <c>unavailable</c>; <c>running</c> while a turn runs, and since BRAINSTORM1b
/// <c>stopped</c> by the person or <c>interrupted</c> by a driver that stopped.</param>
public sealed record BrainstormPassRecord(BrainstormBy By, string Role, string State, IReadOnlyList<string> Codes, int Turns)
{
    public string? Actor { get; init; }
    public string? Choice { get; init; }
    public ActorAsked? Asked { get; init; }
    public ActorRan? Ran { get; init; }
    public const string Generate = "generate";
    public const string Synthesize = "synthesize";

    public const string Answered = "answered";
    public const string Failed = "failed";
    public const string Unavailable = "unavailable";

    /// <summary>A turn of it runs: what a restart finds of a pass its driver was running when it stopped.</summary>
    public const string Running = "running";

    /// <summary>Ended by the person's stop (D176 point 6).</summary>
    public const string Stopped = "stopped";

    /// <summary>Ended by a restart: its driver stopped while it ran, and it never runs again by itself (D176 point 6).</summary>
    public const string Interrupted = "interrupted";
}

/// <summary>A member of a group as the document shows it: the proposal under its anonymous id, with its provenance re-attached.</summary>
public sealed record BrainstormMember(string Id, BrainstormBy By, BrainstormProposal Proposal);

/// <summary>A row of the document: a group, ranked or not, with its members, its flags, and how many makers named it.</summary>
/// <param name="Rank">The synthesis's rank, its claim; null in an unranked document.</param>
public sealed record BrainstormRow(int? Rank, string Title, string Idea, string? Why, IReadOnlyList<BrainstormMember> Members, IReadOnlyList<BrainstormFlag> Flags)
{
    /// <summary>How many makers named it: agreement shown as a count, and never a key of the rank (D176 point 4).</summary>
    public int Makers => Members.Select(member => member.By.Maker).Distinct(StringComparer.OrdinalIgnoreCase).Count();
}

/// <summary>
/// The brainstorm's document (D176 points 4, 6 and 8): the ranked groups with every member's provenance, or the validated proposals
/// unranked where the synthesis did not finish; what was left out; what the passes needed; and which passes ran, how.
/// </summary>
/// <param name="Unranked">Why the document is unranked; null where the synthesis ranked it.</param>
public sealed record BrainstormDocument(
    string Id, string Question, string Packet, IReadOnlyList<BrainstormPassRecord> Passes, IReadOnlyList<BrainstormRow> Rows,
    IReadOnlyList<(BrainstormMember Member, string Why)> LeftOut, IReadOnlyList<string> Needs, string? Unranked)
{
    /// <summary>The makers whose generators answered, each once.</summary>
    public IReadOnlyList<string> Makers =>
    [
        .. Passes.Where(pass => pass is { Role: BrainstormPassRecord.Generate, State: BrainstormPassRecord.Answered })
            .Select(pass => pass.By.Maker).Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>Whether every pass ran on a stub door: no model ran, and the document is no maker's diversity.</summary>
    public bool Stubs => Passes.Count > 0 && Passes.All(pass => pass.By.Stub);

    /// <summary>The generators that did not answer, each with why by code (D176 point 6's partial coverage).</summary>
    public IReadOnlyList<BrainstormPassRecord> Unanswered =>
        [.. Passes.Where(pass => pass.Role == BrainstormPassRecord.Generate && pass.State != BrainstormPassRecord.Answered)];
}

/// <summary>The document's assembly with provenance, its JSON and its Markdown (D176 points 4 and 8), with no model and no I/O.</summary>
public static class BrainstormDocuments
{
    public static void Provenance(Utf8JsonWriter writer, BrainstormPassRecord pass)
    {
        if (pass.Actor is not null) writer.WriteString("actor", pass.Actor);
        if (pass.Choice is not null) writer.WriteString("choice", pass.Choice);
        if (pass.Asked is not null) { writer.WritePropertyName("asked"); JsonSerializer.Serialize(writer, pass.Asked, ActorRun.Json); }
        if (pass.Ran is not null) { writer.WritePropertyName("ran"); JsonSerializer.Serialize(writer, pass.Ran, ActorRun.Json); }
    }
    public const string NoSynthesis = "the synthesis did not finish";

    /// <summary>Why a document the person stopped is unranked (D176 point 6).</summary>
    public const string StoppedByPerson = "stopped by the person: what answered is kept, and nothing ran after the stop";

    /// <summary>Why a document a restart found running is unranked (D176 point 6).</summary>
    public const string Interrupted = "interrupted: Daoris stopped while it ran, and nothing runs again by itself";

    /// <summary>
    /// The ranked document: each group of the validated synthesis with its members' provenance re-attached by Daoris, and every
    /// flag Daoris set on a member put on its group beside the synthesis's own (none lifts one).
    /// </summary>
    public static BrainstormDocument Ranked(
        string id, BrainstormPacket packet, IReadOnlyList<BrainstormPassRecord> passes, IReadOnlyList<BrainstormAnonymous> handed,
        BrainstormSynthesized synthesis, IReadOnlyList<string> needs)
    {
        var byId = handed.ToDictionary(each => each.Id, StringComparer.Ordinal);
        var rows = synthesis.Groups.Select(group =>
        {
            var members = group.Members.Select(member => byId[member]).Select(each => new BrainstormMember(each.Id, each.By, each.Proposal)).ToList();
            return new BrainstormRow(group.Rank, group.Title, group.Idea, group.Why, members, [.. Daoris(members), .. group.Flags]);
        }).ToList();
        var left = synthesis.LeftOut.Select(drop => (new BrainstormMember(drop.Id, byId[drop.Id].By, byId[drop.Id].Proposal), drop.Why)).ToList();
        return new BrainstormDocument(id, packet.Question, packet.Digest, passes, rows, left, needs, null);
    }

    /// <summary>
    /// The unranked document (D176 point 6): the validated proposals by maker, with Daoris's flags, and the exact duplicates folded
    /// (the same idea, normalized, from two makers is one row naming both).
    /// </summary>
    public static BrainstormDocument Unranked(
        string id, BrainstormPacket packet, IReadOnlyList<BrainstormPassRecord> passes, IReadOnlyList<BrainstormAnonymous> handed,
        IReadOnlyList<string> needs, string why)
    {
        var rows = handed
            .OrderBy(each => each.By.Maker, StringComparer.OrdinalIgnoreCase)
            .GroupBy(each => BrainstormAnswers.Normal(each.Proposal.Idea), StringComparer.Ordinal)
            .Select(fold =>
            {
                var members = fold.Select(each => new BrainstormMember(each.Id, each.By, each.Proposal)).ToList();
                return new BrainstormRow(null, members[0].Proposal.Idea, members[0].Proposal.Idea, null, members, Daoris(members));
            })
            .ToList();
        return new BrainstormDocument(id, packet.Question, packet.Digest, passes, rows, [], needs, why);
    }

    private static IReadOnlyList<BrainstormFlag> Daoris(IEnumerable<BrainstormMember> members) =>
    [
        .. members.SelectMany(member => member.Proposal.Flags).Distinct(StringComparer.Ordinal)
            .Select(flag => new BrainstormFlag(flag, "daoris", null)),
    ];

    /// <summary>The document as JSON, written once as <c>document.json</c>.</summary>
    public static byte[] Json(BrainstormDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("brainstorm", BrainstormAnswers.Schema);
            writer.WriteString("id", document.Id);
            writer.WriteString("question", document.Question);
            writer.WriteString("packet", document.Packet);
            writer.WriteBoolean("ranked", document.Unranked is null);
            if (document.Unranked is null) writer.WriteNull("unranked");
            else writer.WriteString("unranked", document.Unranked);
            writer.WriteBoolean("stubs", document.Stubs);
            writer.WriteStartArray("makers");
            foreach (var maker in document.Makers) writer.WriteStringValue(maker);
            writer.WriteEndArray();
            // Added by BRAINSTORM1b: the generators that did not answer, and why by code (D176 point 6).
            writer.WriteStartArray("unanswered");
            foreach (var pass in document.Unanswered)
            {
                writer.WriteStartObject();
                By(writer, pass.By);
                writer.WriteString("state", pass.State);
                writer.WriteStartArray("codes");
                foreach (var code in pass.Codes) writer.WriteStringValue(code);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("rows");
            foreach (var row in document.Rows)
            {
                writer.WriteStartObject();
                if (row.Rank is { } rank) writer.WriteNumber("rank", rank);
                else writer.WriteNull("rank");
                writer.WriteString("title", row.Title);
                writer.WriteString("idea", row.Idea);
                if (row.Why is null) writer.WriteNull("why");
                else writer.WriteString("why", row.Why);
                writer.WriteNumber("makers", row.Makers);
                writer.WriteStartArray("flags");
                foreach (var flag in row.Flags)
                {
                    writer.WriteStartObject();
                    writer.WriteString("flag", flag.Flag);
                    writer.WriteString("by", flag.By);
                    if (flag.Why is null) writer.WriteNull("why");
                    else writer.WriteString("why", flag.Why);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteStartArray("members");
                foreach (var member in row.Members) Member(writer, member, null);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("leftOut");
            foreach (var (member, why) in document.LeftOut) Member(writer, member, why);
            writer.WriteEndArray();
            writer.WriteStartArray("needs");
            foreach (var need in document.Needs) writer.WriteStringValue(need);
            writer.WriteEndArray();
            writer.WriteStartArray("passes");
            foreach (var pass in document.Passes)
            {
                writer.WriteStartObject();
                By(writer, pass.By);
                writer.WriteString("role", pass.Role);
                Provenance(writer, pass);
                writer.WriteString("state", pass.State);
                writer.WriteNumber("turns", pass.Turns);
                writer.WriteStartArray("codes");
                foreach (var code in pass.Codes) writer.WriteStringValue(code);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>
    /// The document as a person reads it at the terminal, in Daoris's fixed words around the agents' own (D176 point 8): what was
    /// asked, which packet, whether the rank is the synthesis's claim, and whether any maker diversity stands behind it.
    /// </summary>
    public static string Markdown(BrainstormDocument document)
    {
        var text = new StringBuilder();
        text.Append("# Brainstorm `").Append(document.Id).Append("`\n\n");
        foreach (var line in document.Question.Split('\n')) text.Append("> ").Append(line.TrimEnd('\r')).Append('\n');
        text.Append("\nPacket `").Append(document.Packet).Append("`, the same file in every pass's room.\n");
        text.Append(document.Unranked is null
            ? "Ranked by the synthesis: the rank is its claim, and agreement is shown as a count, never as a rank.\n"
            : $"Unranked: {document.Unranked}.\n");
        if (document.Stubs)
        {
            text.Append("Every pass ran on a stub agent: no model answered, and stubs are no maker diversity.\n");
        }
        else if (document.Makers.Count < 2)
        {
            text.Append("One maker answered: this is no cross-maker brainstorm.\n");
        }

        foreach (var pass in document.Passes)
        {
            text.Append("\nPass `").Append(pass.By.Pass).Append("`: actor ").Append(pass.Actor ?? "not reported")
                .Append(", choice ").Append(pass.Choice ?? "not reported").Append(", model ")
                .Append(pass.Ran is { Models.Count: > 0 } ran ? string.Join(", ", ran.Models) : "not reported");
            if (pass.Ran is { More: > 0 } more) text.Append(" and ").Append(more.More).Append(" more");
            text.Append(".\n");
        }
        // Partial coverage (BRAINSTORM1b, D176 point 6): which generators answered, and which did not and why, by code.
        var answered = document.Passes.Where(pass => pass is { Role: BrainstormPassRecord.Generate, State: BrainstormPassRecord.Answered }).ToList();
        if (answered.Count > 0 && document.Unanswered.Count > 0)
        {
            text.Append("Answered: ").Append(string.Join("; ", answered.Select(pass => $"`{pass.By.Adapter}` (maker {pass.By.Maker})"))).Append(".\n");
        }

        if (document.Unanswered.Count > 0)
        {
            text.Append("Did not answer: ")
                .Append(string.Join("; ", document.Unanswered.Select(pass => $"`{pass.By.Adapter}` (maker {pass.By.Maker}): {string.Join(", ", pass.Codes)}")))
                .Append(".\n");
        }

        foreach (var row in document.Rows)
        {
            text.Append('\n').Append(row.Rank is { } rank ? $"## {rank}. " : "## ").Append(row.Title).Append("\n\n");
            if (row.Idea != row.Title) text.Append(row.Idea).Append("\n\n");
            if (row.Why is not null) text.Append("Why here (the synthesis's words): ").Append(row.Why).Append('\n');
            foreach (var flag in row.Flags)
            {
                text.Append("Flagged ").Append(flag.Flag == BrainstormAnswers.HardToReverse ? "hard to reverse" : "risky")
                    .Append(flag.By == "daoris" ? " (Daoris, from the fields)" : $" (the synthesis: {flag.Why})").Append('\n');
            }

            text.Append("Named by ").Append(row.Makers).Append(row.Makers == 1 ? " maker:\n" : " makers:\n");
            foreach (var member in row.Members) Line(text, member, null);
        }

        if (document.LeftOut.Count > 0)
        {
            text.Append("\n## Left out by the synthesis\n\n");
            foreach (var (member, why) in document.LeftOut) Line(text, member, why);
        }

        if (document.Needs.Count > 0)
        {
            text.Append("\n## What the passes would need that the packet lacks\n\n");
            foreach (var need in document.Needs) text.Append("- ").Append(need).Append('\n');
        }

        text.Append("\n## Passes\n\n");
        foreach (var pass in document.Passes)
        {
            text.Append("- `").Append(pass.By.Pass).Append("` ").Append(pass.Role).Append(" on `").Append(pass.By.Adapter).Append("` (maker ")
                .Append(pass.By.Maker).Append(pass.By.Account is { } account ? $", account {account}" : "").Append("): ").Append(pass.State)
                .Append(pass.Turns > 1 ? $" after its repair" : "")
                .Append(pass.Codes.Count > 0 ? $" ({string.Join(", ", pass.Codes)})" : "").Append('\n');
        }

        return text.ToString();
    }

    private static void Line(StringBuilder text, BrainstormMember member, string? why)
    {
        var proposal = member.Proposal;
        text.Append("- ").Append(member.Id).Append(", by `").Append(member.By.Adapter).Append("` (maker ").Append(member.By.Maker)
            .Append(", pass `").Append(member.By.Pass).Append("`): ").Append(proposal.Idea)
            .Append(" — effort ").Append(proposal.Effort).Append(", risk ").Append(proposal.Risk).Append(", reversible ").Append(proposal.Reversible)
            .Append(", confidence ").Append(proposal.Confidence)
            .Append(why is null ? "" : $". Left out: {why}").Append('\n');
    }

    private static void Member(Utf8JsonWriter writer, BrainstormMember member, string? why)
    {
        writer.WriteStartObject();
        writer.WriteString("id", member.Id);
        By(writer, member.By);
        Fields(writer, member.Proposal);
        if (why is not null) writer.WriteString("why", why);
        writer.WriteEndObject();
    }

    private static void By(Utf8JsonWriter writer, BrainstormBy by)
    {
        writer.WriteString("pass", by.Pass);
        writer.WriteString("maker", by.Maker);
        writer.WriteString("adapter", by.Adapter);
        if (by.Account is null) writer.WriteNull("account");
        else writer.WriteString("account", by.Account);
    }

    /// <summary>A proposal's fields as <c>brainstorm_give</c> named them, and Daoris's flags read from them.</summary>
    internal static void Fields(Utf8JsonWriter writer, BrainstormProposal proposal)
    {
        writer.WriteString("idea", proposal.Idea);
        writer.WriteString("rationale", proposal.Rationale);
        writer.WriteString("effort", proposal.Effort);
        writer.WriteString("risk", proposal.Risk);
        writer.WriteString("reversible", proposal.Reversible);
        if (proposal.Undo is null) writer.WriteNull("undo");
        else writer.WriteString("undo", proposal.Undo);
        writer.WriteString("confidence", proposal.Confidence);
        writer.WriteStartArray("assumptions");
        foreach (var assumption in proposal.Assumptions) writer.WriteStringValue(assumption);
        writer.WriteEndArray();
        writer.WriteStartArray("sources");
        foreach (var source in proposal.Sources) writer.WriteStringValue(source);
        writer.WriteEndArray();
        writer.WriteStartArray("flags");
        foreach (var flag in proposal.Flags) writer.WriteStringValue(flag);
        writer.WriteEndArray();
    }
}
