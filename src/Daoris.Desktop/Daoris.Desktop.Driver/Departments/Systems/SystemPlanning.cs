using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What became of a session's <c>system_plan</c>, written beside its request: whether a plan is kept, a code where none is, and the words the agent reads.</summary>
public sealed record SystemPlanAnswer(bool Done, string? Code, string Said)
{
    /// <summary>No service answered, so the session's department could not be read.</summary>
    public const string NoService = "no-service";

    /// <summary>The session's department is no system: it has no adapter to plan through.</summary>
    public const string NotSystem = "not-system";

    /// <summary>The changes do not read as a list of resources, each named once and given a value or removed.</summary>
    public const string Named = "named";

    /// <summary>The adapter could not be started, or answered in no shape a plan reads.</summary>
    public const string Failed = "failed";

    /// <summary>A refusal with its code, its words ending that nothing is planned.</summary>
    public static SystemPlanAnswer Refusal(string code, string said) => new(false, code, $"{said} Nothing was planned.");
}

/// <summary>
/// The loop's half of the connector's <c>system_plan</c>: the session's own record, its department read from the registry, and where that
/// is a system, the session's changes handed to the system's adapter, which reads each resource as it stands and types the change;
/// what it comes to is kept as the record's next plan (<see cref="SystemPlans"/>), which the review shows and the <c>apply</c> applies.
/// Each session plans under its own record, so two planning against one system at once each keep their own; nothing here writes to the
/// system.
/// </summary>
/// <param name="service">The loop's service once it answers; null before, when every request is refused saying so.</param>
/// <param name="adapters">How an adapter is started; the plugins installed in the home unless a test hands its own.</param>
public sealed class SystemPlanning(
    string home, Func<ServiceClient?> service, Func<SystemPlace, CancellationToken, Task<IHookChannel>>? adapters = null, Func<DateTimeOffset>? clock = null)
{
    /// <summary>The connector's tool, as it is named on the wire.</summary>
    public const string Tool = "system_plan";

    private readonly Func<SystemPlace, CancellationToken, Task<IHookChannel>> _adapters = adapters ?? SystemAdapters.Starter(home);

    /// <summary>Judge one request and plan where its session works in a system; a refusal is an answer, never thrown.</summary>
    public async Task<SystemPlanAnswer> AnswerAsync(SessionRequest request, CancellationToken ct)
    {
        if (Intents(request.Text) is not { } intents)
        {
            return SystemPlanAnswer.Refusal(SystemPlanAnswer.Named, "The changes do not read as a list of resources, each named once and given a value or removed.");
        }

        if (service() is not { } ledger)
        {
            return SystemPlanAnswer.Refusal(SystemPlanAnswer.NoService, "Daoris's service is not answering yet, so the session's department could not be read.");
        }

        var record = (await ledger.SessionRecordsAsync(ct).ConfigureAwait(false)).FirstOrDefault(each => string.Equals(each.Id, request.Session, StringComparison.Ordinal));
        var department = record is null
            ? null
            : (await ledger.RegistryAsync(ct).ConfigureAwait(false)).FirstOrDefault(row => string.Equals(row.Repository, record.Repository, StringComparison.OrdinalIgnoreCase));
        return await PlanAsync(request.Session, department, intents, ct).ConfigureAwait(false);
    }

    /// <summary><paramref name="record"/>'s changes planned against <paramref name="department"/>'s system and kept as its next plan.</summary>
    public async Task<SystemPlanAnswer> PlanAsync(string record, RepoView? department, IReadOnlyList<SystemIntent> intents, CancellationToken ct)
    {
        if (department is not { Kind: PlaceKind.System, System: { } place })
        {
            return SystemPlanAnswer.Refusal(SystemPlanAnswer.NotSystem,
                $"Session `{record}` works in {(department is null ? "no department registered here" : $"`{department.Repository}`, which is no system")}: "
                + "a plan is a system department's, and a repository's or a folder's work changes its files.");
        }

        SystemChangeSet plan;
        try
        {
            await using var channel = await _adapters(place, ct).ConfigureAwait(false);
            plan = await new SystemAdapter(channel, place, new SystemPlans(home), clock ?? (() => DateTimeOffset.UtcNow)).PlanAsync(record, intents, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return SystemPlanAnswer.Refusal(SystemPlanAnswer.Failed, error.Message.TrimEnd());
        }

        return new SystemPlanAnswer(true, null, Said(department.Repository, plan));
    }

    /// <summary>The words the agent reads: the plan's number, and each resource with what it comes to, or that it changes nothing.</summary>
    public static string Said(string department, SystemChangeSet plan)
    {
        var kept = $"Plan {plan.Plan} is kept for `{department}` (system `{plan.Place}`)";
        if (plan.Operations.Count == 0) return $"{kept} and changes nothing: every resource already holds what you asked.";
        var listed = string.Join(", ", plan.Operations.Select(operation => $"{operation.Op} `{operation.Resource}`"));
        return $"{kept}: {listed}. Nothing in the system has changed: the person reviews it, each resource before and after, and it is applied after their Accept.";
    }

    /// <summary>The connector's changes, a JSON list of <c>{ resource, value }</c> or <c>{ resource, remove: true }</c>; null where it is not one.</summary>
    public static IReadOnlyList<SystemIntent>? Intents(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
            var intents = new List<SystemIntent>();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("resource", out var named) || named.GetString() is not { Length: > 0 } resource) return null;
                var remove = row.TryGetProperty("remove", out var removed) && removed.ValueKind == JsonValueKind.True;
                var value = row.TryGetProperty("value", out var given) && given.ValueKind == JsonValueKind.String ? given.GetString() : null;
                if (remove == (value is not null)) return null;
                if (intents.Any(each => string.Equals(each.Resource, resource, StringComparison.Ordinal))) return null;
                intents.Add(new SystemIntent(resource, value, remove));
            }

            return intents.Count > 0 ? intents : null;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
