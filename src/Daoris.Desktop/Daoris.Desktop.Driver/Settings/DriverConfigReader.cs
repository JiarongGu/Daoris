using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>driver.json</c> read into the choices. By hand, for the same reason the registration store writes by hand: nothing here may
/// quietly stop working under AOT, and the shape is small enough to be explicit about.
/// </summary>
internal static class DriverConfigReader
{
    public static DriverConfig Parse(string json)
    {
        // A key written twice in one object reads as its last value in its first place, as the CLI's JSON.parse reads it;
        // the text read stays as written, and no edit is written over it (Save).
        using var document = JsonDocument.Parse(JsonKeeping.Once(json));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("it holds no JSON object");
        var (actors, actorErrors) = ActorKey.Read(root);
        var empty = DriverConfig.Empty;

        return new DriverConfig(
            Drivable: Json.Strings(root,"drivable"),
            Holds: Json.Strings(root,"holds"),
            Cap: Math.Max(1, Int(root, "cap") ?? empty.Cap),
            PollSeconds: Math.Max(1, Int(root, "pollSeconds") ?? empty.PollSeconds),
            Commands: CommandMap(root),
            // Absent means ON: every machine that already has a driver.json predates this
            // field, and reading silence as "off" would ship the feature switched off everywhere it
            // matters most — a machine that has been driving for a while.
            Notify: Bool(root, "notify") ?? empty.Notify,
            Forgiven: ForgivenMap(root))
        {
            // The text read, which the writer keeps every key it does not own from.
            Source = json,
            // Out of its bounds, or not a whole number, is the default: never a bound the doors would refuse.
            QuietMinutes = root.TryGetProperty(QuietSetting.File, out var quiet) && quiet.ValueKind == JsonValueKind.Number
                ? QuietSetting.Read(quiet.TryGetInt64(out var whole) ? whole : null) : null,
            // Each actor's order: an entry that does not read is kept as its error, and holds its starts.
            Actors = actors,
            ActorErrors = actorErrors,
            // Restricted unless the file names full: never widened by a value it does not know.
            HelpMode = HelpModeNames.Named(Json.NonEmpty(root, "helpMode")) == HelpModeNames.Full ? HelpModeNames.Full : HelpModeNames.Restricted,
            Terminal = TerminalTiers.Read(Json.NonEmpty(root, TerminalTiers.File)),
            // Absent, or less than a minute, or not a whole number, is the default hour: never a spin.
            CoolOffMinutes = root.TryGetProperty("cooloff", out var coolOff) && coolOff.ValueKind == JsonValueKind.Number
                && coolOff.TryGetInt32(out var minutes) && minutes >= 1 ? minutes : null,
            Released = SessionMap(root, "released"),
            TransientReleased = TransientReleaseMap(root, "transientReleased"),
            RoleReleases = RoleReleaseMap(root, "roleReleases"),
            // Each set-up quest against its repository, read as a release's session is.
            Admitted = SessionMap(root, "admitted"),
            PausedAsks = PauseMap(root, "pausedAsks"),
            PausedQuests = PauseMap(root, "pausedQuests"),
            Standing = StandingMap(root, "standing"),
            Lines = BranchMap(root, "lines"),
            WorkspaceLines = BranchMap(root, "workspaceLines"),
            Languages = LanguageMap(root, "languages"),
            WorkspaceLanguages = LanguageMap(root, "workspaceLanguages"),
            // A rule with a problem is not read, and `false` only for a repository.
            Reviews = ReviewKey.Map(root, "reviews", allowNone: true),
            WorkspaceReviews = ReviewKey.Map(root, "workspaceReviews", allowNone: false),
            // A rule with a problem is not read, and `false` only for a repository.
            Opinions = OpinionKey.Map(root, "opinions", allowNone: true),
            WorkspaceOpinions = OpinionKey.Map(root, "workspaceOpinions", allowNone: false),
            Helpers = HelperKey.Map(root, "helpers"),
            WorkspaceHelpers = HelperKey.Map(root, "workspaceHelpers"),
            HandOver = HandOverKey.Map(root, "handOver"),
            WorkspaceHandOver = HandOverKey.Map(root, "workspaceHandOver"),
            Brainstorms = BrainstormKey.Machine(root),
            WorkspaceBrainstorms = BrainstormKey.Map(root, "workspaceBrainstorms"),
            Commits =CommitKey.Map(root, "commits"),
            WorkspaceCommits = CommitKey.Map(root, "workspaceCommits"),
            // An entry with a problem, or naming nothing, is not read.
            Workflows = WorkflowKey.Map(root, "workflows", workspace: false),
            WorkspaceWorkflows = WorkflowKey.Map(root, "workspaceWorkflows", workspace: true),
            // A default that is no workflow's id is not read.
            DefaultWorkflow = root.TryGetProperty("defaultWorkflow", out var homeDefault) && homeDefault.ValueKind == JsonValueKind.String
                && homeDefault.GetString() is { } homeId && homeId != WorkflowKey.Current && WorkflowKey.IsId(homeId) ? homeId : null,
            LinePolicies = LinePolicyKey.PolicyMap(root, "linePolicies"),
            WorkspaceLinePolicies = LinePolicyKey.PolicyMap(root, "workspaceLinePolicies"),
            // Read as a stored shape: a value that is no blob id and not `none` is not read, and shadows none.
            IntegrationGates = IntegrationGatesKey.Map(root),
            // Read as a stored shape: a pause this build does not know passed over, a value it does not know read as today's.
            Access = AccessKey.Map(root, "access"),
            WorkspaceAccess = AccessKey.Map(root, "workspaceAccess"),
            MachineAccess = AccessKey.Machine(root),
            // Read as a stored shape: a list with a gate that does not read is not read, whole.
            HeavyGates = HeavyKey.Map(root),
            // Read as a stored shape: a value that is no choice is not read, and shadows none.
            SessionChecks = SessionChecksKey.Map(root),
            // Read as a stored shape: a value out of its kind's bounds is not read, and its kind keeps its default.
            Keep = KeepChoices.Read(root),
            ReadAcross = FlagMap(root, "readAcross"),
            WorkspaceReadAcross = FlagMap(root, "workspaceReadAcross"),
            CheckoutWrites = FlagMap(root, "checkoutWrites"),
            WriteAcross = TargetMap(root, "writeAcross"),
        };
    }

