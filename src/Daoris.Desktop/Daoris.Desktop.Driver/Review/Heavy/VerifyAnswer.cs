namespace Daoris.Driver;

/// <summary>What the executor answers of an attempt (the pipeline design §4.2), which the performer's reconcile reads.</summary>
public static class VerifyAnswers
{
    /// <summary>Its result is written: reached, failed or unknown.</summary>
    public const string Result = "result";

    /// <summary>It waits for its slots, or runs, here.</summary>
    public const string Running = "running";

    /// <summary>No result and no job of it here: cut off before its result, so it never ran.</summary>
    public const string NotStarted = "not-started";

    /// <summary>Nothing establishes what became of it.</summary>
    public const string Unknown = "unknown";
}

/// <summary>An attempt as the executor answers it: its state, its result where written, and why where it is not known.</summary>
public sealed record VerifyAnswer(string State, VerifyResult? Result = null)
{
    public string? Why { get; init; }
}
