using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Who gave an answer (D176 point 4): its pass, its maker, its adapter and its account, which the synthesis never sees.</summary>
/// <param name="Maker">The maker's family as the adapter set declares it: two doors of one maker are one maker (D176 point 7).</param>
/// <param name="Account">The account's profile, shown on this machine only; null where it ran as none.</param>
public sealed record BrainstormBy(string Pass, string Maker, string Adapter, string? Account)
{
    /// <summary>A stub door, which runs no model: its proposals are no maker's diversity.</summary>
    public bool Stub => Adapter is "stub" or "acp-stub";
}

/// <summary>A validated proposal under its anonymous id, <c>P1</c>…, with who gave it kept beside it by Daoris.</summary>
public sealed record BrainstormAnonymous(string Id, BrainstormBy By, BrainstormProposal Proposal);

/// <summary>A flag on a group: Daoris's, read from a member's fields, or one the synthesis added with its reason. None is lifted.</summary>
/// <param name="By"><c>daoris</c> or <c>synthesis</c>.</param>
public sealed record BrainstormFlag(string Flag, string By, string? Why);

/// <summary>One ranked group of the synthesis (D176 point 4): its title, merged idea, members by anonymous id, rank and why.</summary>
public sealed record BrainstormGroup(int Rank, string Title, string Idea, string Why, IReadOnlyList<string> Members, IReadOnlyList<BrainstormFlag> Flags);

/// <summary>A proposal the synthesis left out, with why.</summary>
public sealed record BrainstormDropped(string Id, string Why);

/// <summary>A synthesis validated: its groups by rank, and what it left out.</summary>
public sealed record BrainstormSynthesized(IReadOnlyList<BrainstormGroup> Groups, IReadOnlyList<BrainstormDropped> LeftOut);

/// <summary>What the validator made of a synthesis: the synthesis, or the codes it failed on.</summary>
public sealed record BrainstormSynthesisChecked(BrainstormSynthesized? Synthesis, IReadOnlyList<string> Codes)
{
    public bool Valid => Synthesis is not null;
}

/// <summary>
/// The synthesis's side of Daoris (D176 point 4): the anonymous numbering, with the makers interleaved; the validation of
/// <c>brainstorm_synthesize</c>'s answer; and nothing of who gave which handed to it.
/// </summary>
public static class BrainstormSyntheses
{
    public const int TitleChars = 200;
    public const int IdeaChars = 400;
    public const int WhyChars = 600;

    public const string MemberTwice = "member-twice";
    public const string MemberMissing = "member-missing";
    public const string MemberUnknown = "member-unknown";
    public const string Ranks = "ranks";
    public const string FlagUnknown = "flag-unknown";

    /// <summary>
    /// The validated proposals numbered <c>P1</c>…<c>Pn</c>, the makers interleaved: each answer's first, in pass order, then each
    /// one's second, so no maker's proposals stand together and the order says nothing of who gave them.
    /// </summary>
    public static IReadOnlyList<BrainstormAnonymous> Number(IReadOnlyList<(BrainstormBy By, BrainstormAnswer Answer)> answered)
    {
        var numbered = new List<BrainstormAnonymous>();
        var most = answered.Count == 0 ? 0 : answered.Max(each => each.Answer.Proposals.Count);
        for (var at = 0; at < most; at++)
        {
            foreach (var (by, answer) in answered)
            {
                if (at < answer.Proposals.Count) numbered.Add(new($"P{numbered.Count + 1}", by, answer.Proposals[at]));
            }
        }

        return numbered;
    }

