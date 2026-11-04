namespace Daoris.Driver;

/// <summary>
/// The edits <see cref="DriverConfig"/> makes to its keyed maps. A name's spelling first written is kept when it comes again in another
/// case: one entry, never two.
/// </summary>
internal static class DriverConfigEdits
{
    public static string Quest(string questId) => questId.Trim().TrimStart('#');

    public static string PauseId(string id) => id.Trim().TrimStart('#').Trim();

    private static string KeyOf<T>(Dictionary<string, T> map, string named) =>
        map.Keys.FirstOrDefault(k => string.Equals(k, named, StringComparison.OrdinalIgnoreCase)) ?? named;

    public static DriverConfig Released(DriverConfig config, string questId, string session)
    {
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(session))
        {
            throw new DriverException("a release names the quest and the session whose stop it releases.");
        }

        var quest = Quest(questId);
        var next = new Dictionary<string, string>(config.Released, StringComparer.OrdinalIgnoreCase);
        next[KeyOf(next, quest)] = session.Trim();
        return config with { Released = next };
    }

    public static DriverConfig TransientReleased(DriverConfig config, string questId, string session, DateTimeOffset ended)
    {
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(session))
        {
            throw new DriverException("a transient release names the quest and the session whose end it releases.");
        }

        var quest = Quest(questId);
        var next = new Dictionary<string, TransientRelease>(config.TransientReleased, StringComparer.OrdinalIgnoreCase);
        next[KeyOf(next, quest)] = new TransientRelease(session.Trim(), ended.ToUniversalTime());
        return config with { TransientReleased = next };
    }

    public static DriverConfig RoleReleased(DriverConfig config, string questId, RoleRelease release)
    {
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(release.Session) || string.IsNullOrWhiteSpace(release.Act))
        {
            throw new DriverException("a role's release names the quest, the session whose stop it releases and the act that made it.");
        }

        var quest = Quest(questId);
        var next = new Dictionary<string, RoleRelease>(config.RoleReleases, StringComparer.OrdinalIgnoreCase);
        next[KeyOf(next, quest)] = release with { Session = release.Session.Trim() };
        return config with { RoleReleases = next };
    }

    public static DriverConfig WithoutRoleRelease(DriverConfig config, string questId)
    {
        var quest = Quest(questId);
        if (!config.RoleReleases.Keys.Any(key => string.Equals(key, quest, StringComparison.OrdinalIgnoreCase))) return config;
        return config with
        {
            RoleReleases = config.RoleReleases.Where(pair => !string.Equals(pair.Key, quest, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
        };
    }

    public static DriverConfig Admitted(DriverConfig config, string questId, string repository)
    {
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(repository))
        {
            throw new DriverException("an admission names the set-up quest and the repository it starts in.");
        }

        var quest = Quest(questId);
        var next = new Dictionary<string, string>(config.Admitted, StringComparer.OrdinalIgnoreCase);
        next[KeyOf(next, quest)] = repository.Trim();
        return config with { Admitted = next };
    }

    public static DriverConfig WithoutQuest(DriverConfig config, string questId)
    {
        var quest = Quest(questId);
        bool Same(string key) => string.Equals(key, quest, StringComparison.OrdinalIgnoreCase);
        if (!config.Released.Keys.Any(Same) && !config.Forgiven.Keys.Any(Same) && !config.TransientReleased.Keys.Any(Same)
            && !config.RoleReleases.Keys.Any(Same) && !config.Admitted.Keys.Any(Same))
        {
            return config;
        }

        return config with
        {
            Released = config.Released.Where(pair => !Same(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Forgiven = config.Forgiven.Where(pair => !Same(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            TransientReleased = config.TransientReleased.Where(pair => !Same(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            RoleReleases = config.RoleReleases.Where(pair => !Same(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Admitted = config.Admitted.Where(pair => !Same(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
        };
    }

    public static IReadOnlyDictionary<string, WorkPause> WithPause(
        IReadOnlyDictionary<string, WorkPause> map, string id, WorkPause? pause)
    {
        var named = PauseId(id ?? "");
        if (named.Length == 0) throw new DriverException("a pause names the ask or the quest it pauses.");

        var next = new Dictionary<string, WorkPause>(map, StringComparer.OrdinalIgnoreCase);
        var key = KeyOf(next, named);
        if (pause is null) next.Remove(key);
        else next[key] = pause;
        return next;
    }

    public static DriverConfig Standing(DriverConfig config, string repository, string? says, DateTimeOffset at)
    {
        var named = repository?.Trim() ?? "";
        if (named.Length == 0) throw new DriverException("a standing answer names the repository it holds for.");
        var words = says?.Trim();
        if (says is not null && (words!.Length == 0 || words.Length > DriverConfig.StandingLimit)) throw new DriverException(DriverConfig.StandingRefusal);

        var next = new Dictionary<string, StandingAnswer>(config.Standing, StringComparer.OrdinalIgnoreCase);
        var key = KeyOf(next, named);
        if (words is null) next.Remove(key);
        else next[key] = new StandingAnswer(words, ToTheSecond(at));
        return config with { Standing = next };
    }

    /// <summary>A moment as the file keeps it: in UTC, to the second.</summary>
    private static DateTimeOffset ToTheSecond(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, TimeSpan.Zero);
    }

    public static IReadOnlyDictionary<string, string> WithLanguageIn(
        IReadOnlyDictionary<string, string> map, string name, string? code, string what)
    {
        var named = name?.Trim() ?? "";
        if (named.Length == 0) throw new DriverException($"a session language is set for a {what} — name it.");
        var language = code is null ? null : SessionLanguages.Code(code) ?? throw new DriverException(SessionLanguages.Refusal(code));

        var next = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        var key = KeyOf(next, named);
        if (language is null) next.Remove(key);
        else next[key] = language;
        return next;
    }

    public static IReadOnlyDictionary<string, LandingIntegrate> Policy(IReadOnlyDictionary<string, LandingIntegrate> map, string key, LandingIntegrate? policy)
    {
        var next = new Dictionary<string, LandingIntegrate>(map, StringComparer.OrdinalIgnoreCase);
        if (policy is null) next.Remove(key.Trim());
        else
        {
            if (LandingKey.PolicyProblem(policy, wholeSet: true) is { } problem) throw new DriverException(problem);
            next[key.Trim()] = policy;
        }

        return next;
    }

    public static DriverConfig IntegrationGates(DriverConfig config, string repository, string? blob)
    {
        var named = repository.Trim();
        if (named.Length == 0) throw new DriverException("an integration's acceptance names its repository.");
        if (blob is not null && !IntegrationGatesKey.IsAcceptance(blob)) throw new DriverException(IntegrationGatesKey.Problem);
        var next = new Dictionary<string, string>(config.IntegrationGates, StringComparer.OrdinalIgnoreCase);
        if (blob is null) next.Remove(named);
        else next[named] = blob;
        return config with { IntegrationGates = next };
    }

    public static DriverConfig WriteAcross(DriverConfig config, string repository, string to, bool allow)
    {
        if (AcrossDeclarations.Problem(repository, to) is { } problem) throw new DriverException(problem);

        bool Same(string name) => string.Equals(name, to.Trim(), StringComparison.OrdinalIgnoreCase);
        var next = new Dictionary<string, IReadOnlyList<string>>(config.WriteAcross, StringComparer.OrdinalIgnoreCase);
        var held = next.GetValueOrDefault(repository.Trim()) ?? [];
        IReadOnlyList<string> kept = allow
            ? (held.Any(Same) ? held : [.. held, to.Trim()])
            : [.. held.Where(name => !Same(name))];

        var key = KeyOf(next, repository.Trim());
        next.Remove(key);
        if (kept.Count > 0) next[key] = kept;
        return config with { WriteAcross = next };
    }

    public static IReadOnlyDictionary<string, bool> SetFlag(IReadOnlyDictionary<string, bool> map, string key, bool? value)
    {
        var next = new Dictionary<string, bool>(map, StringComparer.OrdinalIgnoreCase);
        if (value is { } set) next[key.Trim()] = set;
        else next.Remove(key.Trim());
        return next;
    }

    public static IReadOnlyDictionary<string, string> Set(IReadOnlyDictionary<string, string> map, string key, string? branch)
    {
        var next = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        if (branch is null) next.Remove(key);
        else next[key] = BranchName.IsValid(branch)
            ? branch
            : throw new DriverException($"`{branch}` is not a branch name git would take.");
        return next;
    }

    public static IReadOnlyList<string> Toggle(IReadOnlyList<string> names, string repository, bool present)
    {
        var kept = names.Where(n => !string.Equals(n, repository, StringComparison.OrdinalIgnoreCase));
        return present ? [.. kept, repository] : [.. kept];
    }
}
