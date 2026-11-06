namespace Daoris.Driver;

/// <summary>
/// What a loop hands the starting kinds to build their doors from (D247 §3.5): the home, whether this loop may start now (it holds the lease
/// and no update drains), the machine log, and the loop's own pieces a door is carried out through. A piece left out builds no door that needs it.
/// </summary>
/// <param name="MayStart">Whether a door may start now: this loop holds the home's lease and no update drains (DRAINHOLD1).</param>
internal sealed record NodeDoorWiring(string Home, Func<bool> MayStart)
{
    /// <summary>The machine log a door tells what it did to, by its code.</summary>
    public Action<string, string>? Log { get; init; }

    /// <summary>The landing a run's <c>land</c> is performed through, landing as the review's Accept lands it.</summary>
    public Func<LandPerformer>? Landing { get; init; }

    /// <summary>The heavy executor a <c>check</c> is handed to.</summary>
    public HeavyExecutor? Executor { get; init; }

    /// <summary>The door a unit's <c>integrate</c> asks; the home's own where none is handed.</summary>
    public IIntegrationDoor? Integration { get; init; }

    /// <summary>The service client a run's <c>request</c> publishes its quest through.</summary>
    public IRequestPort? Requests { get; init; }

    /// <summary>Tells the machine log <paramref name="said"/> under <paramref name="code"/>.</summary>
    public void Say(string code, string said) => Log?.Invoke(code, said);
}
