namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A checkout of no commits, written as files with no git start: a folder holding the <c>.git</c> git needs to take it as a
/// repository (its <c>HEAD</c>, <c>objects</c>, <c>refs</c> and a config the test names). For a test that needs only that git reads a
/// place's config or says the place has no commit, where <c>init</c> and <c>remote add</c> are starts that take a second each on a
/// busy machine; and a plain folder under the checkout's scratch is no such place, git there answering for the whole checkout around it.
/// </summary>
internal static class EmptyCheckout
{
    /// <summary>The checkout at <paramref name="root"/>, its config holding <paramref name="config"/> after the format line.</summary>
    public static string Make(string root, string config = "")
    {
        var git = Path.Combine(root, ".git");
        Directory.CreateDirectory(Path.Combine(git, "objects"));
        Directory.CreateDirectory(Path.Combine(git, "refs", "heads"));
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(Path.Combine(git, "config"), "[core]\n\trepositoryformatversion = 0\n" + config);
        return root;
    }
}
