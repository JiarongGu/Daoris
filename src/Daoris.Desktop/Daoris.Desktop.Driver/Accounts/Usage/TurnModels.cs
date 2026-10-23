using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One model's share of a turn's tokens, as its door split them (USAGECAP1a2, D179 point 2).</summary>
public sealed record ModelTokens(string Model, TurnTokens Tokens);

/// <summary>
/// Which models a turn's tokens were spent on, as its door said them (USAGECAP1a2, D179 point 2), for the usage history alone:
/// carried in memory on the turn's event and never written to the session's record, which keeps no model's name (D24, TOOL3).
/// </summary>
/// <remarks>
/// <para><b>A split is taken only where it agrees with the turn's total.</b> The total is the door's measured per-turn report
/// (CONV5); the split by model is not measured on either door. So the models the turn's answers named (or, named by none, the one
/// model whose share is the total) must sum to the total on every kind the total reports. Where the split read as the process's
/// running total agrees, the turn's own is what grew since the turn before; where the split as reported agrees, it is the turn's
/// own. Neither agreeing, there is no split: the model the answers named stands, and nothing is reconciled or guessed.</para>
/// <para><b>A helper model's tokens</b> (a small model the harness called on its own, counted in the split and not in the total)
/// make the turn not whole: they are kept beside the total, never added into it.</para>
/// </remarks>
/// <param name="Model">The one model the turn's total was spent on, or <see cref="Mixed"/> for several, or <see cref="Unknown"/>.</param>
/// <param name="Split">Each model's share, where the door's split agreed with the total; null where it reported none or did not agree.</param>
/// <param name="Whole">
/// False where a helper model's tokens fall outside the turn's total, true where the split holds none, null where no split says.
/// </param>
/// <param name="From">
/// Where the attribution came from: the split's source (<see cref="FromModelUsage"/>, <see cref="FromQuota"/>, either with
/// <see cref="Since"/> where it was read as a running total), or else where the model was named (<see cref="FromMessages"/>,
/// <see cref="FromUpdates"/>); null where nothing named one.
/// </param>
public sealed record TurnModels(string Model, IReadOnlyList<ModelTokens>? Split, bool? Whole, string? From)
{
    /// <summary>The turn's total was spent on more than one model.</summary>
    public const string Mixed = "mixed";

    /// <summary>No model was named, or none as a plain name.</summary>
    public const string Unknown = "unknown";

    /// <summary>The native door: each top-level answer's <c>message.model</c>.</summary>
    public const string FromMessages = "message";

    /// <summary>The native door's split: the result's <c>modelUsage</c> by model.</summary>
    public const string FromModelUsage = "modelUsage";

    /// <summary>The protocol door: the turn's <c>usage_update</c>s' <c>_meta["_claude/model"]</c>.</summary>
    public const string FromUpdates = "usage_update";

    /// <summary>The protocol door's split: the prompt response's <c>_meta.quota</c> by model.</summary>
    public const string FromQuota = "quota";

    /// <summary>Beside a split's source: read as the process's running total, less what it said at the turn before.</summary>
    public const string Since = "-since";

    // A model's name as a maker writes it, its context's suffix among them (`[1m]`): never a space, an `@` or a path's slash.
    private static readonly Regex Plain = new(@"^[A-Za-z0-9][A-Za-z0-9_.:\[\]-]{0,99}$", RegexOptions.CultureInvariant);

    /// <summary>A model's name as the history keeps it, or null for anything that is not a plain name.</summary>
    public static string? Name(string? said) => said is not null && Plain.IsMatch(said) ? said : null;

    /// <summary>
    /// A turn's models (see the remarks): <paramref name="named"/> the models its answers named, <paramref name="reported"/> the
    /// door's split this turn, <paramref name="before"/> the same split as the door last said it in this process.
    /// </summary>
    public static TurnModels Of(
        TurnTokens? total, IEnumerable<string?> named, string namedFrom, IReadOnlyDictionary<string, TurnTokens>? reported,
        IReadOnlyDictionary<string, TurnTokens>? before, string reportedFrom)
    {
        var main = named.Select(Name).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (total is not null && reported is { Count: > 0 } && reported.Keys.All(key => Name(key) is not null))
        {
            if (before is { Count: > 0 } && Less(reported, before) is { } since && Agreeing(since, total, main) is { } grew)
            {
                return Agreed(grew, since, reportedFrom + Since);
            }

            if (Agreeing(reported, total, main) is { } own) return Agreed(own, reported, reportedFrom);
        }

        return new(main.Count switch { 0 => Unknown, 1 => main[0], _ => Mixed }, null, null, main.Count > 0 ? namedFrom : null);
    }

