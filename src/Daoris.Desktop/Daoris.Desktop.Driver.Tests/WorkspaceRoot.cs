namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The workspace root, found by walking up from the test binaries to <c>daoris.json</c>: what a test reads there (a table the
/// CLI's suite reads too, a source, a scratch folder under <c>_fixtures/</c>) is found from here. Five classes had each written
/// the same walk (REFAC1, the second-opinion review of 2026-10-07).
/// </summary>
internal static class WorkspaceRoot
{
    private static readonly Lazy<string> Found = new(Walk);

    /// <summary>The folder that holds <c>daoris.json</c>, the nearest above the test binaries.</summary>
    /// <exception cref="InvalidOperationException">None above them: the tests run outside a checkout.</exception>
    public static string Folder => Found.Value;

    private static string Walk()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
    }
}
