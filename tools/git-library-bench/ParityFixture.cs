namespace GitLibraryBench;

/// <summary>
/// The parity census's repository, made with git's binary: a line and a session branch whose commits rename with an edit, rename by
/// case alone, delete and add; attributes for eol, a filter the library cannot run, ident and binary; ignored files; and the trees
/// Daoris reads: a session's linked tree with every kind of uncommitted work, a detached one, and a sparse clone.
/// </summary>
internal sealed record ParityLayout(string Root, string Session, string Detached, string Sparse, string Base, string Line)
{
    public IEnumerable<(string Name, string Path)> Trees =>
        [("the line's checkout", Root), ("a session's linked tree", Session), ("a detached linked tree", Detached), ("a sparse clone", Sparse)];
}

internal static class ParityFixture
{
    public static async Task<ParityLayout> MakeAsync(GitBinary git, string scratch)
    {
        var top = Path.Combine(scratch, "parity");
        Fixtures.Delete(top);
        var root = Path.Combine(top, "repo");
        Directory.CreateDirectory(root);
        async Task<string> Git(string folder, params string[] arguments) => await Fixtures.MustAsync(git, folder, arguments);
        void Write(string folder, string path, string text)
        {
            var file = Path.Combine(folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text);
        }

        await Git(root, "init", "-q", "-b", "main");
        await Git(root, "config", "user.name", "Bench");
        await Git(root, "config", "user.email", "bench@example.invalid");
        await Git(root, "config", "uploadpack.allowFilter", "true");
        await Git(root, "config", "filter.upper.clean", "tr a-z A-Z");
        await Git(root, "config", "filter.upper.smudge", "tr A-Z a-z");
        Write(root, ".gitattributes", "*.crlf text eol=crlf\n*.lf text eol=lf\n*.up filter=upper\n*.id ident\n*.bin binary\n");
        Write(root, ".gitignore", "*.log\nbuild/\n");
        Write(root, "a.txt", Lines("a", 30));
        Write(root, "Readme.md", "# Read me\n\nWhat this is.\n");
        Write(root, "dir/moved.txt", Lines("moved", 60));
        Write(root, "dir/keep.txt", Lines("keep", 10));
        Write(root, "other/outside.txt", Lines("outside", 10));
        Write(root, "gone.txt", Lines("gone", 10));
        Write(root, "eol.crlf", "one\r\ntwo\r\n");
        Write(root, "eol.lf", "one\ntwo\n");
        Write(root, "data.up", "hello filter\n");
        Write(root, "stamp.id", "$Id$\n");
        File.WriteAllBytes(Path.Combine(root, "img.bin"), [0, 1, 2, 3, 0, 255, 10, 13]);
        await Git(root, "add", "-A");
        await Git(root, "commit", "-q", "-m", "the base");
        var baseCommit = (await Git(root, "rev-parse", "HEAD")).Trim();

        var session = Path.Combine(top, "session");
        await Git(root, "worktree", "add", "-q", "-b", "daoris/s-parity", session, baseCommit);
        await Git(session, "mv", "dir/moved.txt", "dir/renamed.txt");
        Write(session, "dir/renamed.txt", Lines("moved", 60).Replace("moved 7\n", "moved seven\n").Replace("moved 40\n", "moved forty\n"));
        await Git(session, "mv", "-f", "Readme.md", "README.md");
        await Git(session, "rm", "-q", "gone.txt");
        Write(session, "new.txt", Lines("new", 5));
        Write(session, "a.txt", Lines("a", 30).Replace("a 3\n", "a three\n"));
        Write(session, "data.up", "hello changed filter\n");
        await Git(session, "add", "-A");
        await Git(session, "commit", "-q", "-m", "the session's work");

        Write(root, "a.txt", Lines("a", 30).Replace("a 25\n", "a twenty-five\n"));
        await Git(root, "commit", "-q", "-am", "the line moves on");
        var line = (await Git(root, "rev-parse", "HEAD")).Trim();

        Write(session, "a.txt", Lines("a", 30).Replace("a 3\n", "a three\n") + "uncommitted\n");
        Write(session, "staged.txt", "staged\n");
        await Git(session, "add", "staged.txt");
        Write(session, "notes/deep/one.txt", "an untracked note\n");
        Write(session, "notes/two.txt", "another\r\nwith crlf\r\n");
        File.WriteAllBytes(Path.Combine(session, "notes", "blob.dat"), [0, 0, 1, 2]);
        Write(session, "debug.log", "ignored\n");
        Write(session, "build/out.txt", "ignored too\n");
        File.Delete(Path.Combine(session, "eol.lf"));
        Write(session, "eol.crlf", "one\r\ntwo\r\n");
        await Task.Delay(1100);
        Write(session, "data.up", "hello changed filter\n");
        File.Move(Path.Combine(session, "new.txt"), Path.Combine(session, "New.txt"));

        var detached = Path.Combine(top, "detached");
        await Git(root, "worktree", "add", "-q", "--detach", detached, baseCommit);

        var sparse = Path.Combine(top, "sparse");
        await Git(top, "clone", "-q", "--no-local", root, sparse);
        await Git(sparse, "sparse-checkout", "set", "--cone", "dir");

        return new ParityLayout(Path.GetFullPath(root), Path.GetFullPath(session), Path.GetFullPath(detached), Path.GetFullPath(sparse), baseCommit, line);
    }

    private static string Lines(string word, int count) => string.Concat(Enumerable.Range(0, count).Select(n => $"{word} {n}\n"));
}
