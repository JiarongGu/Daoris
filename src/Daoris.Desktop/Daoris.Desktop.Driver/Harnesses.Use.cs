using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// How a scope's list is used (TOOL6a; D130 §2, §3.1, §4.6, §14 as §16.6 amends them;
/// <c>docs/2026-10-02-account-use-design.md</c>): <c>rotationUse</c> for the machine and <c>workspaceRotationUse</c> for
/// one workspace, per agent, beside the lists — <i>use accounts</i> (<c>use</c>: <c>goal</c>, make the most of them, or
/// <c>order</c>, one by one in order), <i>keep for conversations</i> (<c>keep</c>) and <i>switch before the limit</i>
/// (<c>early</c>, <c>near</c>).
/// </summary>
/// <remarks>
/// <para><b>A twin</b> of the CLI's <c>rotation.ts</c> (<c>readUse</c>, <c>resolveScope</c>, <c>withUse</c>,
/// <c>scopeProblem</c>), which <c>daoris agent profile use</c> writes; <c>RotationUseTwinTests</c> and the CLI's
/// <c>rotation-use.test.ts</c> hold the same tables, row for row. The rules both keep:</para>
/// <para>Read: an entry is an object, kept whole but for <c>prefer</c> and <c>parallel</c>, which §16.6 retired and nothing
/// released wrote: skipped, and gone at the next write. Each other field is a value this build knows, or one it does not,
/// which reads as today's default and is said (<see cref="RotationScope.Unknown"/>); a field this build has no name for is
/// kept and said too, so a newer build's setting outlives this one's save.</para>
/// <para>Resolved as one scope (§2 rule 1): the workspace's, when it names a default or a list of its own for the agent,
/// else the machine's. A scope's settings are read only with its list; with none it is its default alone. A kept
/// account not in the list is none, and a default outside it leaves the list to win.</para>
/// <para>Written as chosen: a choice equal to today's default is still written, so a later default never overturns a
/// person's choice; absence alone means <see cref="RotationUse.Default"/>, the one place today's defaults live.</para>
/// <para><see cref="HarnessRoster.SelectAsync"/> reads them through <see cref="ResolveScope"/> (TOOL6b): <c>use</c> and
/// <c>keep</c> choose a start's account (<see cref="AccountRotation"/>), and <c>early</c> and <c>near</c> pass an account its
/// agent said is near (TOOL6c), where its door carries that word into <c>windows.json</c>.</para>
/// </remarks>
public sealed partial record HarnessSettings
{
    /// <summary>Agent → how this machine's list is used, as the file holds it. See <see cref="ResolveScope"/>.</summary>
    public IReadOnlyDictionary<string, UseEntry> Uses { get; init; } =
        new Dictionary<string, UseEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Workspace → agent → how that workspace's own list is used, as the file holds it.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, UseEntry>> WorkspaceUses { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, UseEntry>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The scope a start for <paramref name="agent"/> reads (D130 §2 rule 1): the workspace's, when it names a default or a
    /// list of its own for the agent, else the machine's — its own default, its list, where it begins, and how it is used.
    /// </summary>
    /// <remarks>What <see cref="HarnessRoster.SelectAsync"/> reads for every start but a pick (TOOL6b).</remarks>
    public RotationScope ResolveScope(string agent, string? workspace)
    {
        var circle = workspace?.Trim();
        if (!string.IsNullOrEmpty(circle))
        {
            var own = Workspaces.TryGetValue(circle, out var defaults) && defaults.TryGetValue(agent, out var named)
                && !string.IsNullOrWhiteSpace(named) ? named.Trim() : null;
            IReadOnlyList<string> list = WorkspaceRotation.TryGetValue(circle, out var orders) && orders.TryGetValue(agent, out var order)
                ? order : [];
            if (own is not null || list.Count > 0)
            {
                return ScopeOf(ChoiceFrom.Workspace, own, list, WorkspaceUses.TryGetValue(circle, out var uses) ? uses.GetValueOrDefault(agent) : null);
            }
        }

        var machine = Defaults.TryGetValue(agent, out var held) && !string.IsNullOrWhiteSpace(held) ? held.Trim() : null;
        return ScopeOf(ChoiceFrom.Machine, machine, Rotation.TryGetValue(agent, out var mine) ? mine : [], Uses.GetValueOrDefault(agent));
    }

