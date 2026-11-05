namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A fact that starts the application this project builds (<see cref="BuiltApplication"/>). The application is
/// Windows-only and this project references it only there, so elsewhere, where the suite runs headless, the fact is
/// skipped with that reason and never passes.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ApplicationFactAttribute : FactAttribute
{
    public ApplicationFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "the application is Windows-only, and this project builds it only there";
    }
}