    /// <summary>A role's releases as written: an entry missing any of its four fields is none; a quest named twice is read where first written.</summary>
    private static IReadOnlyDictionary<string, RoleRelease> RoleReleaseMap(JsonElement container, string name)
    {
        var released = new Dictionary<string, RoleRelease>(StringComparer.OrdinalIgnoreCase);
        if (!container.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return released;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Length == 0 || released.ContainsKey(property.Name) || property.Value.ValueKind != JsonValueKind.Object) continue;
            if (Json.NonEmpty(property.Value, "kind") is not { } kind || Json.NonEmpty(property.Value, "session")?.Trim() is not { Length: > 0 } session
                || Json.NonEmpty(property.Value, "by") is not { } by || Json.NonEmpty(property.Value, "act") is not { } act)
            {
                continue;
            }

            released[property.Name] = new RoleRelease(kind, session, by, act);
        }

        return released;
    }

    /// <summary>
    /// The transient releases as written: an entry with no session or no <c>ended</c> that reads as a moment is none,
    /// so a release whose end is unsaid passes nothing; a quest named twice in any case is read where first written.
    /// </summary>
    private static IReadOnlyDictionary<string, TransientRelease> TransientReleaseMap(JsonElement container, string name)
    {
        var released = new Dictionary<string, TransientRelease>(StringComparer.OrdinalIgnoreCase);
        if (!container.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return released;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Length == 0 || released.ContainsKey(property.Name) || property.Value.ValueKind != JsonValueKind.Object) continue;
            if (Json.NonEmpty(property.Value, "session")?.Trim() is not { Length: > 0 } session) continue;
            if (Json.NonEmpty(property.Value, "ended") is not { } ended
                || !DateTimeOffset.TryParse(ended, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at))
            {
                continue;
            }

            released[property.Name] = new TransientRelease(session, at);
        }

        return released;
    }

    /// <summary>
    /// The pauses: each id an object, read where first written in any case. Its <c>at</c> is read only as ISO 8601
    /// writes a moment, and a time that does not read leaves the pause standing with its time unknown: the pause is the
    /// person's, and the time only says when. Its <c>stopped</c> is read as <c>released</c> is.
    /// </summary>
    private static IReadOnlyDictionary<string, WorkPause> PauseMap(JsonElement root, string name)
    {
        var pauses = new Dictionary<string, WorkPause>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return pauses;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object || property.Name.Length == 0 || pauses.ContainsKey(property.Name)) continue;
            pauses[property.Name] = new WorkPause(Moment(Json.NonEmpty(property.Value,"at")), SessionMap(property.Value, "stopped"));
        }

        return pauses;
    }

    /// <summary>
    /// The standing answers: each repository an object whose <c>says</c> is text, read without the spaces around it,
    /// and read where first written in any case. Its <c>at</c> is read only as ISO 8601 writes a moment, and a time that does not
    /// read leaves the answer standing with its time unknown. Blank words, an entry that is not an object, and a map that is not
    /// one are none.
    /// </summary>
    private static IReadOnlyDictionary<string, StandingAnswer> StandingMap(JsonElement root, string name)
    {
        var standing = new Dictionary<string, StandingAnswer>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return standing;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object || property.Name.Trim().Length == 0 || standing.ContainsKey(property.Name)) continue;
            if (!property.Value.TryGetProperty("says", out var says) || says.ValueKind != JsonValueKind.String
                || says.GetString()?.Trim() is not { Length: > 0 } words)
            {
                continue;
            }

            standing[property.Name] = new StandingAnswer(words, Moment(Json.NonEmpty(property.Value,"at")));
        }

        return standing;
    }

    /// <summary>
    /// A moment as ISO 8601 writes it, and only so: to the second, with a fraction or not, in UTC or at an offset. The same
    /// forms <see cref="AccountCooling"/> reads, and the CLI's <c>cooling.ts</c> (<c>isoMoment</c>): a lenient parse
    /// would read <i>Oct 2</i> as this year's.
    /// </summary>
    private static DateTimeOffset? Moment(string? text) => Moments.ReadStrict(text);

    /// <summary>
    /// A map of names to booleans; an entry of any other type is not read. A name written twice in any case is read where first
    /// readable, as the CLI reads it.
    /// </summary>
    private static IReadOnlyDictionary<string, bool> FlagMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False && property.Name.Trim() is { Length: > 0 } named
                    && !map.ContainsKey(named))
                {
                    map[named] = property.Value.GetBoolean();
                }
            }
        }

        return map;
    }

    /// <summary>
    /// The declared relationships: each repository's list of other names, once each in any case, in
    /// the order first written. A list that is not one, an entry that is not a name, and the repository
    /// itself are not read; a repository left with none has no entry. A repository written twice in any case
    /// is read where first readable, as the CLI reads it.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> TargetMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array || property.Name.Trim().Length == 0 || map.ContainsKey(property.Name.Trim())) continue;
            var targets = new List<string>();
            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString()?.Trim() is { Length: > 0 } target
                    && AcrossDeclarations.Problem(property.Name, target) is null
                    && !targets.Contains(target, StringComparer.OrdinalIgnoreCase))
                {
                    targets.Add(target);
                }
            }

            if (targets.Count > 0) map[property.Name.Trim()] = targets;
        }

        return map;
    }

    /// <summary>
    /// The session languages: each name a code of the table, read in any case without the spaces around it. A code
    /// the table does not hold, a value that is not text, a blank name and a map that is not one are not read; a name written
    /// twice in any case is read where first written.
    /// </summary>
    private static IReadOnlyDictionary<string, string> LanguageMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String && SessionLanguages.Code(property.Value.GetString()) is { } code
                && property.Name.Trim() is { Length: > 0 } named && !map.ContainsKey(named))
            {
                map[named] = code;
            }
        }

        return map;
    }

    /// <summary>
    /// A map of names to branch names; an entry that is not one git would take is skipped. A name written twice in any case is read
    /// where first readable, as the CLI reads it.
    /// </summary>
    private static IReadOnlyDictionary<string, string> BranchMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String
                    && property.Value.GetString() is { } branch && BranchName.IsValid(branch) && !map.ContainsKey(property.Name))
                {
                    map[property.Name] = branch;
                }
            }
        }

        return map;
    }

    /// <summary>
    /// The releases, and a pause's stops: each quest against a session's id, read without the spaces
    /// around it; the admissions against a repository's name alike. A blank value, one that is not text, a map
    /// that is not one, and a quest written again in another case are not read.
    /// </summary>
    private static IReadOnlyDictionary<string, string> SessionMap(JsonElement container, string name)
    {
        var released = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!container.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return released;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()?.Trim() is { Length: > 0 } session
                && property.Name.Length > 0 && !released.ContainsKey(property.Name))
            {
                released[property.Name] = session;
            }
        }

        return released;
    }

    private static IReadOnlyDictionary<string, int> ForgivenMap(JsonElement root)
    {
        var forgiven = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("forgiven", out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                // A quest written twice in any case is read where first readable, as the CLI reads it.
                if (property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out var mark) && mark > 0 && !forgiven.ContainsKey(property.Name))
                {
                    forgiven[property.Name] = mark;
                }
            }
        }

        return forgiven;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CommandMap(JsonElement root)
    {
        var commands = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("commands", out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var items = new List<string>();
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text)
                        {
                            items.Add(text);
                        }
                    }
                }

                commands[property.Name] = items;
            }
        }

        return commands;
    }

    private static bool? Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    // A number that is not a whole one is refused in words that name the field: the
    // runtime's own says only that an item is in an invalid format.
    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.TryGetInt32(out var number) ? number : throw new JsonException($"`{name}` is not a whole number")
            : null;
}
