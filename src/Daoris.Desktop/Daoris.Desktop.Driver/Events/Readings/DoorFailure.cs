namespace Daoris.Driver;

/// <summary>Where on its door a failure is met.</summary>
public static class DoorWhere
{
    /// <summary>A <c>session/new</c>, <c>session/resume</c> or <c>session/load</c> the agent refused.</summary>
    public const string Open = "open";

    /// <summary>A prompt the agent answered with an error: the run's first, a held word's, or a try's.</summary>
    public const string Prompt = "prompt";

    /// <summary>The pipe door's failed <c>result</c>.</summary>
    public const string Result = "result";

    /// <summary>The door's own lines beside a failed result or exit, read for access alone.</summary>
    public const string Lines = "lines";
}

/// <summary>
/// What a door's wire carried when a turn ended on a failure, and nothing the agent wrote: the agent's own
/// streamed message, which carries the same words first, is never one, since an agent can write any sentence. A reading's
/// payload, so it sits with the readings below Agents; each transport builds it from its own wire (<c>DoorFailures</c>).
/// </summary>
/// <param name="Door"><c>protocol</c> or <c>native</c>.</param>
/// <param name="Code">The JSON-RPC code, or null where the wire carried none.</param>
/// <param name="Kind">The wire's own kind word (<c>data.errorKind</c>, the harness's <c>error</c> field), or null: an absent kind is no reading, never <c>unknown</c>.</param>
/// <param name="Status">An HTTP status the wire names, or null.</param>
/// <param name="Words">The error's own sentence, or null.</param>
/// <param name="Where">Where it is met (<see cref="DoorWhere"/>).</param>
public sealed record DoorFailure(string Door, int? Code, string? Kind, int? Status, string? Words, string Where);
