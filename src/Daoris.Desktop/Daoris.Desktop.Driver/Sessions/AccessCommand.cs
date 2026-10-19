namespace Daoris.Driver;

/// <summary>What <c>daoris-driver access</c> was asked (AUTONOMY1a): a level, and the change to it, or none to say it.</summary>
public sealed record AccessAsk
{
    /// <summary>The repository named, or null.</summary>
    public string? Repository { get; init; }

    /// <summary>The workspace <c>--workspace</c> named, or null; neither is this machine.</summary>
    public string? Workspace { get; init; }

    /// <summary>The change, at the level named, or null to say the setup there.</summary>
    public AccessEdit? Edit { get; init; }
}

/// <summary>Where a repository sits, as the host read it from the service's registry: its workspace, or that it could not be read.</summary>
/// <param name="Workspace">Its workspace, null being <c>default</c>.</param>
/// <param name="Known">Whether the registry answered; when not, a workspace's setup is not counted.</param>
public sealed record AccessPlace(string? Workspace, bool Known);

/// <summary>
/// <c>daoris-driver access</c> (AUTONOMY1a, D184 point 1, D50): a repository's session access setup from a terminal, its workspace's
/// with <c>--workspace</c>, or this machine's with neither: said pause by pause with where each came from, or set to full access, to the
/// pauses named, or cleared. It reads and writes the driver's file alone, through <see cref="AccessRules"/>, the door the screen's
/// <c>SET_ACCESS</c> goes through, so the two refuse alike.
/// </summary>
/// <remarks>
/// <para>Exit codes are the family's: 0 said or set · 1 refused, nothing written · 2 could not, the usage among them.</para>
/// <para>What each pause changes in a session is not this verb's: the setting is kept here and on each driven session's record.</para>
/// </remarks>
public static class AccessCommand
{
    public const string Usage =
        """
        usage: daoris-driver access [<repository> | --workspace <name>] [full | <pause>=<value>… | --clear]
               pauses: permissions=asking|auto|unrestricted  routing=person|intake  landing=person|rule
                       goAheads=named|never  questions=claude-code;  <pause>= takes one back to the level above
        """;

    private const string Change = "`access` takes `full`, `--clear`, or `<pause>=<value>` for each pause it sets — one change at a time.";

    /// <summary>What the words after <c>access</c> ask, or null with what is wrong with them.</summary>
    public static AccessAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        string? repository = null, workspace = null;
        var at = 0;
        if (args.Count > 0 && args[0] == "--workspace")
        {
            if (args.Count < 2 || args[1].Trim().Length == 0 || args[1].StartsWith("--", StringComparison.Ordinal))
            {
                problem = "`--workspace` takes a workspace's name.";
                return null;
            }

            workspace = args[1].Trim();
            at = 2;
        }
        else if (args.Count > 0 && !IsChange(args[0]))
        {
            if (args[0].StartsWith("--", StringComparison.Ordinal))
            {
                problem = $"`{args[0]}` is not a repository's name; `access` takes `--workspace <name>`, or a repository's name, or neither for this machine.";
                return null;
            }

            repository = args[0].Trim();
            at = 1;
        }

        var level = new AccessEdit { Repository = repository, Workspace = workspace, Machine = repository is null && workspace is null };
        var words = args.Skip(at).ToList();
        AccessEdit? edit = words switch
        {
            [] => null,
            [var one] when string.Equals(one, AccessRules.FullWord, StringComparison.OrdinalIgnoreCase) => level with { Full = true },
            ["--clear"] => level with { Clear = true },
            _ when words.All(word => word.Contains('=', StringComparison.Ordinal)) => level with
            {
                Set = [.. words.Select(word => new KeyValuePair<string, string?>(word[..word.IndexOf('=')], word[(word.IndexOf('=') + 1)..]))],
            },
            _ => level with { Clear = false, Full = false, Set = [] },
        };

        if (edit is { Full: false, Clear: false, Set.Count: 0 })
        {
            problem = Change;
            return null;
        }

