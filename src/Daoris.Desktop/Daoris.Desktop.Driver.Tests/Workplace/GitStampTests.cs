using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The stamp's files, named by git: after each change, <c>git config --list --show-origin --show-scope --includes</c> asked in the
/// folder as Daoris starts git names no file the stamp does not, and every file created, changed or removed moves the stamp. The person's
/// home, XDG folder and <c>ProgramData</c> are the fixture's, so nothing of the machine's is read or written.
/// </summary>
public abstract class GitStampCases : LandedFixture
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    /// <summary>Whether the git asked is the MinGit the toolchain pins rather than the runner's.</summary>
    protected virtual bool Pinned => false;

    private static readonly string[] Isolated = ["HOME", "PROGRAMDATA", "XDG_CONFIG_HOME", "GIT_CONFIG_GLOBAL", "GIT_CONFIG_SYSTEM", "GIT_CONFIG_NOSYSTEM"];

    private string? Variable(string name) => _variables.GetValueOrDefault(name);

    private async Task<GitStamp> StampAsync(string folder)
    {
        var stamp = await GitStamp.OfAsync(folder, CancellationToken.None, Variable);
        Assert.NotNull(stamp);
        await NamesNoOtherFileAsync(folder, stamp);
        return stamp;
    }

    /// <summary>Red where git, started as Daoris starts it with the same variables, names a file the stamp does not.</summary>
    private async Task NamesNoOtherFileAsync(string folder, GitStamp stamp)
    {
        var (info, refusal) = GitRuns.GitStart(folder, ["config", "--list", "--show-origin", "--show-scope", "--includes"], ProgramHome.Named);
        Assert.True(info is not null, refusal);
        foreach (var name in Isolated) info.Environment.Remove(name);
        foreach (var (name, value) in _variables) info.Environment[name] = value;
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await stderr);

        var stamped = stamp.Files.Select(Full).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var named = (await stdout).Split('\n')
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length >= 3 && parts[1].StartsWith("file:", StringComparison.Ordinal))
            // git names the repository's own files relative to the folder that asks.
            .Select(parts => Path.GetFullPath(parts[1]["file:".Length..].Trim('"').Replace('/', Path.DirectorySeparatorChar), folder))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var missed = named.Where(file => !stamped.Contains(file)).ToList();
        Assert.True(missed.Count == 0, $"git names {string.Join(", ", missed)}; the stamp holds {string.Join(", ", stamped)}");
    }

    private static string Full(string path) => Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar));

    private async Task<GitStamp> MovedAsync(string folder, GitStamp before, string what)
    {
        var after = await StampAsync(folder);
        Assert.True(after.Digest != before.Digest, $"{what} did not move the stamp");
        return after;
    }

    /// <summary>One letter of the last key's name in another case, so the file is one length and still read as the same keys.</summary>
    private static void RewrittenInsideTheGrain(string path)
    {
        var written = File.GetLastWriteTimeUtc(path);
        var text = File.ReadAllText(path);
        var line = text.TrimEnd('\n').LastIndexOf('\n') + 1;
        var at = Enumerable.Range(line, text.Length - line).First(index => char.IsAsciiLetter(text[index]));
        var flipped = char.IsUpper(text[at]) ? char.ToLowerInvariant(text[at]) : char.ToUpperInvariant(text[at]);
        File.WriteAllText(path, text[..at] + flipped + text[(at + 1)..]);
        File.SetLastWriteTimeUtc(path, written);
    }

    [Fact]
    public async Task Every_file_git_names_is_stamped_and_each_planted_change_moves_the_stamp()
    {
        string? named = ProgramHome.Named;
        if (Pinned)
        {
            PinnedMinGit.Lay(Home);
            ProgramHome.Named = Home;
        }

        try
        {
            var root = await RepositoryAsync("engine");
            var git = RefFiles.CommonDirOf(root)!;
            _variables["HOME"] = Directory.CreateDirectory(Path.Combine(Scratch, "person")).FullName;
            _variables["PROGRAMDATA"] = Directory.CreateDirectory(Path.Combine(Scratch, "programdata")).FullName;
            var stamp = await StampAsync(root);

            Directory.CreateDirectory(Path.Combine(Scratch, "programdata", "Git"));
            await File.WriteAllTextAsync(Path.Combine(Scratch, "programdata", "Git", "config"), "[planted]\n\tprogramdata = 1\n");
            stamp = await MovedAsync(root, stamp, "the ProgramData file created");

            await File.WriteAllTextAsync(Path.Combine(Scratch, "person", ".gitconfig"), "[planted]\n\tglobal = 1\n");
            stamp = await MovedAsync(root, stamp, "the person's global file created");

            _variables["GIT_CONFIG_SYSTEM"] = Path.Combine(Scratch, "system.gitconfig");
            stamp = await StampAsync(root);
            await File.WriteAllTextAsync(Path.Combine(Scratch, "system.gitconfig"), "[planted]\n\tsystem = 1\n");
            stamp = await MovedAsync(root, stamp, "the system file under GIT_CONFIG_SYSTEM created");

            await File.AppendAllTextAsync(Path.Combine(git, "config"), "[include]\n\tpath = extra.inc\n");
            stamp = await MovedAsync(root, stamp, "an include added");
            await File.WriteAllTextAsync(Path.Combine(git, "extra.inc"), "[include]\n\tpath = deeper.inc\n[planted]\n\textra = 1\n");
            stamp = await MovedAsync(root, stamp, "an include target absent at the first stamp, then created");
            await File.WriteAllTextAsync(Path.Combine(git, "deeper.inc"), "[planted]\n\tdeeper = 1\n");
            stamp = await MovedAsync(root, stamp, "an include inside that include created");

            foreach (var file in stamp.Files.Where(file => file.StartsWith(Full(Scratch), StringComparison.OrdinalIgnoreCase) && File.Exists(file)))
            {
                // Written, stamped at once, then written again within the same write time: two writes inside the grain.
                await File.WriteAllBytesAsync(file, await File.ReadAllBytesAsync(file));
                stamp = await StampAsync(root);
                RewrittenInsideTheGrain(file);
                stamp = await MovedAsync(root, stamp, $"a same-length rewrite of {Path.GetFileName(file)} inside the clock's grain");
            }

            File.Delete(Path.Combine(git, "deeper.inc"));
            stamp = await MovedAsync(root, stamp, "an include removed");

            await File.AppendAllTextAsync(Path.Combine(git, "config"), "[includeIf \"gitdir:**/later/\"]\n\tpath = later.inc\n");
            await File.WriteAllTextAsync(Path.Combine(git, "later.inc"), "[planted]\n\tlater = 1\n");
            stamp = await MovedAsync(root, stamp, "a conditional include added");
            var later = Path.Combine(Scratch, "later");
            await GitAsync(root, "worktree", "add", "-q", "--detach", later, "main");
            var inLater = await StampAsync(later);
            Assert.NotEqual(stamp.Digest, inLater.Digest);
            Assert.False(GitStamp.SameContext(root, later));
        }
        finally
        {
            ProgramHome.Named = named;
        }
    }
}

/// <summary>The stamp's files named by the runner's git.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitStampTests : GitStampCases;

/// <summary>The stamp's files named by the MinGit the toolchain pins (<see cref="PinnedMinGit"/>).</summary>
/// <remarks>Alone: it names the pinned git's home as the program's (<see cref="ProgramHome.Named"/>), which every class beside it reads.</remarks>
[Trait(Category.Name, Category.Process)]
[Collection(Alone.Name)]
public sealed class GitStampPinnedTests : GitStampCases
{
    protected override bool Pinned => true;
}