    /// <summary>
    /// A split from the wire: an object keyed by model (<c>modelUsage</c>) or an array of objects each naming its
    /// <c>model</c> (<c>quota.model_usage</c>), each model's counts read by <see cref="Counts"/>, from the entry's own
    /// <c>token_count</c> where it has one (both protocol agents' measured shape, MODELROLE1a). Null for any other shape, an
    /// entry with no name, or a name twice.
    /// </summary>
    public static IReadOnlyDictionary<string, TurnTokens>? Read(JsonElement split)
    {
        var read = new Dictionary<string, TurnTokens>(StringComparer.Ordinal);
        if (split.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in split.EnumerateObject())
            {
                if (Counts(entry.Value) is not { } counts || !read.TryAdd(entry.Name, counts)) return null;
            }

            return read;
        }

        if (split.ValueKind != JsonValueKind.Array) return null;
        foreach (var entry in split.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || Json.Text(entry, "model") is not { Length: > 0 } model
                || Counts(entry.TryGetProperty("token_count", out var inner) ? inner : entry) is not { } counts
                || !read.TryAdd(model, counts))
            {
                return null;
            }
        }

        return read;
    }

    /// <summary>
    /// One model's four counts, each by the names a wire is known to use for it: the protocol door's <c>usage</c>
    /// (<c>inputTokens</c>, <c>cachedReadTokens</c>), its quota's (<c>cachedInputTokens</c>), the SDK's <c>ModelUsage</c> (<c>cacheReadInputTokens</c>,
    /// <c>cacheCreationInputTokens</c>) and the Messages API's (<c>input_tokens</c>). Null where it names none. A zero stays
    /// zero here: it is one model's share, and the turn's total is what says whether anything was reported.
    /// </summary>
    public static TurnTokens? Counts(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object) return null;
        long? Count(params string[] names)
        {
            foreach (var name in names)
            {
                if (entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n)) return n;
            }

            return null;
        }

        var counts = new TurnTokens(
            Count("inputTokens", "input_tokens"),
            Count("outputTokens", "output_tokens"),
            Count("cachedReadTokens", "cachedInputTokens", "cacheReadInputTokens", "cache_read_input_tokens"),
            Count("cachedWriteTokens", "cacheCreationInputTokens", "cache_creation_input_tokens"));
        return counts is { Input: null, Output: null, CacheRead: null, CacheWrite: null } ? null : counts;
    }

    /// <summary>The split less the one before, model by model; null where any count fell, or a model it held is gone.</summary>
    private static Dictionary<string, TurnTokens>? Less(IReadOnlyDictionary<string, TurnTokens> now, IReadOnlyDictionary<string, TurnTokens> before)
    {
        if (before.Any(pair => !now.ContainsKey(pair.Key) && Spent(pair.Value))) return null;
        var since = new Dictionary<string, TurnTokens>(StringComparer.Ordinal);
        foreach (var (model, tokens) in now)
        {
            if (!before.TryGetValue(model, out var was))
            {
                since[model] = tokens;
                continue;
            }

            long? Grew(long? a, long? b) => a is { } x && b is { } y ? x - y : null;
            var grew = new TurnTokens(Grew(tokens.Input, was.Input), Grew(tokens.Output, was.Output), Grew(tokens.CacheRead, was.CacheRead), Grew(tokens.CacheWrite, was.CacheWrite));
            if (grew.Input < 0 || grew.Output < 0 || grew.CacheRead < 0 || grew.CacheWrite < 0) return null;
            since[model] = grew;
        }

        return since;
    }

    /// <summary>The models the total was spent on, where the split agrees with it: the named ones, or else the one whose share it is.</summary>
    private static IReadOnlyList<string>? Agreeing(IReadOnlyDictionary<string, TurnTokens> split, TurnTokens total, IReadOnlyList<string> named)
    {
        if (named.Count > 0) return Sums(named.Select(model => split.GetValueOrDefault(model)), total) ? named : null;
        var whose = split.Where(pair => Sums([pair.Value], total)).Select(pair => pair.Key).ToList();
        return whose.Count == 1 ? whose : null;
    }

    /// <summary>Whether the parts sum to the total on every kind the total reports, each part reporting it too.</summary>
    private static bool Sums(IEnumerable<TurnTokens?> parts, TurnTokens total)
    {
        var all = parts.ToList();
        if (all.Count == 0 || all.Any(part => part is null) || total is { Input: null, Output: null, CacheRead: null, CacheWrite: null }) return false;
        bool Kind(Func<TurnTokens, long?> of) =>
            of(total) is not { } want || (all.All(part => of(part!) is not null) && all.Sum(part => of(part!)!.Value) == want);
        return Kind(t => t.Input) && Kind(t => t.Output) && Kind(t => t.CacheRead) && Kind(t => t.CacheWrite);
    }

    private static TurnModels Agreed(IReadOnlyList<string> main, IReadOnlyDictionary<string, TurnTokens> split, string from)
    {
        var spent = split.Where(pair => Spent(pair.Value)).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ModelTokens(pair.Key, pair.Value)).ToList();
        return new(main.Count == 1 ? main[0] : Mixed, spent, spent.All(each => main.Contains(each.Model, StringComparer.Ordinal)), from);
    }

    private static bool Spent(TurnTokens tokens) => tokens.Input > 0 || tokens.Output > 0 || tokens.CacheRead > 0 || tokens.CacheWrite > 0;
}
