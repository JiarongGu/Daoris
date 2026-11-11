namespace Daoris.Driver;

/// <summary>
/// A trial's check of what a plugin implements beside its points, read as the driver reads it: each entry refused, an interface this
/// build does not know and a forget that is not run fail the step in one sentence; otherwise it says what Daoris reaches. Nothing is
/// run: a command reaches its platform, which no trial does.
/// </summary>
public static class PluginTrialInterfaces
{
    public const string Name = "interfaces";

    /// <summary>The step; null for a plugin that declares none.</summary>
    public static TrialStep? Step(PluginManifest manifest)
    {
        var interfaces = manifest.Interfaces;
        var lines = PluginInterfaceLines.Of(interfaces).ToList();
        if (lines.Count > 0) return new(Name, false, string.Join(" ", lines));
        if (interfaces.Names.Count == 0) return null;
        IEnumerable<string> said =
        [
            .. interfaces.Commands.Count > 0 ? [$"runs {string.Join(", ", interfaces.Commands.Select(command => $"`{command.Name}`"))}"] : Array.Empty<string>(),
            .. interfaces.Storage is null ? [] : new[] { $"is handed its private storage at {PluginPrivate.Variable}" },
            .. interfaces.Views.Count > 0 ? [$"shows {string.Join(", ", interfaces.Views.Select(view => $"`{view.Id}` ({string.Join(", ", view.Where)})"))}"] : Array.Empty<string>(),
        ];
        return new(Name, true, $"{string.Join("; ", said)}, each as the driver reads it.");
    }
}
