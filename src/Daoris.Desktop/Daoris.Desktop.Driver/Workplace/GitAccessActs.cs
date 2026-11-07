using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One change to git access, as every door makes it: at a <see cref="Repository"/>, a <see cref="Workspace"/> or this
/// <see cref="Machine"/>; the classes it names (a null or blank value taking that class back to the level above), one value for read
/// and write together, or clear.
/// </summary>
public sealed record GitAccessEdit
{
    public string? Repository { get; init; }
    public string? Workspace { get; init; }
    public bool Machine { get; init; }
    public IReadOnlyList<KeyValuePair<string, string?>>? Set { get; init; }

    /// <summary>One value for Read and Write at this level (<i>use one value for both</i>).</summary>
    public string? OneValue { get; init; }

    public bool Clear { get; init; }
}

/// <summary>
/// The edits of git access (10 and 12): one typed operation called by
/// <c>SET_GIT_ACCESS</c> and by <c>daoris git access</c> alone, writing only the key paths it changes in <c>driver.json</c>, so
/// every other key, a key this build does not know included, is kept as written.
/// </summary>
public static class GitAccessActs
{
    /// <summary>The refusal for Dangerous asked: the doors judge each dangerous update themselves, and no person is waited on.</summary>
    public const string DangerousAskLine = "git: Dangerous is `allow` or `deny`: the doors judge each dangerous update themselves and wait on no one (dangerous-no-ask)";

    private const string Scope = "git access is set for a repository, a workspace or this machine — name exactly one.";
    private const string OneChange = "one change at a time: the classes it names with their values, one value for both, or clear.";

    /// <summary>The edit as the screen's route spells it: <c>repository</c>, <c>workspace</c> or <c>machine: true</c>, and <c>access</c> (an object of classes, a null value taking one away), <c>oneValue</c>, or neither to clear.</summary>
    public static GitAccessEdit EditOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return new GitAccessEdit();
        var edit = new GitAccessEdit
        {
            Repository = Json.Text(root, "repository"),
            Workspace = Json.Text(root, "workspace"),
            Machine = root.TryGetProperty("machine", out var machine) && machine.ValueKind == JsonValueKind.True,
            OneValue = Json.Text(root, "oneValue"),
        };

