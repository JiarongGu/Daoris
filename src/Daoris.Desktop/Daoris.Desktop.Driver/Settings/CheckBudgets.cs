using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// What this machine has learned of a repository's gates and what its owner set, kept in the Daoris home (<c>check-budgets</c>,
/// <c>data/check-budgets.json</c>), never in git: each gate's green wall times, the timeout derived from them, a pin over it with its
/// why, the load settings tuned to this machine (<c>memoryGb</c>, <c>alone</c>), a test set's <c>timeoutMs</c>, the tree baseline, the
/// devkit version, the local inputs and the patterns file's place. The file is
/// <c>{ schema: 1, repositories: { &lt;name&gt;: { gates, sets, treeBaseline, devkit, localInputs, patternsFile, … } } }</c>.
/// </summary>
/// <remarks>
/// <para>The merge tool writes the same file from Node (<c>tools/merge/check-budgets.ts</c>: the greens and what it said), and the
/// shared cases (<c>tools/merge/fixtures/check-budget-cases.json</c>) hold the two derivations to one answer. So each write here reads the
/// file again first and replaces it whole through <see cref="JsonFile"/>, and every key it does not model is kept as it stands.</para>
/// <para>A file that does not read is refused by its name, never read as empty: the budgets are not used until it does. A read with no
/// home, or none kept for the repository, gives the product's defaults. The repository is its name as the registry spells it, matched
/// exactly, as the tools match it.</para>
/// </remarks>
public static class CheckBudgets
{
    /// <summary>The timeout is the slowest ninetieth percentile of the recent greens times this, rounded up to a whole minute.</summary>
    public const double Factor = 1.5;

    /// <summary>The least a learned timeout is, in minutes.</summary>
    public const int FloorMinutes = 5;

    /// <summary>How many greens teach a timeout at all.</summary>
    public const int GreensNeeded = 3;

    /// <summary>The newest greens kept per gate.</summary>
    public const int GreensKept = 20;

    /// <summary>A learned timeout that rises past this many times the one last said is said again, once.</summary>
    public const double RiseBound = 1.25;

    /// <summary>The default timeout of the merge lane's gates, and of a gate whose lane is not known, in minutes.</summary>
    public const int MergeDefaultMinutes = 5;

    /// <summary>The default timeout of every other lane's gates, in minutes.</summary>
    public const int FullDefaultMinutes = 10;

    private const string MergeLane = "merge";

    /// <summary>A path the file keeps its inputs under: the family's machine-local, ignored folder.</summary>
    private const string Local = "local/";

    /// <summary>The file in <paramref name="home"/>.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.CheckBudgets);

