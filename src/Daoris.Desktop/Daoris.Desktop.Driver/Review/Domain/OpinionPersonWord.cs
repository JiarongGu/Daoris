namespace Daoris.Driver;

/// <summary>What the person said at the gate (design §8.5), kept by the driver for the chain's work in a repository.</summary>
public static class OpinionPersonSaid
{
    /// <summary><i>Go on anyway…</i>: what was unsettled stays so, and the gate's next item follows.</summary>
    public const string Anyway = "anyway";

    /// <summary><i>I looked myself…</i>: the person's own reading, in place of another agent's.</summary>
    public const string Myself = "myself";

    /// <summary>A press that showed what was unsettled and was made with it shown: <i>Accept…</i> on the screen.</summary>
    public const string Press = "press";
}

/// <summary>
/// One answer of the person's at the gate (design §8.5): what they said, the commit it was said at, when, their words, and the
/// door. It covers the work while what would land is that commit or one before it: a commit after it is work they did not see.
/// </summary>
public sealed record OpinionPersonWord(string Said, string Tip, DateTimeOffset At)
{
    public string? Words { get; init; }

    /// <summary><c>terminal</c> or <c>screen</c> (<see cref="ReviewDoors"/>).</summary>
    public string? Door { get; init; }
}
