using System.Text.Json.Serialization;

namespace Daoris.Desktop;

/// <summary>
/// One repository's line as the bridge carries it: which, in which circle, and what said so. The lines slice adds
/// <see cref="NotReadYet"/> where the checkout's guess is not judged yet: the branch is none and the page says the line
/// not read yet, never what none would mean. The pull, which asks git, never answers it.
/// </summary>
internal sealed record LineRow(string Repository, string Workspace, string? Branch, string Source)
{
    /// <summary>The checkout's guess not judged yet: left out where false, so the pull's rows are as they always were.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool NotReadYet { get; init; }

    /// <summary>The line's integration policy as <see cref="LinesWires.IntegrateWire"/> carries it; left out where none is set.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Policy { get; init; }
}
