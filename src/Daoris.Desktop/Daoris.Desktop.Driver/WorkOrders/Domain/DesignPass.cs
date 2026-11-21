using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One of the owner's closed questions a design pass's comparison asks: what it is, what yes and no change, where to look.</summary>
/// <param name="Key">The comparison's own key for it (<c>Q1</c> and on), which the decision that answers it names.</param>
public sealed record DesignQuestion(string Key, string Question, string Yes, string No, string Where);

/// <summary>The trial the comparison proposes: the one area it is tried on, the baseline recorded first, and the keep and retire criteria written before it starts.</summary>
public sealed record DesignTrial(string Area, string Baseline, IReadOnlyList<string> Keep, IReadOnlyList<string> Retire);

/// <summary>
/// A design pass's comparison, as its reader gives it beside the ranked groups: the route, why, the evidence that would flip it,
/// the trial, and the owner's closed questions. A claim a person reads, never a decision by itself.
/// </summary>
public sealed record DesignComparison(string Route, string Why, IReadOnlyList<string> Flips, DesignTrial Trial, IReadOnlyList<DesignQuestion> Questions);

/// <summary>
/// The design pass (the fresh-design pass of the canon, run by the brainstorm): a packet of the purpose brief alone, designers
/// who write from it with no repository in hand, and one comparison that names the route, what would flip it and a trial judged
/// by criteria written first. A brainstorm's request names it by its <c>pass</c>; with none, every choice here is the brainstorm's
/// own, unchanged.
/// </summary>
public static class DesignPasses
{
    /// <summary>The request's <c>pass</c> that asks for a design pass.</summary>
    public const string Design = "design";

    public const int RouteChars = 400;
    public const int WhyChars = 1_200;
    public const int LineChars = 400;
    public const int MostFlips = 6;
    public const int MostCriteria = 6;
    public const int MostQuestions = 8;

    /// <summary>The comparison is absent or not an object.</summary>
    public const string NoComparison = "comparison-missing";

    /// <summary>A comparison field is missing, blank or too long; the code names it (<c>comparison:route</c>).</summary>
    public const string Field = "comparison";

    public static bool Is(BrainstormRequest request) => string.Equals(request.Pass, Design, StringComparison.Ordinal);

    /// <summary>
    /// A design pass's packet: the purpose brief alone, no search and no source, and no repository named, so nothing of the
    /// repository's history reaches a designer. That is the method's point.
    /// </summary>
    public static BrainstormPacket Packet(BrainstormRequest request) =>
        BrainstormPackets.Freeze(request.Question, null, null, null, [], BrainstormBounds.First);

    public static string Generator(BrainstormRequest request, string digest, int minutes) =>
        Is(request) ? BrainstormInstruction.Designer(digest, minutes) : BrainstormInstruction.Generator(digest, minutes);

    public static string Synthesizer(BrainstormRequest request, string digest) =>
        Is(request) ? BrainstormInstruction.Comparer(digest) : BrainstormInstruction.Synthesizer(digest);

    /// <summary>
    /// The synthesis checked as the brainstorm checks it, and for a design pass its comparison too: one missing or malformed
    /// refuses the answer with its codes, so the one repair turn names them.
    /// </summary>
    public static BrainstormSynthesisChecked Check(BrainstormRequest request, string? text, IReadOnlyList<BrainstormAnonymous> handed)
    {
        var check = BrainstormSyntheses.Check(text, handed);
        if (!Is(request) || check.Synthesis is null) return check;
        var (comparison, codes) = Compare(text!);
        return comparison is null ? new BrainstormSynthesisChecked(null, codes) : check with { Synthesis = check.Synthesis with { Comparison = comparison } };
    }

    /// <summary>The comparison read from a synthesis's answer, or the codes that refuse it.</summary>
    public static (DesignComparison? Comparison, IReadOnlyList<string> Codes) Compare(string text)
    {
        var codes = new List<string>();
        void Fail(string code)
        {
            if (!codes.Contains(code)) codes.Add(code);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("comparison", out var root) || root.ValueKind != JsonValueKind.Object) return (null, [NoComparison]);
            var route = Said(root, "route", RouteChars, Fail);
            var why = Said(root, "why", WhyChars, Fail);
            var flips = Lines(root, "flips", MostFlips, Fail, required: true);
            DesignTrial? trial = null;
            if (!root.TryGetProperty("trial", out var tried) || tried.ValueKind != JsonValueKind.Object) Fail($"{Field}:trial");
            else
            {
                var area = Said(tried, "area", LineChars, Fail, "trial.");
                var baseline = Said(tried, "baseline", LineChars, Fail, "trial.");
                var keep = Lines(tried, "keep", MostCriteria, Fail, required: true, "trial.");
                var retire = Lines(tried, "retire", MostCriteria, Fail, required: true, "trial.");
                if (area is not null && baseline is not null) trial = new DesignTrial(area, baseline, keep, retire);
            }

            var questions = new List<DesignQuestion>();
            if (root.TryGetProperty("questions", out var asked) && asked.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in asked.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) { Fail($"{Field}:questions"); continue; }
                    var parts = new[] { "question", "yes", "no", "where" }.Select(name => Said(item, name, LineChars, Fail, "questions.")).ToArray();
                    if (parts.All(part => part is not null)) questions.Add(new DesignQuestion($"Q{questions.Count + 1}", parts[0]!, parts[1]!, parts[2]!, parts[3]!));
                }

                if (asked.GetArrayLength() > MostQuestions) Fail($"{Field}:questions");
            }
            else if (root.TryGetProperty("questions", out var other) && other.ValueKind != JsonValueKind.Null)
            {
                Fail($"{Field}:questions");
            }

            return codes.Count > 0 || route is null || why is null || trial is null
                ? (null, codes.Count > 0 ? codes : [NoComparison])
                : (new DesignComparison(route, why, flips, trial, questions), []);
        }
        catch (JsonException)
        {
            return (null, [BrainstormAnswers.Shape]);
        }
    }

    private static string? Said(JsonElement item, string name, int bound, Action<string> fail, string under = "")
    {
        var value = item.TryGetProperty(name, out var said) && said.ValueKind == JsonValueKind.String ? said.GetString()!.Trim() : null;
        if (value is { Length: > 0 } && value.Length <= bound) return value;
        fail($"{Field}:{under}{name}");
        return null;
    }

    private static IReadOnlyList<string> Lines(JsonElement item, string name, int most, Action<string> fail, bool required, string under = "")
    {
        if (!item.TryGetProperty(name, out var listed) || listed.ValueKind != JsonValueKind.Array)
        {
            if (required) fail($"{Field}:{under}{name}");
            return [];
        }

        var lines = listed.EnumerateArray().Select(each => each.ValueKind == JsonValueKind.String ? each.GetString()!.Trim() : "").ToList();
        if (lines.Count > most || lines.Any(line => line.Length == 0 || line.Length > LineChars) || (required && lines.Count == 0))
        {
            fail($"{Field}:{under}{name}");
        }

        return lines;
    }
}
