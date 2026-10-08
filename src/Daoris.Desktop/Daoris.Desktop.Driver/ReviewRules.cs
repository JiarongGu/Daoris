using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One review environment (REVIEWENV1a, D154 point 1, the review-environment design §1.1): where a repository's work runs for
/// the person to look at before it lands.
/// </summary>
/// <param name="Name">Lower-case letters, digits and dashes, at most 32, never production.</param>
/// <param name="Kind"><c>local</c> or <c>deployed</c>.</param>
/// <param name="Procedure">A repository-relative path to the document or skill that says how work reaches it; never read here.</param>
/// <param name="Address">Where the app is used: a local one's, required; a deployed one's, optional.</param>
/// <param name="Run">A local one's command for work needing a process of its own (§2.3), the person's standing say-so.</param>
public sealed record ReviewEnvironment(string Name, string Kind, string Procedure, string? Address = null, string? Run = null);

/// <summary>
/// A review rule (design §1.3): its environments, the first the default, and whether work waits for the person's look. With
/// no environment it is <see cref="None"/>, a repository's <c>false</c>: none here, whatever its workspace says.
/// </summary>
public sealed record ReviewRule(IReadOnlyList<ReviewEnvironment> Environments, bool Required = false)
{
    /// <summary>A repository's <c>false</c>: no review environment, whatever its workspace says.</summary>
    public static ReviewRule None { get; } = new([], false);

    /// <summary>Whether this is <see cref="None"/>.</summary>
    public bool IsNone => Environments.Count == 0;
}

/// <summary>Where a repository's review rule was set, for the screen, the room and the terminal.</summary>
public static class ReviewSource
{
    public const string Repository = "repository";
    public const string Workspace = "workspace";
}

/// <summary>A rule as it resolves for a repository, and where it was set (<see cref="ReviewSource"/>).</summary>
public sealed record ResolvedReview(ReviewRule Rule, string Source);

/// <summary>An environment as an entry or a door spells it, before it is judged: a field that is not text is null.</summary>
public sealed record ReviewSpelled(string? Name, string? Kind, string? Procedure, string? Address = null, string? Run = null);

/// <summary>
/// One change to a review rule, as every door makes it (design §1.7): for a <see cref="Repository"/> or a
/// <see cref="Workspace"/>, add or replace an environment (<see cref="Put"/>, with <see cref="Required"/> if given), say a
/// repository has <see cref="None"/>, <see cref="Drop"/> one, set <see cref="Required"/> alone, or <see cref="Clear"/>.
/// </summary>
public sealed record ReviewEdit
{
    public string? Repository { get; init; }
    public string? Workspace { get; init; }
    public ReviewSpelled? Put { get; init; }
    public bool? Required { get; init; }
    public bool None { get; init; }
    public string? Drop { get; init; }
    public bool Clear { get; init; }
}

/// <summary>
/// The review rule, <c>reviews</c> and <c>workspaceReviews</c> in <c>driver.json</c> (REVIEWENV1a, D154 point 2, design
/// §1.1–§1.3, §1.7): read, refused, resolved, said and edited — the one place every door asks.
/// </summary>
/// <remarks>
/// <para>A TWIN with the CLI's <c>reviews.ts</c>: both hold one table, the CLI's <c>test/fixtures/review-rules.json</c>, cell
/// for cell (<c>ReviewRulesTests</c> and <c>driverconfig.test.ts</c>): the reading and its precedence, every refusal in the
/// same words, each door's sentences, the edits, and what a checkout holding a procedure means.</para>
///
/// <para>Declared only (design §9): nothing here composes a step, gates a landing or serves anything; REVIEWENV1b–h read it.</para>
/// </remarks>
public static class ReviewRules
{
    /// <summary>The kinds a review environment may be — the CLI's <c>REVIEW_KINDS</c>.</summary>
    public static readonly IReadOnlyList<string> Kinds = ["local", "deployed"];

    /// <summary>
    /// The words a name reads as production by, whole or as a word between its dashes — a deliberate copy of the place words
    /// the service's <c>GoAheads.cs</c> reads <c>production</c> by, and of the CLI's <c>PRODUCTION_WORDS</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> Production = ["production", "prod", "prd", "live"];

    /// <summary>Said by each door after what the rule lets a step do, until the set-up step and the gate read it (REVIEWENV1c).</summary>
    public const string DeclaredOnly = "Declared only: nothing reads it yet, so no set-up step is composed and no landing waits for it.";