        return new AccessAsk { Repository = repository, Workspace = workspace, Edit = edit };
    }

    private static bool IsChange(string word) =>
        string.Equals(word, AccessRules.FullWord, StringComparison.OrdinalIgnoreCase) || word == "--clear" || word.Contains('=', StringComparison.Ordinal);

    /// <summary>
    /// Say the setup at the level asked, each pause with its value and where it came from, or make the change, written to the driver's
    /// file at <paramref name="configPath"/>. A repository's is resolved with the workspace <paramref name="place"/> names; where the
    /// registry was not read, a workspace's setup is left out, and said so.
    /// </summary>
    /// <exception cref="DriverConfigUnreadableException">The file does not read, so nothing was written.</exception>
    public static int Run(AccessAsk ask, string configPath, AccessPlace? place, TextWriter output)
    {
        var config = DriverConfig.Load(configPath);
        var level = Level(ask);
        if (ask.Edit is { } edit)
        {
            DriverConfig changed;
            try
            {
                changed = config.WithAccess(edit);
            }
            catch (DriverException refused)
            {
                output.WriteLine($"access: {refused.Message}");
                return 1;
            }

            changed.Save(configPath);
            output.WriteLine($"access: {Said(ask, Own(changed, ask), level)}");
            return 0;
        }

        output.WriteLine($"access: {level}'s own: {Listed(Own(config, ask))}.");
        var workspace = ask.Workspace ?? (place is { Known: true } ? RemoteTarget.Workspace(place.Workspace) : null);
        var resolved = AccessRules.ResolveLevels(
            ask.Repository is { } repository && config.Access.TryGetValue(repository, out var own) ? own : null,
            workspace is not null && config.WorkspaceAccess.TryGetValue(workspace, out var shared) ? shared : null,
            config.MachineAccess);
        foreach (var pause in resolved.Pauses)
        {
            var whose = pause.Source == AccessRules.FromWorkspace ? $"its workspace, `{workspace}`" : AccessRules.Whose(pause.Source);
            output.WriteLine($"  {pause.Pause,-11}  {pause.Value,-12}  {whose}");
        }

        if (ask.Repository is not null && place is not { Known: true })
        {
            output.WriteLine("  its workspace was not read (the service did not answer), so a workspace's setup is not counted here.");
        }

        if (ask.Repository is null && ask.Workspace is null)
        {
            var nearer = config.WorkspaceAccess.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"workspace `{pair.Key}`: {Listed(pair.Value)}")
                .Concat(config.Access.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"`{pair.Key}`: {Listed(pair.Value)}"))
                .ToList();
            if (nearer.Count > 0) output.WriteLine($"  set nearer: {string.Join(" · ", nearer)}");
        }

        return 0;
    }

    /// <summary>The level's own setting after the change, or before it for a say.</summary>
    private static AccessSetting? Own(DriverConfig config, AccessAsk ask) =>
        ask.Repository is { } repository ? config.Access.GetValueOrDefault(repository)
        : ask.Workspace is { } workspace ? config.WorkspaceAccess.GetValueOrDefault(workspace)
        : config.MachineAccess;

    private static string Level(AccessAsk ask) =>
        ask.Repository is { } repository ? $"`{repository}`" : ask.Workspace is { } workspace ? $"workspace `{workspace}`" : "this machine";

    /// <summary>A level's own setting as the terminal says it.</summary>
    private static string Listed(AccessSetting? setting) =>
        setting is null ? "nothing of its own"
        : setting.Full ? "full access"
        : string.Join(", ", AccessRules.Pauses.Where(pause => setting.Pauses.ContainsKey(pause.Name)).Select(pause => $"{pause.Name} {setting.Pauses[pause.Name]}"));

    /// <summary>What a change did, in a sentence, and where a pause the level does not name comes from.</summary>
    private static string Said(AccessAsk ask, AccessSetting? now, string level)
    {
        var above = ask.Repository is not null ? "its workspace, this machine or the default"
            : ask.Workspace is not null ? "this machine or the default"
            : "the default";
        return now switch
        {
            null => $"{level} sets nothing of its own now; each pause comes from {above}.",
            { Full: true } => $"{level} now runs its sessions with full access: every pause off. A level nearer a repository may still name a "
                + "pause; your own deny rules still hold, and a go-ahead an ask itself demands is still asked.",
            _ => $"{level}'s own access setup is now {Listed(now)}. A pause it does not name comes from {above}.",
        };
    }
}
