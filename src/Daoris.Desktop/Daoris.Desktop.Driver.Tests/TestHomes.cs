namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where a test's home or scratch tree is rooted: <c>_fixtures/test-homes</c> of the checkout, the gitignored scratch the
/// repository's no-OS-temp rule names, never the machine's temporary folder. A test names its own folder under it and removes
/// that folder when it ends; the root itself is created on first use. Linked into the modules' tests.
/// </summary>
internal static class TestHomes
{
    private static readonly Lazy<string> Folder = new(() =>
    {
        var root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "test-homes");
        Directory.CreateDirectory(root);
        return root;
    });

    /// <summary>The folder every test home lies under.</summary>
    public static string Root => Folder.Value;
}