    /// <summary>
    /// An agent's settings changed, the machine's or one workspace's; null clears them, and a scope or a workspace left
    /// with none is dropped. What this build does not know is kept, unless the change sets that field.
    /// </summary>
    /// <remarks>Each value is written as given: whether it is a setting at all is the door's question, asked first.</remarks>
    public HarnessSettings WithUse(string agent, UseChange? change, string? workspace = null)
    {
        UseEntry Edited(UseEntry? entry) => change is null ? UseEntry.Empty : (entry ?? UseEntry.Empty).Changed(change);

        if (string.IsNullOrWhiteSpace(workspace)) return this with { Uses = Placed(Uses, agent, Edited(Uses.GetValueOrDefault(agent))) };

        var circle = workspace.Trim();
        var circles = new Dictionary<string, IReadOnlyDictionary<string, UseEntry>>(WorkspaceUses, StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, UseEntry> uses = circles.GetValueOrDefault(circle) ?? new Dictionary<string, UseEntry>();
        var placed = Placed(uses, agent, Edited(uses.GetValueOrDefault(agent)));
        if (placed.Count == 0) circles.Remove(circle);
        else circles[circle] = placed;
        return this with { WorkspaceUses = circles };
    }

    private static RotationScope ScopeOf(ChoiceFrom from, string? own, IReadOnlyList<string> list, UseEntry? entry)
    {
        if (list.Count == 0) return new RotationScope(from, own, [], own, RotationUse.Default, []);

        var (use, unknown) = RotationUse.Read(entry);
        if (use.Keep is { } keep && !list.Contains(keep, StringComparer.Ordinal)) use = use with { Keep = null };
        var begins = own is not null && list.Contains(own, StringComparer.Ordinal) ? own : list[0];
        return new RotationScope(from, own, list, begins, use, unknown);
    }

    private static Dictionary<string, UseEntry> Placed(IReadOnlyDictionary<string, UseEntry> uses, string agent, UseEntry entry)
    {
        var next = new Dictionary<string, UseEntry>(uses, StringComparer.OrdinalIgnoreCase);
        if (entry.IsEmpty) next.Remove(agent);
        else next[agent] = entry;
        return next;
    }

    /// <summary>One agent → settings map: each entry an object kept whole; anything else, or an entry naming nothing, is none.</summary>
    private static IReadOnlyDictionary<string, UseEntry> ReadUses(JsonElement parent, string? property)
    {
        var uses = new Dictionary<string, UseEntry>(StringComparer.OrdinalIgnoreCase);
        var element = parent;
        if (property is not null && !parent.TryGetProperty(property, out element)) return uses;
        if (element.ValueKind != JsonValueKind.Object) return uses;

        foreach (var entry in element.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object) continue;
            List<KeyValuePair<string, JsonElement>> fields = [.. entry.Value.EnumerateObject()
                .Where(field => !RotationUse.Retired.Contains(field.Name, StringComparer.Ordinal))
                .Select(field => KeyValuePair.Create(field.Name, field.Value.Clone()))];
            if (fields.Count > 0) uses[entry.Name] = new UseEntry(fields);
        }

        return uses;
    }

    /// <summary>One workspace → agent → settings map, each read as <see cref="ReadUses"/> reads one; a workspace naming none is none.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, UseEntry>> ReadUseCircles(JsonElement root, string property)
    {
        var circles = new Dictionary<string, IReadOnlyDictionary<string, UseEntry>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Object) return circles;

        foreach (var circle in element.EnumerateObject())
        {
            var uses = ReadUses(circle.Value, null);
            if (uses.Count > 0) circles[circle.Name] = uses;
        }

