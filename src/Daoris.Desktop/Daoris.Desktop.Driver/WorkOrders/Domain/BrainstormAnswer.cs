using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One proposal as a generator gave it through <c>brainstorm_give</c> (BRAINSTORM1a, D176 point 3), validated.</summary>
public sealed record BrainstormProposal(
    string Idea, string Rationale, string Effort, string Risk, string Reversible, string? Undo, string Confidence,
    IReadOnlyList<string> Assumptions, IReadOnlyList<string> Sources)
{
    /// <summary>
    /// Daoris's flags, read from the fields (D176 point 3): <c>hard-to-reverse</c> where reversible is not <c>yes</c>, <c>risky</c>
    /// where the risk is <c>high</c> or <c>unknown</c>. Unknown is never read as its best value.
    /// </summary>
    public IReadOnlyList<string> Flags =>
    [
        .. Reversible != "yes" ? [BrainstormAnswers.HardToReverse] : Array.Empty<string>(),
        .. Risk is "high" or "unknown" ? [BrainstormAnswers.Risky] : Array.Empty<string>(),
    ];
}

/// <summary>A generator's whole answer, validated: its proposals, what it would need that the packet lacks, or why it has none.</summary>
public sealed record BrainstormAnswer(IReadOnlyList<BrainstormProposal> Proposals, IReadOnlyList<string> Needs, string? None);

/// <summary>What the validator made of an answer: the answer, or the codes it failed on. Never both, and never part of one.</summary>
public sealed record BrainstormChecked(BrainstormAnswer? Answer, IReadOnlyList<string> Codes)
{
    public bool Valid => Answer is not null;
}

/// <summary>
/// The validator of a generator's answer (D176 point 3): Daoris's, by the schema, with no model. A missing answer, a field outside
/// its scale or bound, an unknown source id, an idea given twice, or proposals beside <c>none</c> fail the pass; Daoris never
/// repairs, trims or keeps part of an invalid answer.
/// </summary>
public static class BrainstormAnswers
{
    public const int Schema = 1;

    public const string HardToReverse = "hard-to-reverse";
    public const string Risky = "risky";

    public static readonly IReadOnlyList<string> Efforts = ["small", "medium", "large", "unknown"];
    public static readonly IReadOnlyList<string> Risks = ["low", "medium", "high", "unknown"];
    public static readonly IReadOnlyList<string> Reversibles = ["yes", "partly", "no", "unknown"];
    public static readonly IReadOnlyList<string> Confidences = ["low", "medium", "high"];

    public const int IdeaChars = 200;
    public const int RationaleChars = 1_200;
    public const int UndoChars = 400;
    public const int ListItems = 5;
    public const int ItemChars = 200;
    public const int MostProposals = 8;
    public const int AnswerChars = 20_000;

    // The codes, in fixed words a repair turn is handed (D176 point 3).
    public const string Missing = "missing";
    public const string Shape = "shape";
    public const string TooLong = "too-long";
    public const string TooMany = "too-many";
    public const string Empty = "empty";
    public const string BesideNone = "proposals-beside-none";
    public const string IdeaTwice = "idea-twice";
    public const string UnknownSource = "unknown-source";

    /// <summary>The code for a field outside its scale, or past its bound: <c>&lt;field&gt;</c> or <c>&lt;field&gt;-length</c>.</summary>
    public static string Field(string field) => $"field:{field}";

    /// <summary>
    /// Validate an answer's text against the packet's source ids. The codes are ordered and each said once; an answer with any
    /// code is no answer.
    /// </summary>
    public static BrainstormChecked Check(string? text, IReadOnlySet<string> sources)
    {
        if (text is null) return new(null, [Missing]);
        if (text.Length > AnswerChars) return new(null, [TooLong]);
        try
        {
            using var document = JsonDocument.Parse(text);
            return Check(document.RootElement, sources);
        }
        catch (JsonException)
        {
            return new(null, [Shape]);
        }
    }

