namespace Daoris.Driver;

/// <summary>The environment variables a door's program reads for the model and the effort it runs with; null for one it reads none.</summary>
/// <param name="Model">The variable naming the model an account's own settings choose.</param>
/// <param name="Effort">The variable naming the effort, which overrides what the command carried.</param>
public sealed record SpawnChoiceVariables(string? Model, string? Effort)
{
    /// <summary>A program that reads no variable for either.</summary>
    public static SpawnChoiceVariables None { get; } = new(null, null);
}
