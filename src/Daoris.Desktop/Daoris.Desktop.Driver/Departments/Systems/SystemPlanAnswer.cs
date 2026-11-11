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