    private static BrainstormChecked Check(JsonElement root, IReadOnlySet<string> sources)
    {
        var codes = new List<string>();
        void Fail(string code)
        {
            if (!codes.Contains(code)) codes.Add(code);
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("brainstorm", out var schema)
            // A schema that is no whole number is no schema: read as a shape, never thrown (BRAINSTORM1a2's table).
            || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != Schema)
        {
            return new(null, [Shape]);
        }

        var none = Words(root, "none", ItemChars, "none", Fail);
        var needs = List(root, "needs", "needs", Fail);
        var proposals = new List<BrainstormProposal>();
        var items = root.TryGetProperty("proposals", out var given) && given.ValueKind == JsonValueKind.Array ? given.EnumerateArray().ToList() : null;
        if (root.TryGetProperty("proposals", out var shape) && shape.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null)) Fail(Shape);
        items ??= [];
        if (items.Count > MostProposals) Fail(TooMany);
        if (items.Count > 0 && none is not null) Fail(BesideNone);
        if (items.Count == 0 && none is null) Fail(Empty);

        var ideas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                Fail(Shape);
                continue;
            }

            var idea = Words(item, "idea", IdeaChars, "idea", Fail, required: true);
            var rationale = Words(item, "rationale", RationaleChars, "rationale", Fail, required: true);
            var effort = Scale(item, "effort", Efforts, Fail);
            var risk = Scale(item, "risk", Risks, Fail);
            var reversible = Scale(item, "reversible", Reversibles, Fail);
            var undo = Words(item, "undo", UndoChars, "undo", Fail, required: reversible is not null and not "yes");
            var confidence = Scale(item, "confidence", Confidences, Fail);
            var assumptions = List(item, "assumptions", "assumptions", Fail);
            var named = Ids(item, Fail);
            foreach (var id in named.Where(id => !sources.Contains(id))) Fail(UnknownSource);
            if (idea is not null && !ideas.Add(Normal(idea))) Fail(IdeaTwice);
            if (idea is null || rationale is null || effort is null || risk is null || reversible is null || confidence is null) continue;
            proposals.Add(new BrainstormProposal(idea, rationale, effort, risk, reversible, undo, confidence, assumptions, named));
        }

        return codes.Count > 0 ? new(null, codes) : new(new BrainstormAnswer(proposals, needs, none), []);
    }

    /// <summary>An idea's text as two answers' are compared: trimmed, spaces folded, case ignored.</summary>
    public static string Normal(string idea) =>
        string.Join(' ', idea.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static string? Words(JsonElement item, string name, int bound, string field, Action<string> fail, bool required = false)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required) fail(Field(field));
            return null;
        }

        if (value.ValueKind != JsonValueKind.String || value.GetString()!.Trim().Length == 0)
        {
            fail(Field(field));
            return null;
        }

        var text = value.GetString()!.Trim();
        if (text.Length > bound)
        {
            fail(Field($"{field}-length"));
            return null;
        }

        return text;
    }

    private static string? Scale(JsonElement item, string name, IReadOnlyList<string> scale, Action<string> fail)
    {
        var given = item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (given is not null && scale.Contains(given)) return given;
        fail(Field(name));
        return null;
    }

    private static IReadOnlyList<string> List(JsonElement item, string name, string field, Action<string> fail)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            fail(Field(field));
            return [];
        }

        var items = value.EnumerateArray().ToList();
        if (items.Count > ListItems) fail(Field($"{field}-count"));
        var kept = new List<string>();
        foreach (var each in items)
        {
            if (each.ValueKind != JsonValueKind.String || each.GetString()!.Trim().Length == 0) fail(Field(field));
            else if (each.GetString()!.Trim().Length > ItemChars) fail(Field($"{field}-length"));
            else kept.Add(each.GetString()!.Trim());
        }

        return kept;
    }

    private static IReadOnlyList<string> Ids(JsonElement item, Action<string> fail)
    {
        if (!item.TryGetProperty("sources", out var value) || value.ValueKind == JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            fail(Field("sources"));
            return [];
        }

        var ids = new List<string>();
        foreach (var each in value.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.String) fail(Field("sources"));
            else if (!ids.Contains(each.GetString()!)) ids.Add(each.GetString()!);
        }

        return ids;
    }
}