        return circles;
    }

    /// <summary>A settings section, written only where an entry is set: agents in order of name.</summary>
    private static void WriteUses(Utf8JsonWriter writer, string property, IReadOnlyDictionary<string, UseEntry> uses)
    {
        if (!uses.Any(entry => !entry.Value.IsEmpty)) return;
        writer.WriteStartObject(property);
        WriteEntries(writer, uses);
        writer.WriteEndObject();
    }

    /// <summary>The workspaces' settings section, written only where one is set: workspaces in order of name.</summary>
    private static void WriteUseCircles(
        Utf8JsonWriter writer, string property, IReadOnlyDictionary<string, IReadOnlyDictionary<string, UseEntry>> circles)
    {
        if (!circles.Any(circle => circle.Value.Any(entry => !entry.Value.IsEmpty))) return;
        writer.WriteStartObject(property);
        foreach (var (workspace, uses) in circles.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (!uses.Any(entry => !entry.Value.IsEmpty)) continue;
            writer.WriteStartObject(workspace);
            WriteEntries(writer, uses);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteEntries(Utf8JsonWriter writer, IReadOnlyDictionary<string, UseEntry> uses)
    {
        foreach (var (agent, entry) in uses.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (entry.IsEmpty) continue;
            writer.WriteStartObject(agent);
            entry.WriteTo(writer);
            writer.WriteEndObject();
        }
    }
}

/// <summary>How one scope's list is used, each setting's value as a start reads it (D130 §16.6).</summary>
/// <param name="Use"><i>Use accounts</i>: one of <see cref="Modes"/> — <c>goal</c>, §16.3's walk (<see cref="AccountRotation.Order"/>), or
/// <c>order</c>, D125's walk.</param>
/// <param name="Keep">The account kept for conversations, or none.</param>
/// <param name="Early"><i>Switch before the limit</i>.</param>
/// <param name="Near">Where an account is near its limit, as a percent of a window, where its agent gives only a number.</param>
public sealed record RotationUse(string Use, string? Keep, bool Early, int Near)
{
    /// <summary>Today's defaults (D130 §16.6), and the one place they live: what absence means for each setting.</summary>
    public static readonly RotationUse Default = new("goal", Keep: null, Early: true, Near: 90);

    /// <summary><i>Use accounts</i>' choices (§16.6): make the most of them, one by one in order. A new choice is a row here.</summary>
    public static readonly IReadOnlyList<string> Modes = ["goal", "order"];

    /// <summary><i>Near</i>'s range (§14): a whole percent from the one to the other.</summary>
    public const int NearLowest = 50;

    /// <inheritdoc cref="NearLowest"/>
    public const int NearHighest = 99;

    /// <summary>The settings this build knows, in the order they are written.</summary>
    public static readonly IReadOnlyList<string> Fields = ["use", "keep", "early", "near"];

    /// <summary>The settings §16.6 retired, which nothing released wrote: skipped by the reader, so gone at the next write.</summary>
    public static readonly IReadOnlyList<string> Retired = ["prefer", "parallel"];

    /// <summary>
    /// An entry's settings: today's default where it says nothing or holds what this build does not know, and the names
    /// of what it does not know — the known settings first, in their order, then the ones it has no name for.
    /// </summary>
    public static (RotationUse Use, IReadOnlyList<string> Unknown) Read(UseEntry? entry)
    {
        var use = Default;
        var unknown = new List<string>();
        if (entry is null) return (use, unknown);

        foreach (var field in Fields)
        {
            if (entry[field] is not { } raw) continue;
            switch (field)
            {
                case "use" when ModeOf(raw) is { } mode: use = use with { Use = mode }; break;
                case "keep" when KeepOf(raw) is { } keep: use = use with { Keep = keep }; break;
                case "early" when FlagOf(raw) is { } early: use = use with { Early = early }; break;
                case "near" when NearOf(raw) is { } near: use = use with { Near = near }; break;
                default: unknown.Add(field); break;
            }
        }

        unknown.AddRange(entry.Fields.Select(field => field.Key).Where(name => !Fields.Contains(name, StringComparer.Ordinal)));
        return (use, unknown);
    }

    internal static string? ModeOf(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.String && Modes.Contains(raw.GetString()!, StringComparer.Ordinal) ? raw.GetString() : null;

    internal static bool? FlagOf(JsonElement raw) => raw.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    internal static string? KeepOf(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.String && raw.GetString()!.Trim() is { Length: > 0 } keep ? keep : null;

    /// <summary>A whole percent in range, however the number is spelled (<c>85.0</c> is 85).</summary>
    internal static int? NearOf(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.Number && raw.TryGetDouble(out var near) && near == Math.Floor(near)
            && near >= NearLowest && near <= NearHighest ? (int)near : null;
}

/// <summary>
/// One scope's settings as the file holds them (TOOL6a): by name, in the order read, each a value this build knows or not.
/// An editor keeps what it has no field for, so a setting a newer build writes outlives this one's save.
/// </summary>
public sealed record UseEntry(IReadOnlyList<KeyValuePair<string, JsonElement>> Fields)
{
    public static readonly UseEntry Empty = new([]);

    public bool IsEmpty => Fields.Count == 0;

    /// <summary>The value written for <paramref name="name"/>, or null where none is.</summary>
    public JsonElement? this[string name]
    {
        get
        {
            foreach (var (key, value) in Fields)
            {
                if (key == name) return value;
            }

            return null;
        }
    }

    /// <summary>The entry with <paramref name="name"/> set where it stood, or added last; a null value takes it out.</summary>
    public UseEntry With(string name, JsonElement? value)
    {
        var fields = new List<KeyValuePair<string, JsonElement>>();
        var placed = false;
        foreach (var field in Fields)
        {
            if (field.Key != name)
            {
                fields.Add(field);
            }
            else if (value is { } set && !placed)
            {
                fields.Add(KeyValuePair.Create(name, set));
                placed = true;
            }
        }

        if (value is { } added && !placed) fields.Add(KeyValuePair.Create(name, added));
        return new UseEntry(fields);
    }

    internal UseEntry Changed(UseChange change)
    {
        var entry = this;
        if (change.Use is { } mode) entry = entry.With("use", Element(writer => writer.WriteStringValue(mode)));
        if (change.NoKeep) entry = entry.With("keep", null);
        else if (change.Keep?.Trim() is { Length: > 0 } keep) entry = entry.With("keep", Element(writer => writer.WriteStringValue(keep)));
        if (change.Early is { } early) entry = entry.With("early", Element(writer => writer.WriteBooleanValue(early)));
        if (change.Near is { } near) entry = entry.With("near", Element(writer => writer.WriteNumberValue(near)));
        return entry;
    }

    /// <summary>
    /// The entry as written: the settings this build knows in their order, a value it knows normalised, then the rest as
    /// read — as the CLI's <c>writtenUse</c> writes it.
    /// </summary>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        foreach (var field in RotationUse.Fields)
        {
            if (this[field] is not { } raw) continue;
            writer.WritePropertyName(field);
            switch (field)
            {
                case "use" when RotationUse.ModeOf(raw) is { } mode: writer.WriteStringValue(mode); break;
                case "early" when RotationUse.FlagOf(raw) is { } flag: writer.WriteBooleanValue(flag); break;
                case "keep" when RotationUse.KeepOf(raw) is { } keep: writer.WriteStringValue(keep); break;
                case "near" when RotationUse.NearOf(raw) is { } near: writer.WriteNumberValue(near); break;
                default: raw.WriteTo(writer); break;
            }
        }

        foreach (var (name, value) in Fields.Where(field => !RotationUse.Fields.Contains(field.Key, StringComparer.Ordinal)
            && !RotationUse.Retired.Contains(field.Key, StringComparer.Ordinal)))
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }
    }

    private static JsonElement Element(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) write(writer);
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
}

/// <summary>
/// A change to one scope's settings: a field null is left as it was; <paramref name="NoKeep"/> keeps none. A door
/// validates each value first; <see cref="HarnessSettings.WithUse"/> writes it as given.
/// </summary>
public sealed record UseChange(string? Use = null, string? Keep = null, bool NoKeep = false, bool? Early = null, int? Near = null);

/// <summary>The scope a start reads (D130 §2 rule 1).</summary>
/// <param name="From">Whose scope: <see cref="ChoiceFrom.Workspace"/> or <see cref="ChoiceFrom.Machine"/>.</param>
/// <param name="Default">The scope's own default, trimmed, or none.</param>
/// <param name="List">The scope's list, empty for none.</param>
/// <param name="Begins">Its default where its list holds it, else its list's first; with no list, its default alone.</param>
/// <param name="Use">How it is used; today's defaults with no list.</param>
/// <param name="Unknown">The settings it holds that this build does not know, by name.</param>
public sealed record RotationScope(
    ChoiceFrom From, string? Default, IReadOnlyList<string> List, string? Begins, RotationUse Use, IReadOnlyList<string> Unknown);

/// <summary>What binds a scope (D130 §3.1, §4.6).</summary>
public enum ScopeProblemKind
{
    /// <summary>Its default is not in its list.</summary>
    Default,

    /// <summary>Its kept account is not in its list.</summary>
    Keep,

    /// <summary>Its kept account is the one account its list holds, so driven work would have none.</summary>
    Alone,
}

/// <summary>
/// Why a scope cannot be written (D130 §3.1, §4.6): its default or its kept account outside its list, or a kept account that
/// leaves driven work none. Both doors refuse it; the CLI's <c>scopeProblem</c> is the twin. No list is refused for its
/// length: a list of one with no kept account, and a list of many, are both fine.
/// </summary>
public sealed record ScopeProblem(ScopeProblemKind Kind, string Account)
{
    /// <summary>
    /// The first problem with a scope's default, list and kept account, or null. Names compare exactly, as a door compares
    /// a name with its directory; a scope with no list is its default alone, so only a kept account is refused there.
    /// </summary>
    public static ScopeProblem? Of(string? scopeDefault, IReadOnlyList<string> list, string? keep)
    {
        var own = scopeDefault?.Trim();
        if (!string.IsNullOrEmpty(own) && list.Count > 0 && !list.Contains(own, StringComparer.Ordinal))
        {
            return new ScopeProblem(ScopeProblemKind.Default, own);
        }

        var kept = keep?.Trim();
        if (string.IsNullOrEmpty(kept)) return null;
        if (!list.Contains(kept, StringComparer.Ordinal)) return new ScopeProblem(ScopeProblemKind.Keep, kept);
        return list.Any(name => name != kept) ? null : new ScopeProblem(ScopeProblemKind.Alone, kept);
    }
}
