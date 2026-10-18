using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The brainstorm turn a connector serves (BRAINSTORM1a2, D176 points 1-4): the brainstorm, the turn, the answer file its result tool
/// writes and the room the turn was handed, each as the driver names them on the connector it offers a pass. Blank is nothing said.
/// </summary>
/// <remarks>
/// A twin of the driver's <c>BrainstormVariables</c>: the names are duplicated deliberately, never imported across. Every other
/// session's connector names none of them, and its brainstorm tools are refused.
/// </remarks>
public sealed record BrainstormScope(string? Id, string? Turn, string? Answer, string? Room)
{
    public const string IdVariable = "DAORIS_BRAINSTORM_ID";
    public const string TurnVariable = "DAORIS_BRAINSTORM_TURN";
    public const string AnswerVariable = "DAORIS_BRAINSTORM_ANSWER";
    public const string RoomVariable = "DAORIS_BRAINSTORM_ROOM";

    /// <summary>What this process was spawned with.</summary>
    public static BrainstormScope FromEnvironment() => new(
        Blank(Environment.GetEnvironmentVariable(IdVariable)), Blank(Environment.GetEnvironmentVariable(TurnVariable)),
        Blank(Environment.GetEnvironmentVariable(AnswerVariable)), Blank(Environment.GetEnvironmentVariable(RoomVariable)));

    /// <summary>Whether this connector serves a brainstorm's turn: every part named.</summary>
    public bool Active => Id is not null && Turn is not null && Answer is not null && Room is not null;

    /// <summary>A synthesis's turn (<c>s1</c>, <c>s1r</c>) rather than a generator's (<c>g1</c>…).</summary>
    public bool Synthesis => Turn?.StartsWith('s') == true;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>What a result tool made of an answer: whether it is kept as valid, the codes it was refused with, and the agent's sentence.</summary>
public sealed record BrainstormKept(bool Kept, IReadOnlyList<string> Codes, string Message);

/// <summary>
/// Where a brainstorm pass's result tool writes its answer (BRAINSTORM1a2, D176 points 3 and 4): the one file the driver's spawn names,
/// outside the pass's room, which the driver reads when the turn ends. One writer, the turn's tool; the file the contract.
/// </summary>
/// <remarks>
/// <para><b>Judged as the driver judges it</b>, by <see cref="BrainstormChecks"/>, the twin of the driver's validator, both held to one
/// table (<c>fixtures/brainstorm-answers.json</c> among the driver's tests). A refused answer is said back with its codes so the agent
/// may give it again within its turn, and stays the file's until a valid one replaces it: a turn that ends refused is the driver's to
/// judge, and its one repair is handed the same codes. A valid answer is kept once; Daoris never repairs, trims or keeps part of one.</para>
///
/// <para><b>Judged against what the turn was handed</b>, read from its room: a generator's sources from <c>packet.json</c>, a
/// synthesis's anonymous ids from <c>proposals.json</c>. A room that holds neither judges nothing, and nothing is written.</para>
/// </remarks>
public sealed class BrainstormBox(BrainstormScope scope)
{
    public const string GiveTool = "brainstorm_give";
    public const string SynthesizeTool = "brainstorm_synthesize";

    public BrainstormScope Scope { get; } = scope;

    public static BrainstormBox FromEnvironment() => new(BrainstormScope.FromEnvironment());

    private const string NoTurn =
        "This connector serves no brainstorm turn Daoris started, so there is no answer to give here. Nothing was kept.";

    /// <summary>A generator's answer, as <c>brainstorm_give</c> composed it.</summary>
    public BrainstormKept Give(string text) => Keep(text, synthesis: false);

    /// <summary>A synthesis's answer, as <c>brainstorm_synthesize</c> composed it.</summary>
    public BrainstormKept Synthesize(string text) => Keep(text, synthesis: true);

