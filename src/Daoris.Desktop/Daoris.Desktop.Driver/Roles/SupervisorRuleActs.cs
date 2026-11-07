using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A rule of the person's that acted at a point on a line: the rule, the revision of its place's set,
/// the count it watched and what that stood at, the point it acted at, and when.
/// </summary>
/// <param name="Point">Where it acted: <c>go-on</c>, <c>start</c> or <c>episode</c>.</param>
public sealed record RuleActed(string Line, string Session, SupervisorRule Rule, long Revision, int N, bool Known, string Point, DateTimeOffset At)
{
    /// <summary>The decision's id: its time and its count, so two acts on one line never share a file.</summary>
    public string Id => $"rule-{At.UtcDateTime:yyyyMMddHHmmssfff}-{Rule.Count}";
}

/// <summary>
/// The history of the rules' acts: each act said once on the line as a <c>supervisor.rule</c> note, kept in the decision record beside the
/// questions' decisions as <c>&lt;session&gt;.decisions/&lt;id&gt;.json</c> with <c>holder.kind</c> <c>rule</c>, and logged on the machine.
/// A rule's file carries no <c>question</c> and no state lines: it is final as written, so the questions' reader and the start's sweep pass
/// it by. It is read for the history alone, never to decide.
/// </summary>
public sealed class SupervisorRuleActs(string home, ISessionEvents? events = null, MachineLog? log = null)
{
    /// <summary>The holder kind a rule's decision file names.</summary>
    public const string HolderKind = "rule";

    private SupervisorDecisionFiles Files => new(home);

    /// <summary>Say <paramref name="act"/> on its line, keep its decision file, and log it. A file that cannot be written is logged and the act stands.</summary>
    public void Record(RuleActed act)
    {
        events?.Keep(act.Session, Note(act), say: null);
        try
        {
            Directory.CreateDirectory(Files.FolderOf(act.Session));
            AtomicFile.WriteText(Files.FileOf(act.Session, act.Id), Json(act));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            log?.Warn("supervisor.rule-unkept", ("session", act.Session), ("why", error.Message));
        }

        log?.Info("supervisor.rule", ("line", act.Line), ("session", act.Session), ("rule", act.Rule.Id), ("count", act.Rule.Count),
            ("n", act.N), ("known", act.Known), ("do", act.Rule.Do));
    }

    /// <summary>The rules' acts on <paramref name="session"/>, oldest first, read from its decision folder by their holder kind.</summary>
    public IReadOnlyList<RuleActed> Of(string session)
    {
        var folder = Files.FolderOf(session);
        if (!Directory.Exists(folder)) return [];
        var acts = new List<RuleActed>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            if (SupervisorDecisionFiles.Read(file) is { } text && Parse(text) is { } act) acts.Add(act);
        }

        return [.. acts.OrderBy(act => act.At)];
    }

    /// <summary>The line's note: the count, what it stood at, the rule's number, its place and its act.</summary>
    public static SessionEvent Note(RuleActed act)
    {
        var noted = Noted.Of(
            HeldNoteCodes.SupervisorRule,
            $"Your rule for {act.Rule.Place} acted on this line: {act.Rule.Count} stood at {act.N}, and the rule says {act.Rule.Do} at {act.Rule.At}.",
            ("counted", act.Rule.Count), ("n", act.N.ToString(CultureInfo.InvariantCulture)), ("bound", act.Rule.At.ToString(CultureInfo.InvariantCulture)),
            ("place", act.Rule.Place), ("do", act.Rule.Do));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts };
    }

    /// <summary>The decision file's body: the holder, the counts read, the point, the act and its time; no question and no state.</summary>
    public static string Json(RuleActed act) => new JsonObject
    {
        ["id"] = act.Id,
        ["holder"] = new JsonObject { ["kind"] = HolderKind, ["rule"] = act.Rule.Id, ["revision"] = act.Revision, ["place"] = act.Rule.Place },
        ["line"] = act.Line,
        ["session"] = act.Session,
        ["counts"] = new JsonArray(new JsonObject { ["count"] = act.Rule.Count, ["n"] = act.N, ["known"] = act.Known }),
        ["point"] = act.Point,
        ["rule"] = JsonSerializer.SerializeToNode(act.Rule),
        ["do"] = act.Rule.Do,
        ["at"] = act.At.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>A rule's act read back, or null for any other file (a question's decision, or one that does not read).</summary>
    public static RuleActed? Parse(string text)
    {
        try
        {
            if (JsonNode.Parse(text) is not JsonObject root || root["holder"]?["kind"]?.GetValue<string>() != HolderKind || root.ContainsKey("question")) return null;
            var rule = root["rule"].Deserialize<SupervisorRule>();
            var count = root["counts"]?.AsArray().FirstOrDefault();
            if (rule is null || count is null || root["at"]?.GetValue<string>() is not { } at) return null;
            return new RuleActed(
                root["line"]?.GetValue<string>() ?? "", root["session"]?.GetValue<string>() ?? "", rule, root["holder"]!["revision"]?.GetValue<long>() ?? 0,
                count["n"]?.GetValue<int>() ?? 0, count["known"]?.GetValue<bool>() ?? true, root["point"]?.GetValue<string>() ?? "",
                DateTimeOffset.Parse(at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