    /// <summary>
    /// The repository's record: the product's defaults where there is no home, no file or nothing kept for it.
    /// </summary>
    /// <exception cref="DriverException">The file does not read, named.</exception>
    public static RepositoryBudgets Of(string? home, string? repository)
    {
        if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(repository)) return RepositoryBudgets.None;
        return Read(home) is { } root && Repositories(root, PathOf(home))[repository.Trim()] is JsonObject kept ? Parse(kept) : RepositoryBudgets.None;
    }

    /// <summary>
    /// A gate's budget in minutes: its pin, else what its green times teach, else the lane's default (<c>merge</c>, or no lane, five
    /// minutes; any other lane ten). A learned value that stands past <see cref="RiseBound"/> times the one last said carries the
    /// <see cref="GateTimeout.Rise"/>, which the caller says and then marks; a gate with nothing said yet rises from nothing.
    /// </summary>
    public static GateTimeout TimeoutOf(GateKept? kept, string? lane)
    {
        if (kept?.Pin is { } pin && Positive(pin.Minutes)) return new(pin.Minutes, "pin", pin.Why);
        var slowest = Slowest(kept?.Green);
        if (slowest is not { } ms) return new(lane is null or MergeLane ? MergeDefaultMinutes : FullDefaultMinutes, "default");
        var minutes = Math.Max(FloorMinutes, Math.Ceiling(ms * Factor / 60_000));
        return kept?.Said is { } said && Positive(said) && minutes > said * RiseBound
            ? new(minutes, "learned", null, new GateTimeoutRise(said, minutes))
            : new(minutes, "learned");
    }

    /// <summary>The nearest-rank ninetieth percentile, in milliseconds, of the newest greens; null where fewer than <see cref="GreensNeeded"/> are kept.</summary>
    private static double? Slowest(IReadOnlyList<GreenRun>? greens)
    {
        var recent = (greens ?? []).Where(each => Positive(each.Ms)).TakeLast(GreensKept).Select(each => each.Ms).Order().ToList();
        return recent.Count < GreensNeeded ? null : recent[(int)Math.Ceiling(0.9 * recent.Count) - 1];
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;

    // ── the door's writes: each judged before anything is written, and each a whole replacement of the file ──

    /// <summary>A gate's timeout pinned over the learned one, the reason recorded with it.</summary>
    /// <exception cref="DriverException">No whole number of minutes from one, no reason, or the file does not read.</exception>
    public static void SetPin(string home, string repository, string gate, double minutes, string? why)
    {
        if (!double.IsInteger(minutes) || minutes < 1 || minutes > int.MaxValue) throw new DriverException("a pin is a whole number of minutes, at least 1.");
        if (string.IsNullOrWhiteSpace(why)) throw new DriverException("say why the timeout is pinned (`--why`): the reason is recorded with it.");
        Change(home, repository, gate, "gate", each => each["pin"] = new JsonObject { ["minutes"] = minutes, ["why"] = why.Trim() });
    }

    /// <summary>A gate's pin taken away: its timeout follows its green times again.</summary>
    public static void ClearPin(string home, string repository, string gate) => Change(home, repository, gate, "gate", each => each.Remove("pin"));

    /// <summary>A gate's memory, in gigabytes, the admission reserves for it; null takes it away.</summary>
    /// <exception cref="DriverException">Not a positive number of gigabytes.</exception>
    public static void SetMemoryGb(string home, string repository, string gate, double? gigabytes)
    {
        if (gigabytes is { } gb && !Positive(gb)) throw new DriverException("a gate's memory is a positive number of gigabytes.");
        Change(home, repository, gate, "gate", each =>
        {
            if (gigabytes is { } kept) each["memoryGb"] = kept;
            else each.Remove("memoryGb");
        });
    }

    /// <summary>Whether a gate runs alone on this machine; false takes it away.</summary>
    public static void SetAlone(string home, string repository, string gate, bool alone) => Change(home, repository, gate, "gate", each =>
    {
        if (alone) each["alone"] = true;
        else each.Remove("alone");
    });

    /// <summary>A test set's per-test timeout in milliseconds, by <c>&lt;gate&gt;/&lt;set&gt;</c>; null takes it away.</summary>
    /// <exception cref="DriverException">Not a whole number of milliseconds from one.</exception>
    public static void SetSetTimeout(string home, string repository, string set, long? timeoutMs)
    {
        if (string.IsNullOrWhiteSpace(set)) throw new DriverException("a set's timeout is set on one set: name it, as `<gate>/<set>`.");
        if (timeoutMs is < 1) throw new DriverException("a set's timeout is a whole number of milliseconds, at least 1.");
        Change(home, repository, null, null, kept =>
        {
            var sets = kept["sets"] as JsonObject ?? (JsonObject)(kept["sets"] = new JsonObject());
            if (timeoutMs is { } ms) sets[set.Trim()] = new JsonObject { ["timeoutMs"] = ms };
            else sets.Remove(set.Trim());
        });
    }

    /// <summary>The command a tree's preparation records its base with; null takes it away.</summary>
    public static void SetTreeBaseline(string home, string repository, string? command) => SetText(home, repository, "treeBaseline", command);

    /// <summary>The devkit version this machine runs; null takes it away.</summary>
    public static void SetDevkit(string home, string repository, string? version) => SetText(home, repository, "devkit", version);

    /// <summary>
    /// The private files of this machine bound into a repository's trees, each under <c>local/</c> in git's spelling; null takes the
    /// member away (an older declaration's, if it holds one, is read again), an empty list says there are none.
    /// </summary>
    /// <exception cref="DriverException">A path that is not under <c>local/</c>, or has an empty, <c>.</c> or <c>..</c> part.</exception>
    public static void SetLocalInputs(string home, string repository, IReadOnlyList<string>? inputs)
    {
        var spelled = inputs?.Select(Spelled).ToList();
        foreach (var path in spelled ?? []) if (!UnderLocal(path)) throw new DriverException(NotUnderLocal(path));
        Change(home, repository, null, null, kept =>
        {
            if (spelled is null) kept.Remove("localInputs");
            else kept["localInputs"] = new JsonArray([.. spelled.Distinct(StringComparer.Ordinal).Select(path => (JsonNode?)JsonValue.Create(path))]);
        });
    }

    /// <summary>The place of the private patterns file, under <c>local/</c>; null takes it away.</summary>
    /// <exception cref="DriverException">Not a path under <c>local/</c>.</exception>
    public static void SetPatternsFile(string home, string repository, string? path)
    {
        var spelled = string.IsNullOrWhiteSpace(path) ? null : Spelled(path);
        if (spelled is not null && !UnderLocal(spelled)) throw new DriverException(NotUnderLocal(spelled));
        SetText(home, repository, "patternsFile", spelled);
    }

    /// <summary>A path in git's spelling: forward slashes, trimmed.</summary>
    public static string Spelled(string path) => path.Trim().Replace('\\', '/');

    /// <summary>
    /// A path under <c>local/</c> in git's spelling, every part of it a name: none empty, <c>.</c>, <c>..</c>, or holding a colon.
    /// The one place of the rule, which the tree's copy and the door both judge by.
    /// </summary>
    public static bool UnderLocal(string path) =>
        path.StartsWith(Local, StringComparison.Ordinal) && path.Split('/').All(part => part.Length > 0 && part is not ("." or "..") && !part.Contains(':'));

    private static string NotUnderLocal(string path) =>
        $"`{path}` is not a file under `local/`: Daoris binds into a tree only an ignored file there (no empty, `.` or `..` part).";

    private static void SetText(string home, string repository, string member, string? text) => Change(home, repository, null, null, kept =>
    {
        if (string.IsNullOrWhiteSpace(text)) kept.Remove(member);
        else kept[member] = text.Trim();
    });

    // ── the file ──

    /// <summary>
    /// One write: the file read again, <paramref name="change"/> applied to the repository's record (or to <paramref name="gate"/>'s in
    /// it, named for a refusal by <paramref name="what"/>), the empty entries it leaves taken away, the file replaced whole.
    /// </summary>
    private static void Change(string home, string repository, string? gate, string? what, Action<JsonObject> change)
    {
        if (string.IsNullOrWhiteSpace(home)) throw new DriverException("the check budgets are kept under Daoris's home, and none is named here.");
        if (string.IsNullOrWhiteSpace(repository)) throw new DriverException("a repository's check budgets are set for a repository: name one.");
        if (what is not null && string.IsNullOrWhiteSpace(gate)) throw new DriverException($"name the {what}.");
        var path = PathOf(home);
        var root = Read(home) ?? new JsonObject { ["schema"] = 1, ["repositories"] = new JsonObject() };
        var all = Repositories(root, path);
        var kept = all[repository.Trim()] as JsonObject ?? (JsonObject)(all[repository.Trim()] = new JsonObject());
        if (gate is null)
        {
            change(kept);
        }
        else
        {
            var gates = kept["gates"] as JsonObject ?? (JsonObject)(kept["gates"] = new JsonObject());
            change(gates[gate.Trim()] as JsonObject ?? (JsonObject)(gates[gate.Trim()] = new JsonObject()));
        }

        Tidy(kept);
        JsonFile.Write(path, root);
    }

    /// <summary>The empty gate and set entries a clearing leaves, and the empty maps, taken away.</summary>
    private static void Tidy(JsonObject kept)
    {
        foreach (var member in new[] { "gates", "sets" })
        {
            if (kept[member] is not JsonObject map) continue;
            foreach (var name in map.Where(each => each.Value is JsonObject { Count: 0 }).Select(each => each.Key).ToList()) map.Remove(name);
            if (map.Count == 0) kept.Remove(member);
        }
    }

    /// <summary>The file's object, or null where there is no file.</summary>
    private static JsonObject? Read(string home)
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonNode.Parse(AtomicFile.ReadText(path)) as JsonObject ?? throw new JsonException("it is not a JSON object");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            throw Refused(path, error.Message);
        }
    }

    private static JsonObject Repositories(JsonObject root, string path) =>
        root["repositories"] as JsonObject ?? throw Refused(path, "it has no repositories object");

    private static DriverException Refused(string path, string why) =>
        new($"{path} does not read ({why}): the machine's check budgets are not used until it does. Fix it, or move it aside to start again.");

    // ── the typed reading ──

    private static RepositoryBudgets Parse(JsonObject kept)
    {
        var gates = new Dictionary<string, GateKept>(StringComparer.Ordinal);
        if (kept["gates"] is JsonObject all)
        {
            foreach (var (name, node) in all) if (node is JsonObject gate) gates[name] = ParseGate(gate);
        }

        var sets = new Dictionary<string, long>(StringComparer.Ordinal);
        if (kept["sets"] is JsonObject known)
        {
            foreach (var (name, node) in known)
            {
                if (Number(node is JsonObject set ? set["timeoutMs"] : null) is { } ms && double.IsInteger(ms) && ms >= 1 && ms <= long.MaxValue) sets[name] = (long)ms;
            }
        }

        var inputs = kept["localInputs"] is JsonArray list
            ? (IReadOnlyList<string>)[.. list.Select(Text).OfType<string>().Select(Spelled).Distinct(StringComparer.Ordinal)]
            : null;
        return new(gates, sets, Text(kept["treeBaseline"]), Text(kept["devkit"]), inputs, Text(kept["patternsFile"]));
    }

    private static GateKept ParseGate(JsonObject gate)
    {
        var greens = gate["green"] is JsonArray list
            ? list.OfType<JsonObject>().Select(each => Number(each["ms"]) is { } ms ? new GreenRun(ms, Text(each["at"]) ?? "") : null).OfType<GreenRun>().ToList()
            : [];
        var pin = gate["pin"] is JsonObject pinned && Number(pinned["minutes"]) is { } minutes ? new GatePin(minutes, Text(pinned["why"]) ?? "") : null;
        return new(greens, pin, Number(gate["memoryGb"]), gate["alone"] is JsonValue alone && alone.TryGetValue<bool>(out var flag) && flag, Number(gate["said"]));
    }

    private static double? Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number) ? number : null;

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;

    /// <summary>A number as the door's words spell it.</summary>
    internal static string Said(double number) => number.ToString("0.###", CultureInfo.InvariantCulture);
}