    /// <summary>What stands where nothing is set anywhere (design §1.8): today's behaviour, said as such.</summary>
    public const string NoneSet = "None: work is offered to land once its quest is done.";

    private const string RuleShape = "a review rule is its environments and whether it is required, or `false` for none here.";
    private const string NoEnvironments = "a review rule names at least one environment; the first is the default.";
    private const string EnvironmentShape = "each environment is an object: its `name`, `kind` and `procedure`, and a local one's `address`.";
    private const string NameRule = "lower-case letters, digits and dashes, at most 32 characters, such as `local` or `dev`.";
    private const string KindMissing = "an environment's `kind` is `local` or `deployed`.";
    private const string ProcedureMissing = "an environment names its `procedure`: the path, in the repository, of the document or skill that "
        + "says how work reaches it, such as `README.md`.";
    private const string AddressMissing = "a local environment needs its `address`: where the app normally runs, such as `http://localhost:4200`, "
        + "the only place a set-up step may show the work.";
    private const string RunDeployed = "only a local environment carries `run`: a deployed one is reached by its procedure, under your go-aheads.";
    private const string RunShape = "`run` is one command on one line: the one that starts work needing a process of its own.";
    private const string Scope = "a review rule is set for a repository or a workspace — name exactly one.";
    private const string OneAtATime = "one change at a time: add an environment (and whether it is required), drop one, say none, say whether "
        + "it is required, or clear.";
    private const string NoneWorkspace = "`none` is a repository's: a workspace with no review environment sets none, and `--clear` takes its rule away.";
    private const string NoneHere = "No review environment here, whatever its workspace says: work is offered to land once its quest is done.";

