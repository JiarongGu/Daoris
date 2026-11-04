namespace Daoris.Driver;

/// <summary>What the executor runs with: its clocks, the environment its jobs are composed from, the admission's seams, and who it tells.</summary>
public sealed record VerifyWorld
{
    /// <summary>The clock the bound and a busy runner's ten minutes are read on: the machine's unless a test holds it.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>Between two looks at slots another run holds.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The environment every job is composed from (<see cref="GateEnvironment.Compose"/>): the driver's own, never a shell's.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Start { get; init; } = Own();

    public AdmissionWaiting Admission { get; init; } = AdmissionWaiting.Default;

    /// <summary>How many places the machine admits, where not this machine's.</summary>
    public int? Capacity { get; init; }

    /// <summary>A variable of the driver's own environment, which the admission reads for a hold this process is already inside.</summary>
    public Func<string, string?> Variable { get; init; } = Environment.GetEnvironmentVariable;

    /// <summary>This machine's heavy gates for a repository that declares none (HEAVYRUN1b2a), by its name.</summary>
    public Func<string, IReadOnlyList<HeavyGate>?> Mapping { get; init; } = _ => null;

    /// <summary>Where the executor says what it waits for and runs.</summary>
    public TextWriter Said { get; init; } = TextWriter.Null;

    /// <summary>Told once an attempt's result is written: the runtime asks a pass, which reconciles it.</summary>
    public Action<string, string>? Answered { get; init; }

    private static IReadOnlyList<KeyValuePair<string, string>> Own() =>
        [.. Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Key is string && entry.Value is string)
            .Select(entry => KeyValuePair.Create((string)entry.Key, (string)entry.Value!))];
}