    /// <summary>
    /// What the synthesizer reads beside the packet: each proposal by its anonymous id with its fields and Daoris's flags, and
    /// nothing of its maker, its adapter, its pass or its account.
    /// </summary>
    public static byte[] Anonymous(IReadOnlyList<BrainstormAnonymous> proposals)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("brainstorm", BrainstormAnswers.Schema);
            writer.WriteStartArray("proposals");
            foreach (var each in proposals)
            {
                writer.WriteStartObject();
                writer.WriteString("id", each.Id);
                BrainstormDocuments.Fields(writer, each.Proposal);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>
    /// Validate a synthesis against the ids it was handed (D176 point 4): every id once, in one group or left out with why; ranks
    /// 1 to n with no gap or tie; no id invented; each group's words within their bounds; a flag it adds named and given its why.
    /// Daoris's flags are not the synthesis's to keep: Daoris puts each member's on its group (<see cref="BrainstormDocuments"/>).
    /// </summary>
    public static BrainstormSynthesisChecked Check(string? text, IReadOnlyList<BrainstormAnonymous> handed) =>
        Check(text, handed.Select(each => each.Id).ToList());

    /// <summary>
    /// The same against the anonymous ids alone: what the turn was handed. The connector's <c>brainstorm_synthesize</c> judges by its
    /// twin, the service's <c>BrainstormChecks</c>, and both are held to one table (BRAINSTORM1a2, <c>fixtures/brainstorm-answers.json</c>).
    /// </summary>
    public static BrainstormSynthesisChecked Check(string? text, IReadOnlyCollection<string> ids)
    {
        if (text is null) return new(null, [BrainstormAnswers.Missing]);
        if (text.Length > BrainstormAnswers.AnswerChars) return new(null, [BrainstormAnswers.TooLong]);
        try
        {
            using var document = JsonDocument.Parse(text);
            return Check(document.RootElement, ids);
        }
        catch (JsonException)
        {
            return new(null, [BrainstormAnswers.Shape]);
        }
    }

    private static BrainstormSynthesisChecked Check(JsonElement root, IReadOnlyCollection<string> handed)
    {
        var codes = new List<string>();
        void Fail(string code)
        {
            if (!codes.Contains(code)) codes.Add(code);
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("brainstorm", out var schema)
            || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != BrainstormAnswers.Schema
            || !root.TryGetProperty("groups", out var given) || given.ValueKind != JsonValueKind.Array)
        {
            return new(null, [BrainstormAnswers.Shape]);
        }

        var known = handed.ToHashSet(StringComparer.Ordinal);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        void Place(string id)
        {
            if (!known.Contains(id)) Fail(MemberUnknown);
            else if (!placed.Add(id)) Fail(MemberTwice);
        }

        var groups = new List<BrainstormGroup>();
        foreach (var item in given.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                Fail(BrainstormAnswers.Shape);
                continue;
            }

            var title = Words(item, "title", TitleChars, Fail);
            var idea = Words(item, "idea", IdeaChars, Fail);
            var why = Words(item, "why", WhyChars, Fail);
            var rank = item.TryGetProperty("rank", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var r) ? r : 0;
            var members = item.TryGetProperty("members", out var ids) && ids.ValueKind == JsonValueKind.Array
                ? ids.EnumerateArray().Select(id => id.ValueKind == JsonValueKind.String ? id.GetString()! : "").ToList()
                : null;
            if (members is null or { Count: 0 }) Fail(BrainstormAnswers.Field("members"));
            foreach (var id in members ?? []) Place(id);

            var flags = new List<BrainstormFlag>();
            if (item.TryGetProperty("flags", out var added) && added.ValueKind == JsonValueKind.Array)
            {
                foreach (var flag in added.EnumerateArray())
                {
                    var name = Json.Text(flag, "flag");
                    var reason = Json.Text(flag, "why")?.Trim();
                    if (name is not (BrainstormAnswers.HardToReverse or BrainstormAnswers.Risky) || reason is not { Length: > 0 and <= WhyChars }) Fail(FlagUnknown);
                    else flags.Add(new BrainstormFlag(name, "synthesis", reason));
                }
            }

            if (title is null || idea is null || why is null || members is null) continue;
            groups.Add(new BrainstormGroup(rank, title, idea, why, members, flags));
        }

        var dropped = new List<BrainstormDropped>();
        if (root.TryGetProperty("leftOut", out var left) && left.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in left.EnumerateArray())
            {
                var id = Json.Text(item, "id") ?? "";
                var why = Words(item, "why", WhyChars, Fail);
                Place(id);
                if (why is not null) dropped.Add(new BrainstormDropped(id, why));
            }
        }

        if (known.Any(id => !placed.Contains(id))) Fail(MemberMissing);
        var ranks = groups.Select(group => group.Rank).OrderBy(rank => rank).ToList();
        if (!ranks.SequenceEqual(Enumerable.Range(1, ranks.Count))) Fail(Ranks);

        return codes.Count > 0
            ? new(null, codes)
            : new(new BrainstormSynthesized([.. groups.OrderBy(group => group.Rank)], dropped), []);
    }

    private static string? Words(JsonElement item, string name, int bound, Action<string> fail)
    {
        var text = Json.Text(item, name)?.Trim();
        if (text is not { Length: > 0 })
        {
            fail(BrainstormAnswers.Field(name));
            return null;
        }

        if (text.Length > bound)
        {
            fail(BrainstormAnswers.Field($"{name}-length"));
            return null;
        }

        return text;
    }
}
