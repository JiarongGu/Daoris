using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One pause a repository's sessions may stop for (AUTONOMY1a, D184 point 3): its name as the driver's file keeps it, its values in
/// the order a person is offered them, the value that is today's behaviour (<see cref="Default"/>) and the one full access takes
/// (<see cref="Open"/>).
/// </summary>
public sealed record AccessPause(string Name, IReadOnlyList<string> Values, string Default, string Open);

/// <summary>
/// One level's access setup as the driver's file keeps it (AUTONOMY1a, D184 points 1-3): full access, or the pauses it names, each
/// with its value. A pause it does not name is the next level's. Never both: a pause set over full access writes every pause.
/// </summary>
public sealed record AccessSetting
{
    /// <summary>Every pause off (D184 point 2): what the file's <c>"full"</c> says.</summary>
    public static AccessSetting FullAccess { get; } = new() { Full = true };

    /// <summary>Whether this level is full access: every pause at its <see cref="AccessPause.Open"/> value.</summary>
    public bool Full { get; init; }

    /// <summary>The pauses this level names, by <see cref="AccessPause.Name"/>, each with a value of its own; none where <see cref="Full"/>.</summary>
    public IReadOnlyDictionary<string, string> Pauses { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>One pause as it resolves for a repository: its value, and where it was set (<see cref="AccessRules.FromRepository"/> and the rest).</summary>
public sealed record ResolvedPause(string Pause, string Value, string Source);

/// <summary>A repository's access setup as it resolves (D184 point 1), pause by pause in <see cref="AccessRules.Pauses"/>' order.</summary>
public sealed record ResolvedAccess(IReadOnlyList<ResolvedPause> Pauses)
{
    /// <summary>The value <paramref name="pause"/> resolves to.</summary>
    public string Value(string pause) => Pauses.First(each => each.Pause == pause).Value;

    /// <summary>Where <paramref name="pause"/>'s value was set.</summary>
    public string Source(string pause) => Pauses.First(each => each.Pause == pause).Source;

    /// <summary>Whether every pause is off: each at the value full access takes, from whichever level.</summary>
    public bool Full => Pauses.All(each => each.Value == AccessRules.Pause(each.Pause).Open);

    /// <summary>Whether every pause is today's behaviour (D184 point 5), set or not.</summary>
    public bool Today => Pauses.All(each => each.Value == AccessRules.Pause(each.Pause).Default);

    /// <summary>Each pause's value, as a record keeps the setup a session started under.</summary>
    public IReadOnlyDictionary<string, string> Values => Pauses.ToDictionary(each => each.Pause, each => each.Value, StringComparer.Ordinal);
}

/// <summary>
/// One change to an access setup, as every door makes it: at a <see cref="Repository"/>, a <see cref="Workspace"/> or this
/// <see cref="Machine"/>; full access, the pauses it names (a blank or null value taking that pause away), or clear.
/// </summary>
public sealed record AccessEdit
{
    public string? Repository { get; init; }
    public string? Workspace { get; init; }
    public bool Machine { get; init; }
    public bool Full { get; init; }
    public IReadOnlyList<KeyValuePair<string, string?>>? Set { get; init; }
    public bool Clear { get; init; }
}

/// <summary>
/// A repository's session access setup (AUTONOMY1a, D184 points 1-3, 5): <c>access</c> (by repository), <c>workspaceAccess</c> (by
/// workspace) and <c>machineAccess</c> in <c>driver.json</c>; read, resolved pause by pause, said, written and edited, the one place
/// the route, the terminal and a start ask.
/// </summary>
/// <remarks>
/// <para><b>Each pause resolves on its own</b>: the repository's, else its workspace's, else this machine's, else today's behaviour
/// (<see cref="AccessPause.Default"/>), and the answer names where it came from. A level that is full access gives every pause its
/// open value. Unset everywhere is today's behaviour, so nothing loosens by itself (D184 point 5).</para>
///
/// <para><b>Read as a stored shape</b>: a pause this build does not know is passed over, and a value it does not know reads as the
/// pause's default at that level, never as the next level's, which may be looser. Both stay in the file as written, since an edit
/// changes only the key paths it changed. A level of another shape is not read.</para>
///
/// <para><b>Not here: the effects.</b> What a pause changes in a session (the permission prompts, AUTONOMY1b; the routing, the
/// landing and the go-aheads, AUTONOMY1c; the questions, AUTONOMY1e) reads <see cref="Resolve"/> where it acts. A go-ahead the ask
/// itself demands is asked whatever this says (D184 point 5).</para>
/// </remarks>
public static class AccessRules
{
    /// <summary>The file's word for full access at a level, and a door's.</summary>
    public const string FullWord = "full";

    public const string Permissions = "permissions";
    public const string Routing = "routing";
    public const string Landing = "landing";
    public const string GoAheads = "goAheads";
    public const string Questions = "questions";

    /// <summary>Permission prompts: the harness's asking mode.</summary>
    public const string Asking = "asking";

    /// <summary>Permission prompts: the harness's auto mode, today's (D81).</summary>
    public const string Auto = "auto";

    /// <summary>Permission prompts: the harness's unrestricted mode (D183's full postures).</summary>
    public const string Unrestricted = "unrestricted";

    /// <summary>Routing and landing: the person's press, today's.</summary>
    public const string Person = "person";

    /// <summary>Routing: the ask goes where the intake chose.</summary>
    public const string Intake = "intake";

    /// <summary>Landing: the repository's landing rule lands it.</summary>
    public const string Rule = "rule";

    /// <summary>Go-aheads: asked before the acts the repository names, today's.</summary>
    public const string Named = "named";

    /// <summary>Go-aheads: never asked, save what the ask itself demands (D184 point 5).</summary>
    public const string Never = "never";

    /// <summary>Questions: as Claude Code's console asks them (D184 point 4), today's behaviour until AUTONOMY1e.</summary>
    public const string ClaudeCode = "claude-code";

    /// <summary>The pauses, in the order every door and the record say them.</summary>
    public static IReadOnlyList<AccessPause> Pauses { get; } =
    [
        new(Permissions, [Asking, Auto, Unrestricted], Auto, Unrestricted),
        new(Routing, [Person, Intake], Person, Intake),
        new(Landing, [Person, Rule], Person, Rule),
        new(GoAheads, [Named, Never], Named, Never),
        new(Questions, [ClaudeCode], ClaudeCode, ClaudeCode),
    ];

    public const string FromRepository = "repository";
    public const string FromWorkspace = "workspace";
    public const string FromMachine = "machine";
    public const string DefaultSource = "default";

    internal const string Scope = "an access setup is set for a repository, a workspace or this machine — name exactly one.";
    private const string OneAtATime = "one change at a time: full access, the pauses it names with their values, or clear.";
    private const string Shape = "an access setup is `full`, or the pauses it names, each with its value — or none, to clear it.";

    /// <summary>The pause named <paramref name="name"/>, as the file and every door spell it.</summary>
    public static AccessPause Pause(string name) => Pauses.First(pause => pause.Name == name);

    /// <summary>The pause a spelling names, trimmed and in any case, or null for none.</summary>
    public static AccessPause? PauseNamed(string? spelled) =>
        Pauses.FirstOrDefault(pause => string.Equals(pause.Name, spelled?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The value of <paramref name="pause"/> a spelling names, trimmed and in any case, or null for none.</summary>
    public static string? ValueNamed(AccessPause pause, string? spelled) =>
        pause.Values.FirstOrDefault(value => string.Equals(value, spelled?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The refusal for a pause the setup does not have.</summary>
    public static string UnknownPause(string spelled) =>
        $"`{spelled.Trim()}` is not a pause a session stops for — one of {Listed(Pauses.Select(pause => pause.Name).ToList(), "or")}.";

    /// <summary>The refusal for a value <paramref name="pause"/> does not have.</summary>
    public static string UnknownValue(AccessPause pause, string spelled) => $"`{pause.Name}` is {Listed(pause.Values, "or")}, not `{spelled.Trim()}`.";

    private static string Listed(IReadOnlyList<string> words, string last) => words.Count == 1
        ? $"`{words[0]}`"
        : string.Join(", ", words.Take(words.Count - 1).Select(word => $"`{word}`")) + $" {last} `{words[^1]}`";

    /// <summary>
    /// A level as the file keeps it, read as a stored shape: <c>"full"</c> in any case, or an object of pauses. A pause this build does
    /// not know is passed over; a value it does not know, or one that is no word, reads as the pause's default; a null value is
    /// unset. Null where the level is of another shape.
    /// </summary>
    internal static AccessSetting? Kept(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return string.Equals(value.GetString()?.Trim(), FullWord, StringComparison.OrdinalIgnoreCase) ? AccessSetting.FullAccess : null;
        }

        if (value.ValueKind != JsonValueKind.Object) return null;
        var pauses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (PauseNamed(property.Name) is not { } pause || pauses.ContainsKey(pause.Name) || property.Value.ValueKind == JsonValueKind.Null) continue;
            pauses[pause.Name] = property.Value.ValueKind == JsonValueKind.String && ValueNamed(pause, property.Value.GetString()) is { } named
                ? named
                : pause.Default;
        }

        return new AccessSetting { Pauses = pauses };
    }

    /// <summary>A map of levels as <c>driver.json</c> holds it: each name trimmed, a blank one and a level not read passed over, a name written twice in any case read where first readable.</summary>
    internal static IReadOnlyDictionary<string, AccessSetting> Map(JsonElement root, string name)
    {
        var map = new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || Kept(property.Value) is not { } setting || map.ContainsKey(named)) continue;
            map[named] = setting;
        }

        return map;
    }

    /// <summary>This machine's level as <c>driver.json</c> holds it, <c>machineAccess</c>: null where it is absent or not read.</summary>
    internal static AccessSetting? Machine(JsonElement root) =>
        root.TryGetProperty("machineAccess", out var element) ? Kept(element) : null;

    /// <summary>A map written as <c>driver.json</c> keeps it, only when set.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, AccessSetting> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, setting) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key);
            Write(writer, setting);
        }

        writer.WriteEndObject();
    }

