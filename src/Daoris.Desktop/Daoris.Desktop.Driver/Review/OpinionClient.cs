using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the driver asks the local host when it starts a pass (XAGENT1d; XAGENT1c's <c>POST /api/opinions</c>): the occasion and
/// the pass, the working session, the candidate by full ids, the reviewer as the walk chose it, the families that wrote the
/// work, what held the reviewer read-only, the rule's minutes, and the reviewer's own copy.
/// </summary>
/// <param name="Minutes">The rule's bound of one pass, as the opinion keeps it: the service holds no rule.</param>
/// <param name="Tree">The reviewer's copy, never the working session's tree.</param>
public sealed record OpinionAskBody(
    string Occasion, string Working, OpinionCandidateRead Candidate, string Adapter, string Label, IReadOnlyList<string> Families,
    string Posture, int Minutes, string Tree)
{
    /// <summary><c>first</c>, or a recheck's (XAGENT1e).</summary>
    public string Pass { get; init; } = "first";

    /// <summary>For a recheck (XAGENT1e, design §6.5), the first pass whose findings it reads again; null for a first pass.</summary>
    public string? Rechecks { get; init; }

    /// <summary>The reviewer's declared product and maker, as every opinion carries them (§3.4); null where none is declared.</summary>
    public string? Product { get; init; }

    public string? Maker { get; init; }

    /// <summary>The account it runs as, a profile's name; null for the tool's own home.</summary>
    public string? Account { get; init; }

    /// <summary>The harness version the reviewer's account walk observed (D49 §4).</summary>
    public string? HarnessVersion { get; init; }

    /// <summary>The body as the door reads it.</summary>
    internal string Json()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("occasion", Occasion);
            writer.WriteString("pass", Pass);
            if (Rechecks is not null) writer.WriteString("rechecks", Rechecks);
            writer.WriteString("working", Working);
            writer.WriteStartObject("candidate");
            writer.WriteString("repository", Candidate.Repository);
            writer.WriteString("base", Candidate.Base);
            writer.WriteString("tip", Candidate.Tip);
            writer.WriteStartArray("commits");
            foreach (var commit in Candidate.Commits) writer.WriteStringValue(commit);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteStartObject("reviewer");
            writer.WriteString("adapter", Adapter);
            writer.WriteString("label", Label);
            if (Product is not null) writer.WriteString("product", Product);
            if (Maker is not null) writer.WriteString("maker", Maker);
            if (Account is not null) writer.WriteString("account", Account);
            writer.WriteEndObject();
            writer.WriteStartArray("families");
            foreach (var family in Families) writer.WriteStringValue(family);
            writer.WriteEndArray();
            writer.WriteString("posture", Posture);
            writer.WriteNumber("minutes", Minutes);
            writer.WriteString("tree", Tree);
            if (HarnessVersion is not null) writer.WriteString("harnessVersion", HarnessVersion);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

/// <summary>What the host answered a pass's ask: the opinion and its reviewer's record, or why not, in its sentence.</summary>
/// <param name="Opinion">The second opinion's id; null where it was refused.</param>
/// <param name="Session">The reviewer's session record; null where it was refused.</param>
/// <param name="Message">The host's sentence: its yes, or its refusal verbatim.</param>
public sealed record OpinionAsked(string? Opinion, string? Session, string Message)
{
    public bool Opened => Opinion is not null && Session is not null;
}
