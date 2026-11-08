using LibGit2Sharp;

namespace GitLibraryBench;

/// <summary>
/// Which repositories the library opens and reads: each case made with git's binary, then its head, status, a file's content and the
/// last commit's diff asked of both. Where the library refuses an extension, the open is tried again with the extension allowed.
/// </summary>
internal static class Opens
{
    private sealed record Case(string Name, string Folder, string File, string? Extension);

    public static async Task RunAsync(GitBinary git, string scratch)
    {
        Report.Machine();
        var source = Path.Combine(scratch, "parity", "repo");
        if (!Directory.Exists(source)) await ParityFixture.MakeAsync(git, scratch);
        var top = Path.Combine(scratch, "opens");
        Fixtures.Delete(top);
        Directory.CreateDirectory(top);
        async Task<(int Code, string Output)> Git(string folder, params string[] arguments)
        {
            var (code, output, _) = await git.RunAsync(folder, arguments);
            return (code, output);
        }

        async Task<string> Fresh(string name, params string[] init)
        {
            var folder = Path.Combine(top, name);
            await Git(top, ["init", "-q", "-b", "main", .. init, folder]);
            await Git(folder, "config", "user.name", "Bench");
            await Git(folder, "config", "user.email", "bench@example.invalid");
            File.WriteAllText(Path.Combine(folder, "a.txt"), "one\n");
            await Git(folder, "add", "a.txt");
            await Git(folder, "commit", "-q", "-m", "one");
            File.WriteAllText(Path.Combine(folder, "a.txt"), "one\ntwo\n");
            await Git(folder, "commit", "-q", "-am", "two");
            return folder;
        }

        async Task<string> Clone(string name, params string[] options)
        {
            var folder = Path.Combine(top, name);
            await Git(top, ["clone", "-q", .. options, source, folder]);
            return folder;
        }

        var cases = new List<Case>
        {
            new("a full clone (the control)", await Clone("full", "--no-local"), "a.txt", null),
            new("a partial clone, blobs filtered out", await Clone("partial", "--no-local", "--filter=blob:none"), "a.txt", "partialclone"),
            new("a shallow clone", await Clone("shallow", "--no-local", "--depth", "1"), "a.txt", null),
            new("a clone sharing its objects (alternates)", await Clone("shared", "--shared"), "a.txt", null),
            new("reftable refs", await Fresh("reftable", "--ref-format=reftable"), "a.txt", "refstorage"),
            new("sha256 objects", await Fresh("sha256", "--object-format=sha256"), "a.txt", "objectformat"),
        };

        var unknown = await Fresh("unknown");
        await Git(unknown, "config", "core.repositoryformatversion", "1");
        await Git(unknown, "config", "extensions.benchunknown", "true");
        cases.Add(new("an unknown extension", unknown, "a.txt", "benchunknown"));

        var worktreeConfig = await Fresh("worktree-config");
        await Git(worktreeConfig, "config", "core.repositoryformatversion", "1");
        await Git(worktreeConfig, "config", "extensions.worktreeConfig", "true");
        cases.Add(new("per-worktree configuration (extensions.worktreeConfig)", worktreeConfig, "a.txt", "worktreeconfig"));

        var split = await Fresh("split-index");
        await Git(split, "update-index", "--split-index");
        cases.Add(new("a split index", split, "a.txt", null));

        var sparseIndex = await Clone("sparse-index", "--no-local");
        await Git(sparseIndex, "sparse-checkout", "set", "--cone", "--sparse-index", "dir");
        cases.Add(new("a sparse index", sparseIndex, "dir/keep.txt", "worktreeconfig"));

        var manyFiles = Path.Combine(top, "many-files");
        await Git(top, "-c", "feature.manyFiles=true", "clone", "-q", "--no-local", source, manyFiles);
        await Git(manyFiles, "config", "feature.manyFiles", "true");
        File.AppendAllText(Path.Combine(manyFiles, "a.txt"), "more\n");
        await Git(manyFiles, "add", "a.txt");
        cases.Add(new("feature.manyFiles (index v4, no index hash, untracked cache)", manyFiles, "a.txt", null));

        var graphs = await Clone("commit-graph", "--no-local");
        await Git(graphs, "commit-graph", "write", "--reachable");
        await Git(graphs, "repack", "-q", "-d", "--write-midx");
        cases.Add(new("a commit-graph and a multi-pack index", graphs, "a.txt", null));

        var dubious = await Clone("other-owner", "--no-local");
        var icacls = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("icacls", [dubious, "/setowner", "*S-1-5-18", "/T", "/Q"])
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true })!;
        await icacls.WaitForExitAsync();
        cases.Add(new($"another account's repository (setting the owner exited {icacls.ExitCode})", dubious, "a.txt", null));

        foreach (var each in cases) await ProbeAsync(git, each);
    }

    private static async Task ProbeAsync(GitBinary git, Case each)
    {
        // The library reads first: git fetches a partial clone's missing objects as it reads them, after which the library finds them.
        var library = Library(each.Folder, each.File);
        var head = await git.RunAsync(each.Folder, ["rev-parse", "HEAD"]);
        var status = await git.RunAsync(each.Folder, ["--no-optional-locks", "status", "--porcelain"]);
        var blob = await git.RunAsync(each.Folder, ["cat-file", "-p", $"HEAD:{each.File}"]);
        var diff = await git.RunAsync(each.Folder, ["diff", "--numstat", "HEAD~1", "HEAD"]);
        string Said(int code) => code == 0 ? "read" : $"exit {code}";
        Console.WriteLine($"{each.Name}:");
        Console.WriteLine($"  git: head {Said(head.Code)}, status {Said(status.Code)}, a file {Said(blob.Code)}, the last commit's diff {Said(diff.Code)}");
        Console.WriteLine($"  library: {library}");
        if (each.Extension is { } extension && !Opened(each.Folder))
        {
            GlobalSettings.SetExtensions(extension);
            Console.WriteLine($"  library, {extension} allowed: {Library(each.Folder, each.File)}");
            GlobalSettings.SetExtensions($"!{extension}");
        }
    }

    private static bool Opened(string folder)
    {
        try
        {
            using var repository = new Repository(folder);
            return true;
        }
        catch (LibGit2SharpException)
        {
            return false;
        }
    }

    private static string Library(string folder, string file)
    {
        var said = new List<string>();
        Repository repository;
        try
        {
            repository = new Repository(folder);
        }
        catch (Exception error) when (error is LibGit2SharpException or ArgumentException)
        {
            return $"cannot open: {error.Message}";
        }

        using (repository)
        {
            said.Add(Try("head", () => repository.Head.Tip?.Sha[..8] ?? "unborn"));
            said.Add(Try("status", () => $"{repository.RetrieveStatus(new StatusOptions { IncludeUntracked = true }).Count()} entries"));
            said.Add(Try("a file", () => $"{repository.Lookup<Blob>($"HEAD:{file}")?.Size.ToString() ?? "none"} bytes"));
            said.Add(Try("the last commit's diff", () =>
            {
                var tip = repository.Head.Tip;
                var parent = tip.Parents.FirstOrDefault();
                if (parent is null) return "no parent";
                using var patch = repository.Diff.Compare<Patch>(parent.Tree, tip.Tree);
                return $"{patch.Count()} files";
            }));
        }

        return string.Join(", ", said);
    }

    private static string Try(string name, Func<string> read)
    {
        try
        {
            return $"{name} {read()}";
        }
        catch (Exception error) when (error is LibGit2SharpException or NullReferenceException or InvalidOperationException)
        {
            return $"{name} fails ({error.GetType().Name}: {error.Message})";
        }
    }
}
