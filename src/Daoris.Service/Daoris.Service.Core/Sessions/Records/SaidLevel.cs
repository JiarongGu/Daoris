namespace Daoris.Knowledge;

/// <summary>
/// The levels a word on a record names in its <see cref="SaidWord.Level"/>: when its sender asked it to reach
/// a working session. No level stops a turn.
/// </summary>
public static class SaidLevel
{
    /// <summary>At a natural point, never into a turn in flight.</summary>
    public const string Ordinary = "ordinary";

    /// <summary>At the agent's next step, where its door takes words then.</summary>
    public const string Urgent = "urgent";

    /// <summary>Cutting the step in flight, where the agent steers: the person's alone.</summary>
    public const string Now = "now";

    /// <summary>Whether this build keeps a word at <paramref name="level"/>: none named, or one it knows.</summary>
    public static bool Known(string? level) => level is null or Ordinary or Urgent or Now;
}