        if (edit.OneValue is not null) return edit;
        if (!root.TryGetProperty("access", out var access) || access.ValueKind != JsonValueKind.Object) return edit with { Clear = true };
        return edit with
        {
            Set =
            [
                .. access.EnumerateObject().Select(each => new KeyValuePair<string, string?>(
                    each.Name, each.Value.ValueKind == JsonValueKind.String ? each.Value.GetString() : each.Value.ValueKind == JsonValueKind.Null ? null : each.Value.GetRawText())),
            ],
        };
    }

    /// <summary>The level an edit names: its key, its name, or null for the machine's.</summary>
    private static (string Key, string? Name) LevelOf(GitAccessEdit edit)
    {
        var repository = edit.Repository?.Trim() is { Length: > 0 } r ? r : null;
        var workspace = edit.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        if (new[] { repository is not null, workspace is not null, edit.Machine }.Count(named => named) != 1) throw new DriverException(Scope);
        return repository is not null ? (GitAccessKeys.RepositoryKey, repository)
            : workspace is not null ? (GitAccessKeys.WorkspaceKey, workspace)
            : (GitAccessKeys.MachineKey, null);
    }

    /// <summary>The edit's changes, judged: the class and its value, or null to take it away.</summary>
    /// <exception cref="DriverException">An edit that is not one change, a class or a value there is not, or Dangerous asked.</exception>
    public static IReadOnlyList<KeyValuePair<string, string?>> Changes(GitAccessEdit edit)
    {
        var changes = new[] { edit.Set is { Count: > 0 }, edit.OneValue is not null, edit.Clear }.Count(each => each);
        if (changes != 1) throw new DriverException(OneChange);
        if (edit.Clear) return [.. GitAccessKeys.Classes.Select(each => new KeyValuePair<string, string?>(each, null))];
        if (edit.OneValue is not null)
        {
            var one = GitAccessKeys.ValueNamed(edit.OneValue) ?? throw new DriverException($"git access is `allow`, `ask` or `deny`, not `{edit.OneValue.Trim()}`.");
            return [new(GitAccessKeys.Read, one), new(GitAccessKeys.Write, one)];
        }

        var set = new List<KeyValuePair<string, string?>>();
        foreach (var (spelled, value) in edit.Set!)
        {
            var name = GitAccessKeys.ClassNamed(spelled) ?? throw new DriverException($"`{spelled.Trim()}` is not a class of git access — `read`, `write` or `dangerous`.");
            var named = string.IsNullOrWhiteSpace(value) ? null : GitAccessKeys.ValueNamed(value) ?? throw new DriverException($"git access is `allow`, `ask` or `deny`, not `{value.Trim()}`.");
            if (name == GitAccessKeys.Dangerous && named == GitAccessKeys.Ask) throw new DriverException(DangerousAskLine);
            set.Add(new(name, named));
        }

        return set;
    }

    /// <summary>The levels as they stand once <paramref name="edit"/> is made, nothing written.</summary>
    public static GitAccessLevels After(GitAccessLevels before, GitAccessEdit edit)
    {
        var (key, name) = LevelOf(edit);
        var changes = Changes(edit);
        IReadOnlyDictionary<string, string> Changed(IReadOnlyDictionary<string, string>? own)
        {
            var next = new Dictionary<string, string>(own ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            foreach (var (cls, value) in changes)
            {
                if (value is null) next.Remove(cls);
                else next[cls] = value;
            }

            return next;
        }

        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Mapped(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> map)
        {
            var copy = new Dictionary<string, IReadOnlyDictionary<string, string>>(map, StringComparer.OrdinalIgnoreCase);
            var level = Changed(copy.GetValueOrDefault(name!));
            if (level.Count == 0) copy.Remove(name!);
            else copy[name!] = level;
            return copy;
        }

        return key switch
        {
            GitAccessKeys.RepositoryKey => before with { Repository = Mapped(before.Repository) },
            GitAccessKeys.WorkspaceKey => before with { Workspace = Mapped(before.Workspace) },
            _ => before with { Machine = Changed(before.Machine) },
        };
    }

    /// <summary>
    /// The classes the edit loosens in the view of the level it changes (§8's widenings: a class loosened, or a level cleared where the
    /// level above is looser; <i>one value for both</i> where it loosens). Narrowing and no change are none.
    /// </summary>
    public static IReadOnlyList<string> Widened(GitAccessLevels before, GitAccessEdit edit)
    {
        var after = After(before, edit);
        var repository = edit.Repository?.Trim() is { Length: > 0 } r ? r : null;
        var workspace = edit.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        var was = GitAccessKeys.Resolve(before, repository, workspace);
        var will = GitAccessKeys.Resolve(after, repository, workspace);
        return [.. GitAccessKeys.Classes.Where(each => GitAccessKeys.Looseness(will.Of(each).Value) < GitAccessKeys.Looseness(was.Of(each).Value))];
    }

    /// <summary>Make the edit in the home's <c>driver.json</c>, only the key paths it changes; the levels after.</summary>
    /// <exception cref="DriverException">The edit's refusal, or a file that does not read, nothing written.</exception>
    public static GitAccessLevels Apply(string home, GitAccessEdit edit)
    {
        var (key, name) = LevelOf(edit);
        var changes = Changes(edit);
        DriverConfigFile.Change(home, root =>
        {
            var map = key == GitAccessKeys.MachineKey ? null : root[key] as JsonObject;
            JsonObject? level = key == GitAccessKeys.MachineKey
                ? root[key] as JsonObject
                : map?.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value as JsonObject;
            var spelled = key == GitAccessKeys.MachineKey ? key : map?.Select(pair => pair.Key).FirstOrDefault(each => string.Equals(each, name, StringComparison.OrdinalIgnoreCase)) ?? name!;

            level ??= new JsonObject();
            foreach (var (cls, value) in changes)
            {
                if (value is null) level.Remove(cls);
                else level[cls] = value;
            }

            if (key == GitAccessKeys.MachineKey)
            {
                if (level.Count == 0) root.Remove(key);
                else root[key] = level;
                return;
            }

            map ??= new JsonObject();
            map.Remove(spelled);
            if (level.Count > 0) map[spelled] = level;
            if (map.Count == 0) root.Remove(key);
            else root[key] = map;
        });
        return GitAccessKeys.Load(home);
    }
}