    private BrainstormKept Keep(string text, bool synthesis)
    {
        if (!Scope.Active) return new(false, [], NoTurn);
        if (Scope.Synthesis != synthesis)
        {
            var own = Scope.Synthesis ? SynthesizeTool : GiveTool;
            return new(false, [], $"This turn is a brainstorm's {(Scope.Synthesis ? "synthesis" : "generator")}: answer through `{own}`. Nothing was kept.");
        }

        var handed = synthesis ? Handed(Scope.Room!, "proposals.json", "proposals") : Handed(Scope.Room!, "packet.json", "sources");
        if (handed is null)
        {
            var file = synthesis ? "proposals.json" : "packet.json";
            return new(false, [], $"This turn's room holds no readable `{file}`, so the answer cannot be judged. Nothing was kept.");
        }

        IReadOnlyList<string> Judge(string? answer) => synthesis
            ? BrainstormChecks.Synthesize(answer, handed)
            : BrainstormChecks.Give(answer, handed.ToHashSet(StringComparer.Ordinal));

        if (Read(Scope.Answer!) is { } before && Judge(before).Count == 0)
        {
            return new(false, [], "Your answer is already kept for this turn, and an answer is kept once. You can end now.");
        }

        var codes = Judge(text);
        Directory.CreateDirectory(Path.GetDirectoryName(Scope.Answer!)!);
        // As composed, byte for byte: the driver judges the same text, and a newline added here would count against its bound.
        AtomicFile.WriteText(Scope.Answer!, text);
        if (codes.Count > 0)
        {
            return new(false, codes,
                $"Daoris's validator refused this answer, with these codes: {string.Join(", ", codes)}. Give it again, whole, through "
                + "this tool before you end: an answer still refused when your turn ends fails the turn.");
        }

        return new(true, [], $"Your {(synthesis ? "synthesis" : "answer")} is kept: {Counted(text, synthesis)}. It is a claim the person "
            + "reads, and nobody can ask you anything after. You can end now.");
    }

    /// <summary>What a kept answer holds, in words: its proposals, or its groups and what it left out.</summary>
    private static string Counted(string text, bool synthesis)
    {
        using var document = JsonDocument.Parse(text);
        int Count(string name) => document.RootElement.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array ? list.GetArrayLength() : 0;
        if (synthesis)
        {
            var groups = Count("groups");
            return $"{groups} {(groups == 1 ? "group" : "groups")}, {Count("leftOut")} left out";
        }

        var proposals = Count("proposals");
        return proposals == 0 ? "no proposal, and why" : $"{proposals} {(proposals == 1 ? "proposal" : "proposals")}";
    }