    private static readonly Regex NameShape = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);

    private static readonly Regex AddressShape = new(
        @"^https?://(?:\[[0-9A-Fa-f:.]+\]|[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)*)(?::([0-9]{1,5}))?/?\z",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A repository's entry read as a rule, with its first problem: <c>false</c> is <see cref="ReviewRule.None"/>; anything
    /// else must be an object naming at least one environment, each judged in order. A field that is not text is absent;
    /// only JSON <c>true</c> is required; what an environment has no field for is not kept.
    /// </summary>
    public static (ReviewRule? Rule, string? Problem) Read(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.False) return (ReviewRule.None, null);
        if (value.ValueKind != JsonValueKind.Object) return (null, RuleShape);
        if (!value.TryGetProperty("environments", out var environments) || environments.ValueKind != JsonValueKind.Array)
        {
            return (null, NoEnvironments);
        }

        var spelled = new List<ReviewSpelled>();
        foreach (var each in environments.EnumerateArray())
        {
            // An entry that is not an object is said at its place, after the environments before it are judged.
            if (each.ValueKind != JsonValueKind.Object)
            {
                return (null, (spelled.Count > 0 ? Judge(spelled, false).Problem : null) ?? EnvironmentShape);
            }

            spelled.Add(new ReviewSpelled(Text(each, "name"), Text(each, "kind"), Text(each, "procedure"), Text(each, "address"), Text(each, "run")));
        }

        return Judge(spelled, value.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.True);
    }

    /// <summary>The environments judged in order, the first problem said, or the rule as kept.</summary>
    private static (ReviewRule? Rule, string? Problem) Judge(IReadOnlyList<ReviewSpelled> environments, bool required)
    {
        if (environments.Count == 0) return (null, NoEnvironments);
        var kept = new List<ReviewEnvironment>();
        foreach (var environment in environments)
        {
            if (Problem(environment, kept) is { } problem) return (null, problem);
            kept.Add(Kept(environment));
        }

        return (new ReviewRule(kept, required), null);
    }

    /// <summary>The first problem of one environment, judged after those before it in its rule — the CLI's <c>environmentProblem</c>.</summary>
    private static string? Problem(ReviewSpelled environment, IReadOnlyList<ReviewEnvironment> earlier)
    {
        var (name, kind, procedure, address, run) = environment;
        if (name is null || name.Trim().Length == 0) return $"an environment is named: {NameRule}";
        if (!NameShape.IsMatch(name) || name.Length > 32) return $"`{name}` is not an environment's name — {NameRule}";
        if (name == "none") return "`none` says a repository has no review environment, so no environment is named it.";
        if (ReadsAsProduction(name))
        {
            return $"`{name}` reads as production, and production is never a review environment — name where work is looked at "
                + "before it lands, such as `local` or `dev`.";
        }

        if (earlier.Any(each => each.Name == name)) return $"`{name}` is named twice in one rule — each environment has a name of its own.";

        if (kind is null) return KindMissing;
        if (!Kinds.Contains(kind)) return $"`{kind}` is not a kind of review environment — `local` or `deployed`.";

        if (procedure is null || procedure.Trim().Length == 0) return ProcedureMissing;
        if (!IsProcedurePath(procedure))
        {
            return $"`{procedure}` is not a path in the repository — a procedure is named from its root with forward slashes, and "
                + "no `..`, root or drive, such as `docs/deploying-to-dev.md`.";
        }

        if (address is null && kind == "local") return AddressMissing;
        if (address is not null && !IsAddress(address))
        {
            return $"`{address}` is not an address — an absolute `http` or `https` origin, such as `http://localhost:4200`.";
        }

        if (run is not null && kind != "local") return RunDeployed;
        if (run is not null && (run.Trim().Length == 0 || run.Contains('\r') || run.Contains('\n'))) return RunShape;
        return null;
    }

    /// <summary>Whether a name reads as production: the whole of it, or a word between its dashes, is one of <see cref="Production"/>.</summary>
    public static bool ReadsAsProduction(string name) => name.Split('-').Any(word => Production.Contains(word));

    /// <summary>
    /// A path a procedure may be (design §1.1, as KNOWUSE1c names a router): from the repository's root, with forward slashes,
    /// no space around it, and no <c>..</c>, <c>.</c>, empty part, root, drive or control character.
    /// </summary>
    private static bool IsProcedurePath(string path)
    {
        if (path != path.Trim() || path.StartsWith('/') || path.Contains('\\') || path.Contains(':')) return false;
        if (path.Any(c => c < 0x20 || c == '\u007f')) return false;
        return path.Split('/').All(part => part.Length > 0 && part is not "." and not "..");
    }

    /// <summary>An absolute <c>http</c> or <c>https</c> origin: a host or a bracketed IPv6 address, a port from 1 to 65535, nothing after but one <c>/</c>.</summary>
    private static bool IsAddress(string address)
    {
        var match = AddressShape.Match(address);
        if (!match.Success) return false;
        return !match.Groups[1].Success || int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) is >= 1 and <= 65_535;
    }

    /// <summary>An environment as kept: an address without its last slash, and absent fields left out.</summary>
    private static ReviewEnvironment Kept(ReviewSpelled environment) => new(
        environment.Name!, environment.Kind!, environment.Procedure!,
        environment.Address is { } address && address.EndsWith('/') ? address[..^1] : environment.Address,
        environment.Run);

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// A map of review rules as <c>driver.json</c> holds it: each name trimmed, a blank one not read; a rule with a problem,
    /// and <c>true</c>, not read; <c>false</c> read only where <paramref name="allowNone"/> (a repository's, never a
    /// workspace's); a name written twice in any case read where first readable. A map that is not one is none.
    /// </summary>
    internal static IReadOnlyDictionary<string, ReviewRule> Map(JsonElement root, string name, bool allowNone)
    {
        var map = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || (property.Value.ValueKind == JsonValueKind.False && !allowNone)) continue;
            if (Read(property.Value) is not ({ } rule, null) || map.ContainsKey(named)) continue;
            map[named] = rule;
        }

        return map;
    }

    /// <summary>A map written as <c>driver.json</c> keeps it, only when set: <c>false</c> for none, else the rule as kept.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, ReviewRule> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, rule) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key);
            Write(writer, rule);
        }

        writer.WriteEndObject();
    }

    /// <summary>One rule as kept: <c>false</c> for none; else <c>required</c> only when on, then the environments in order.</summary>
    public static void Write(Utf8JsonWriter writer, ReviewRule rule)
    {
        if (rule.IsNone)
        {
            writer.WriteBooleanValue(false);
            return;
        }

        writer.WriteStartObject();
        if (rule.Required) writer.WriteBoolean("required", true);
        writer.WriteStartArray("environments");
        foreach (var environment in rule.Environments)
        {
            writer.WriteStartObject();
            writer.WriteString("name", environment.Name);
            writer.WriteString("kind", environment.Kind);
            writer.WriteString("procedure", environment.Procedure);
            if (environment.Address is { } address) writer.WriteString("address", address);
            if (environment.Run is { } run) writer.WriteString("run", run);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    /// The review rule standing for a repository: its own (a rule or none), else its workspace's (one in no workspace being
    /// in <c>default</c>), else null, which is nothing set anywhere — names matched in any case, as every door matches them.
    /// </summary>
    public static ResolvedReview? Resolve(DriverConfig config, string repository, string? workspace) =>
        config.Reviews.TryGetValue(repository.Trim(), out var own)
            ? new ResolvedReview(own, ReviewSource.Repository)
            : config.WorkspaceReviews.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)
                ? new ResolvedReview(shared, ReviewSource.Workspace)
                : null;

    /// <summary>Names in backticks, the last after <c>or</c>: <c>a</c>, <c>a or b</c>, <c>a, b or c</c>.</summary>
    private static string Either(IReadOnlyList<string> names)
    {
        var ticked = names.Select(name => $"`{name}`").ToList();
        return ticked.Count <= 1 ? string.Concat(ticked) : $"{string.Join(", ", ticked.Take(ticked.Count - 1))} or {ticked[^1]}";
    }

    /// <summary>
    /// What each door says as a rule is set (design §1.7): whether work waits for the person's look and where, then what a
    /// set-up step may do in each environment, in order — the CLI's <c>reviewSays</c>, word for word.
    /// </summary>
    public static IReadOnlyList<string> Says(ReviewRule rule)
    {
        if (rule.IsNone) return [NoneHere];
        var said = new List<string>();
        var others = rule.Environments.Skip(1).Select(each => each.Name).ToList();
        if (rule.Required)
        {
            said.Add($"Before work here lands, it is shown to you in `{rule.Environments[0].Name}` and waits for you to say it is right.");
            if (others.Count > 0) said.Add($"A task may ask for {Either(others)} instead.");
        }
        else
        {
            said.Add($"When a task asks for it, work here may be shown to you in {Either([.. rule.Environments.Select(each => each.Name)])}; "
                + "nothing waits for it.");
        }

        foreach (var environment in rule.Environments)
        {
            if (environment.Kind == "local")
            {
                said.Add($"A set-up step here builds the work in its own tree and shows it in Daoris's browser at `{environment.Address}`; "
                    + "your own servers and processes are not touched.");
                if (environment.Run is { } run)
                {
                    said.Add($"A set-up step here may run `{run}` in its tree on a port nobody holds, without asking you each time; "
                        + "it stops once you have reviewed.");
                }
            }
            else
            {
                said.Add($"A set-up step here follows `{environment.Procedure}` toward `{environment.Name}`; each deploy or write there "
                    + "asks your go-ahead once per ask.");
            }
        }

        return said;
    }

    /// <summary>
    /// The config with one change made, or the refusal thrown in the CLI's words, nothing changed — the CLI's
    /// <c>applyReviewEdit</c>. A repository's rule starts afresh rather than from its workspace's, since it replaces that whole;
    /// an environment put under a name already there replaces it where it stands; the key keeps the spelling first written.
    /// </summary>
    /// <exception cref="DriverException">The edit's refusal, in the twin's words.</exception>
    public static DriverConfig Apply(DriverConfig config, ReviewEdit edit)
    {
        var repository = edit.Repository?.Trim() is { Length: > 0 } r ? r : null;
        var workspace = edit.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        if ((repository is null) == (workspace is null)) throw new DriverException(Scope);

        var changes = new[] { edit.Put is not null, edit.None, edit.Drop is not null, edit.Clear }.Count(change => change);
        if (changes > 1 || (changes == 0 && edit.Required is null) || (edit.Required is not null && changes == 1 && edit.Put is null))
        {
            throw new DriverException(OneAtATime);
        }

        if (edit.None && workspace is not null) throw new DriverException(NoneWorkspace);

        var map = repository is not null ? config.Reviews : config.WorkspaceReviews;
        var named = (repository ?? workspace)!;
        var key = map.Keys.FirstOrDefault(k => string.Equals(k, named, StringComparison.OrdinalIgnoreCase)) ?? named;
        var whose = repository is not null ? $"`{key}`" : $"the workspace `{key}`";
        var ownRule = map.TryGetValue(key, out var own) && !own.IsNone ? own : null;

        ReviewRule? next;
        if (edit.Clear)
        {
            next = null;
        }
        else if (edit.None)
        {
            next = ReviewRule.None;
        }
        else if (edit.Drop is { } drop)
        {
            if (ownRule is null || !ownRule.Environments.Any(each => each.Name == drop))
            {
                throw new DriverException($"{whose} has no environment `{drop}` of its own to drop.");
            }

            if (ownRule.Environments.Count == 1)
            {
                throw new DriverException($"`{drop}` is the only environment of {whose} — `--clear` hands it back to what stands above it, "
                    + "and for a repository `none` says it has none.");
            }

            next = ownRule with { Environments = [.. ownRule.Environments.Where(each => each.Name != drop)] };
        }
        else if (edit.Put is not { } put)
        {
            if (ownRule is null) throw new DriverException($"{whose} has no review rule of its own — add an environment to it first.");
            next = ownRule with { Required = edit.Required == true };
        }
        else
        {
            var base_ = (ownRule?.Environments ?? []).Select(each => new ReviewSpelled(each.Name, each.Kind, each.Procedure, each.Address, each.Run)).ToList();
            var at = base_.FindIndex(each => each.Name == put.Name);
            if (at == -1) base_.Add(put);
            else base_[at] = put;
            var judged = Judge(base_, edit.Required ?? ownRule?.Required == true);
            next = judged.Rule ?? throw new DriverException(judged.Problem!);
        }

        var changed = new Dictionary<string, ReviewRule>(map, StringComparer.OrdinalIgnoreCase);
        if (next is null) changed.Remove(key);
        else changed[key] = next;
        return repository is not null ? config with { Reviews = changed } : config with { WorkspaceReviews = changed };
    }

    /// <summary>
    /// Whether a checkout holds a procedure (design §1.1): a regular file at that path, reached through folders, with no link
    /// on the way — the CLI's <c>holdsProcedure</c>. Read where the registry says the checkout is, never for meaning.
    /// </summary>
    public static bool Holds(string root, string procedure)
    {
        if (!IsProcedurePath(procedure)) return false;
        var parts = procedure.Split('/');
        var at = root;
        for (var index = 0; index < parts.Length; index++)
        {
            at = Path.Combine(at, parts[index]);
            FileSystemInfo found = index < parts.Length - 1 ? new DirectoryInfo(at) : new FileInfo(at);
            if (!found.Exists || found.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
        }

        return true;
    }

    /// <summary>
    /// The repositories of <paramref name="checkouts"/> that a put of <paramref name="procedure"/> reaches and whose checkout
    /// here does not hold it: the one named, or each of the workspace's, matched in any case.
    /// </summary>
    public static IReadOnlyList<string> Lacking(
        IEnumerable<(string Repository, string? Workspace, string? Root)> checkouts, ReviewEdit edit, string procedure) =>
        [.. checkouts
            .Where(each => each.Root is { Length: > 0 })
            .Where(each => edit.Repository is { } repository
                ? string.Equals(each.Repository, repository.Trim(), StringComparison.OrdinalIgnoreCase)
                : string.Equals(RemoteTarget.Workspace(each.Workspace), RemoteTarget.Workspace(edit.Workspace), StringComparison.OrdinalIgnoreCase))
            .Where(each => !Holds(each.Root!, procedure))
            .Select(each => each.Repository)];

    /// <summary>The refusal a repository's put says when its checkout here does not hold the procedure, nothing written.</summary>
    public static string NotHeld(string repository, string procedure) =>
        $"`{procedure}` is not a file in `{repository}`'s checkout here — a procedure is a document or a skill the repository "
        + "holds, and writing it is that repository's own work. Nothing was written.";

    /// <summary>What a workspace's put says of a repository there whose checkout does not hold the procedure.</summary>
    public static string SitsUntil(string repository, string procedure) =>
        $"`{repository}` holds no `{procedure}` here: its set-up steps sit until it does.";

    /// <summary>
    /// The room's cell for a repository (design §1.7–§1.8): its environments with their kind, address, procedure and command,
    /// whether work waits for the person's look, and where it was set; <c>no review environment</c> where nothing is.
    /// </summary>
    public static string RoomCell(ResolvedReview? review)
    {
        if (review is null) return "no review environment";
        var source = review.Source == ReviewSource.Repository ? "set for it" : "its workspace's rule";
        if (review.Rule.IsNone) return $"no review environment ({source})";
        var environments = review.Rule.Environments.Select(each =>
        {
            var parts = new List<string> { each.Kind };
            if (each.Address is { } address) parts.Add($"`{address}`");
            parts.Add($"by `{each.Procedure}`");
            if (each.Run is { } run) parts.Add($"runs `{run}`");
            return $"`{each.Name}` ({string.Join(", ", parts)})";
        });
        var waits = review.Rule.Required ? "work waits for the person's review before it lands" : "shown when a task asks";
        return $"{string.Join("; ", environments)}, the first the default; {waits} ({source})";
    }

    /// <summary>A rule as compact JSON, for a test to compare with the shared table's cells.</summary>
    public static string ToJson(ReviewRule rule)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(writer, rule);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
