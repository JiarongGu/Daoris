namespace Daoris.Driver;

/// <summary>What the look's landing reaches of the driver beyond <see cref="ILookWiring"/>: its plugins' wiring, whose types sit above Work orders.</summary>
internal interface ILandingWiring : ILookWiring
{
    /// <summary>Where a landing's plugin says its lines on the console under its name, as at a press.</summary>
    SessionOutput? Output { get; }

    /// <summary>The loop's hooks, whose log keeps what the landing did with each plugin.</summary>
    HookSet? Hooks { get; }
}