    /// <summary>The ids a room's file lists under <paramref name="list"/>, or null where it does not read.</summary>
    private static IReadOnlyList<string>? Handed(string room, string file, string list)
    {
        try
        {
            var path = Path.Combine(room, file);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty(list, out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return [.. items.EnumerateArray().Select(item => BrainstormChecks.Text(item, "id")).OfType<string>()];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>
/// The service's validator of a brainstorm's answers (BRAINSTORM1a2, D176 points 3 and 4): by the schema, with no model, codes only.
/// </summary>
/// <remarks>
/// A TWIN of the driver's <c>BrainstormAnswers</c> and <c>BrainstormSyntheses</c>, which judge the answer file again when the turn
/// ends. They share no code: both are held, row for row, to ONE table, the driver suite's <c>fixtures/brainstorm-answers.json</c>, which
/// the driver's <c>BrainstormValidationTests</c> and this suite's <c>McpBrainstormToolTests</c> read. The codes, the bounds and the order
/// they are said in are the table's; a change here is a change there, in the same commit.
/// </remarks>
public static class BrainstormChecks
{
    private const int Schema = 1;
    private const int IdeaChars = 200;
    private const int RationaleChars = 1_200;
    private const int UndoChars = 400;
    private const int ListItems = 5;
    private const int ItemChars = 200;
    private const int MostProposals = 8;
    private const int AnswerChars = 20_000;
    private const int TitleChars = 200;
    private const int MergedChars = 400;
    private const int WhyChars = 600;

    private static readonly string[] Efforts = ["small", "medium", "large", "unknown"];
    private static readonly string[] Risks = ["low", "medium", "high", "unknown"];
    private static readonly string[] Reversibles = ["yes", "partly", "no", "unknown"];
    private static readonly string[] Confidences = ["low", "medium", "high"];
    private static readonly string[] Flags = ["hard-to-reverse", "risky"];

    /// <summary>A generator's answer against the packet's source ids: the codes it fails on, in order, each once; none for a valid one.</summary>
    public static IReadOnlyList<string> Give(string? text, IReadOnlySet<string> sources) =>
        Parsed(text, root => GiveCodes(root, sources));

    /// <summary>A synthesis against the anonymous ids its turn was handed: the codes it fails on, in order, each once.</summary>
    public static IReadOnlyList<string> Synthesize(string? text, IReadOnlyCollection<string> ids) =>
        Parsed(text, root => SynthesisCodes(root, ids.ToHashSet(StringComparer.Ordinal)));

    private static IReadOnlyList<string> Parsed(string? text, Func<JsonElement, IReadOnlyList<string>> judge)
    {
        if (text is null) return ["missing"];
        if (text.Length > AnswerChars) return ["too-long"];
        try
        {
            using var document = JsonDocument.Parse(text);
            return judge(document.RootElement);
        }
        catch (JsonException)
        {
            return ["shape"];
        }
    }

    private static bool Versioned(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("brainstorm", out var schema)
        && schema.ValueKind == JsonValueKind.Number && schema.TryGetInt32(out var version) && version == Schema;

    private static IReadOnlyList<string> GiveCodes(JsonElement root, IReadOnlySet<string> sources)
    {
        if (!Versioned(root)) return ["shape"];
        var codes = new List<string>();
        void Fail(string code)
        {
            if (!codes.Contains(code)) codes.Add(code);
        }

        var none = Words(root, "none", ItemChars, Fail);
        List(root, "needs", Fail);
        var given = root.TryGetProperty("proposals", out var list) ? list : default;
        if (given.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined)) Fail("shape");
        var items = given.ValueKind == JsonValueKind.Array ? given.EnumerateArray().ToList() : [];
        if (items.Count > MostProposals) Fail("too-many");
        if (items.Count > 0 && none is not null) Fail("proposals-beside-none");
        if (items.Count == 0 && none is null) Fail("empty");

        var ideas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                Fail("shape");
                continue;
            }

            var idea = Words(item, "idea", IdeaChars, Fail, required: true);
            Words(item, "rationale", RationaleChars, Fail, required: true);
            Scale(item, "effort", Efforts, Fail);
            Scale(item, "risk", Risks, Fail);
            var reversible = Scale(item, "reversible", Reversibles, Fail);
            Words(item, "undo", UndoChars, Fail, required: reversible is not null and not "yes");
            Scale(item, "confidence", Confidences, Fail);
            List(item, "assumptions", Fail);
            foreach (var id in Ids(item, Fail).Where(id => !sources.Contains(id))) Fail("unknown-source");
            if (idea is not null && !ideas.Add(Normal(idea))) Fail("idea-twice");
        }

        return codes;
    }

    private static IReadOnlyList<string> SynthesisCodes(JsonElement root, IReadOnlySet<string> known)
    {
        if (!Versioned(root) || !root.TryGetProperty("groups", out var given) || given.ValueKind != JsonValueKind.Array) return ["shape"];
        var codes = new List<string>();
        void Fail(string code)
        {
            if (!codes.Contains(code)) codes.Add(code);
        }

        var placed = new HashSet<string>(StringComparer.Ordinal);
        void Place(string id)
        {
            if (!known.Contains(id)) Fail("member-unknown");
            else if (!placed.Add(id)) Fail("member-twice");
        }

        var ranks = new List<int>();
        foreach (var item in given.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                Fail("shape");
                continue;
            }

            var title = Said(item, "title", TitleChars, Fail);
            var idea = Said(item, "idea", MergedChars, Fail);
            var why = Said(item, "why", WhyChars, Fail);
            var rank = item.TryGetProperty("rank", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var r) ? r : 0;
            var members = item.TryGetProperty("members", out var ids) && ids.ValueKind == JsonValueKind.Array
                ? ids.EnumerateArray().Select(id => id.ValueKind == JsonValueKind.String ? id.GetString()! : "").ToList()
                : null;
            if (members is null or { Count: 0 }) Fail("field:members");
            foreach (var id in members ?? []) Place(id);

            if (item.TryGetProperty("flags", out var added) && added.ValueKind == JsonValueKind.Array)
            {
                foreach (var flag in added.EnumerateArray())
                {
                    var name = Text(flag, "flag");
                    var reason = Text(flag, "why")?.Trim();
                    if (name is null || !Flags.Contains(name) || reason is not { Length: > 0 and <= WhyChars }) Fail("flag-unknown");
                }
            }

            if (title is null || idea is null || why is null || members is null) continue;
            ranks.Add(rank);
        }

        if (root.TryGetProperty("leftOut", out var left) && left.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in left.EnumerateArray())
            {
                var id = Text(item, "id") ?? "";
                Said(item, "why", WhyChars, Fail);
                Place(id);
            }
        }

        if (known.Any(id => !placed.Contains(id))) Fail("member-missing");
        ranks.Sort();
        if (!ranks.SequenceEqual(Enumerable.Range(1, ranks.Count))) Fail("ranks");
        return codes;
    }