    /// <summary>This machine's level written as <c>driver.json</c> keeps it, only when set.</summary>
    internal static void WriteMachine(Utf8JsonWriter writer, AccessSetting? setting)
    {
        if (setting is null) return;
        writer.WritePropertyName("machineAccess");
        Write(writer, setting);
    }

    /// <summary>One level as kept: <c>"full"</c>, or its pauses in <see cref="Pauses"/>' order.</summary>
    public static void Write(Utf8JsonWriter writer, AccessSetting setting)
    {
        if (setting.Full)
        {
            writer.WriteStringValue(FullWord);
            return;
        }

        writer.WriteStartObject();
        foreach (var pause in Pauses)
        {
            if (setting.Pauses.TryGetValue(pause.Name, out var value)) writer.WriteString(pause.Name, value);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Each pause as it resolves (D184 point 1): <paramref name="repository"/>'s, else <paramref name="workspace"/>'s, else
    /// <paramref name="machine"/>'s, else today's behaviour from <see cref="DefaultSource"/>. A pure function of the three levels.
    /// </summary>
    public static ResolvedAccess ResolveLevels(AccessSetting? repository, AccessSetting? workspace, AccessSetting? machine) =>
        new([
            .. Pauses.Select(pause =>
                Of(pause, repository, FromRepository) ?? Of(pause, workspace, FromWorkspace) ?? Of(pause, machine, FromMachine)
                ?? new ResolvedPause(pause.Name, pause.Default, DefaultSource)),
        ]);

    private static ResolvedPause? Of(AccessPause pause, AccessSetting? setting, string source) =>
        setting is null ? null
        : setting.Full ? new ResolvedPause(pause.Name, pause.Open, source)
        : setting.Pauses.TryGetValue(pause.Name, out var value) ? new ResolvedPause(pause.Name, value, source)
        : null;

    /// <summary>
    /// A repository's setup as it resolves from <paramref name="config"/>: its own, its workspace's (one in no workspace being in
    /// <c>default</c>) and this machine's, names matched in any case as every door matches them.
    /// </summary>
    public static ResolvedAccess Resolve(DriverConfig config, string repository, string? workspace) =>
        ResolveLevels(
            config.Access.TryGetValue(repository.Trim(), out var own) ? own : null,
            config.WorkspaceAccess.TryGetValue(RemoteTarget.Workspace(workspace), out var shared) ? shared : null,
            config.MachineAccess);

    /// <summary>
    /// The setup in one line, as a driven start's note says it: full access or today's defaults with where they came from, else each
    /// pause with its value and where it was set.
    /// </summary>
    public static string Said(ResolvedAccess access)
    {
        var values = string.Join(", ", access.Pauses.Select(pause => $"{pause.Pause} {pause.Value}"));
        var sources = access.Pauses.Select(pause => pause.Source).Distinct(StringComparer.Ordinal).ToList();
        if (access.Today && sources is [DefaultSource]) return $"Access setup: today's defaults — {values}.";
        if (access.Full && sources is [var one]) return $"Access setup: full access, set for {Whose(one)} — {values}.";
        return "Access setup: " + string.Join(", ", access.Pauses.Select(pause => $"{pause.Pause} {pause.Value} ({Whose(pause.Source)})")) + ".";
    }

    /// <summary>
    /// What a driven start's transcript notes of its setup, once its record is open: nothing where every pause is today's behaviour, so
    /// a start nobody changed reads as it always did, and <see cref="Said"/>'s one line otherwise. Its record keeps the setup either way.
    /// </summary>
    public static string? StartNote(ResolvedAccess access) => access.Today ? null : Said(access);

    /// <summary>Where a value was set, in a sentence.</summary>
    public static string Whose(string source) => source switch
    {
        FromRepository => "its repository",
        FromWorkspace => "its workspace",
        FromMachine => "this machine",
        _ => "the default",
    };

    /// <summary>
    /// One change made to the config, or its refusal thrown, nothing changed: at exactly one level, full access, the pauses it names
    /// (a pause set over full access keeping the rest as full access had them, a blank value taking its pause away, a level left
    /// naming none cleared), or clear. A key keeps the spelling first written.
    /// </summary>
    /// <exception cref="DriverException">The edit's refusal: no level or two, not one change, a pause or a value the setup does not have.</exception>
    public static DriverConfig Apply(DriverConfig config, AccessEdit edit)
    {
        var repository = edit.Repository?.Trim() is { Length: > 0 } r ? r : null;
        var workspace = edit.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        if (new[] { repository is not null, workspace is not null, edit.Machine }.Count(named => named) != 1) throw new DriverException(Scope);

        var set = edit.Set is { Count: > 0 } named ? named : null;
        if (new[] { edit.Full, set is not null, edit.Clear }.Count(change => change) != 1) throw new DriverException(OneAtATime);

        var map = repository is not null ? config.Access : workspace is not null ? config.WorkspaceAccess : null;
        var key = map is null ? null : map.Keys.FirstOrDefault(k => string.Equals(k, repository ?? workspace, StringComparison.OrdinalIgnoreCase)) ?? (repository ?? workspace);
        var own = map is null ? config.MachineAccess : map.TryGetValue(key!, out var standing) ? standing : null;

        AccessSetting? next = null;
        if (edit.Full) next = AccessSetting.FullAccess;
        else if (set is not null)
        {
            var pauses = own is { Full: true }
                ? Pauses.ToDictionary(pause => pause.Name, pause => pause.Open, StringComparer.Ordinal)
                : new Dictionary<string, string>(own?.Pauses ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            foreach (var (spelled, value) in set)
            {
                var pause = PauseNamed(spelled) ?? throw new DriverException(UnknownPause(spelled));
                if (string.IsNullOrWhiteSpace(value))
                {
                    pauses.Remove(pause.Name);
                    continue;
                }

                pauses[pause.Name] = ValueNamed(pause, value) ?? throw new DriverException(UnknownValue(pause, value));
            }

            next = pauses.Count == 0 ? null : new AccessSetting { Pauses = pauses };
        }

        if (map is null) return config with { MachineAccess = next };
        var changed = new Dictionary<string, AccessSetting>(map, StringComparer.OrdinalIgnoreCase);
        if (next is null) changed.Remove(key!);
        else changed[key!] = next;
        return repository is not null ? config with { Access = changed } : config with { WorkspaceAccess = changed };
    }

    /// <summary>
    /// An edit as the screen's route spells it (<c>SET_ACCESS</c>): <c>repository</c>, <c>workspace</c> or <c>machine: true</c>, and
    /// <c>access</c>: <c>"full"</c>, an object of pauses (a null value taking that pause away), or null or absent to clear. A value that
    /// is no word is kept by its JSON, to be refused naming it.
    /// </summary>
    /// <exception cref="DriverException"><c>access</c> is a word other than full, or of another shape.</exception>
    public static AccessEdit EditOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return new AccessEdit();
        var edit = new AccessEdit
        {
            Repository = Json.Text(root, "repository"),
            Workspace = Json.Text(root, "workspace"),
            Machine = root.TryGetProperty("machine", out var machine) && machine.ValueKind == JsonValueKind.True,
        };

        if (!root.TryGetProperty("access", out var access) || access.ValueKind == JsonValueKind.Null) return edit with { Clear = true };
        return access.ValueKind switch
        {
            JsonValueKind.String when string.Equals(access.GetString()?.Trim(), FullWord, StringComparison.OrdinalIgnoreCase) => edit with { Full = true },
            JsonValueKind.Object => edit with
            {
                Set =
                [
                    .. access.EnumerateObject().Select(pause => new KeyValuePair<string, string?>(
                        pause.Name,
                        pause.Value.ValueKind switch
                        {
                            JsonValueKind.Null => null,
                            JsonValueKind.String => pause.Value.GetString(),
                            // Not a word: refused as a value of that pause, naming what was sent.
                            _ => pause.Value.GetRawText(),
                        })),
                ],
            },
            _ => throw new DriverException(Shape),
        };
    }
}
