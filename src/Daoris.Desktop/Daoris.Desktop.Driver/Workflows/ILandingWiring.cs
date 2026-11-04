namespace Daoris.Driver;

/// <summary>What the look's landings reach of the driver beyond <see cref="ILookWiring"/>: their plugins' wiring and the wakes the watch hands a look.</summary>
internal interface ILandingWiring : ILookWiring
{
    /// <summary>Where a landing's plugin says its lines on the console under its name, as at a press.</summary>
    SessionOutput? Output { get; }

    /// <summary>The loop's hooks, whose log keeps what the landing did with each plugin.</summary>
    HookSet? Hooks { get; }

    /// <summary>The wakes a plan was woken by, which its landing looks at alone; null lands on every due entry.</summary>
    IReadOnlyCollection<LandingWake>? Woke { get; }

    /// <summary>Hands back the wakes this look took, told by a selection beside the look that failed before it judged with them.</summary>
    Action? WokeBack { get; }

    /// <summary>Asks the loop's runtime for a whole pass, which a landing held by a run's graph for a judgement asks.</summary>
    Action? PassAsked { get; }

    /// <summary>Told once a landing pass this look started has ended, so the loop offers again the wakes it held back meanwhile.</summary>
    Action? LandingEnded { get; }
}