    /// <summary>An idea's text as two answers' are compared: trimmed, spaces folded, case ignored.</summary>
    private static string Normal(string idea) =>
        string.Join(' ', idea.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>The string a field holds; null when missing, not a string, or the element no object.</summary>
    internal static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A generator's words: a missing or null field fails only where required; one blank, not a string or too long fails.</summary>
    private static string? Words(JsonElement item, string name, int bound, Action<string> fail, bool required = false)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required) fail($"field:{name}");
            return null;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : "";
        if (text.Length == 0)
        {
            fail($"field:{name}");
            return null;
        }

        if (text.Length > bound)
        {
            fail($"field:{name}-length");
            return null;
        }

        return text;
    }

    /// <summary>A synthesis's words: required, a string, not blank, within its bound.</summary>
    private static string? Said(JsonElement item, string name, int bound, Action<string> fail)
    {
        var text = Text(item, name)?.Trim();
        if (text is not { Length: > 0 })
        {
            fail($"field:{name}");
            return null;
        }

        if (text.Length > bound)
        {
            fail($"field:{name}-length");
            return null;
        }

        return text;
    }

    private static string? Scale(JsonElement item, string name, string[] scale, Action<string> fail)
    {
        var given = item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (given is not null && scale.Contains(given)) return given;
        fail($"field:{name}");
        return null;
    }

    private static void List(JsonElement item, string name, Action<string> fail)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return;
        if (value.ValueKind != JsonValueKind.Array)
        {
            fail($"field:{name}");
            return;
        }

        if (value.GetArrayLength() > ListItems) fail($"field:{name}-count");
        foreach (var each in value.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.String || each.GetString()!.Trim().Length == 0) fail($"field:{name}");
            else if (each.GetString()!.Trim().Length > ItemChars) fail($"field:{name}-length");
        }
    }

    private static IReadOnlyList<string> Ids(JsonElement item, Action<string> fail)
    {
        if (!item.TryGetProperty("sources", out var value) || value.ValueKind == JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            fail("field:sources");
            return [];
        }

        var ids = new List<string>();
        foreach (var each in value.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.String) fail("field:sources");
            else if (!ids.Contains(each.GetString()!)) ids.Add(each.GetString()!);
        }

        return ids;
    }
}
