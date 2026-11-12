namespace Daoris.Driver;

/// <summary>The versions of a measurement: the agent's program as it reported itself, and its adapter where a door has one.</summary>
public sealed record MeasuredAt(string Agent, string? Adapter = null)
{
    /// <summary>The configuration as a sheet's row says it.</summary>
    public string Said => $"agent {Agent}" + (Adapter is null ? "" : $", adapter {Adapter}");
}
