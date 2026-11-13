using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The library's native-fault guard: libgit2 runs in the desktop's own process, so a fault there ends the
/// process and .NET cannot catch it. Each lease's open and close is appended to a file of the home's run folder where the process's
/// death keeps it, and the next start refuses a repository a dead process ended inside from the library, until the package changes.
/// The process that dies is the owner stand-in in its <c>lease</c> mode, started and ended by the test, never the installed Daoris.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryFaultProcessTests : LandedFixture
{
    private string Leases => DaorisHome.Entry(Home, HomeEntry.GitLibraryLeases);

    /// <summary>The stand-in opens a lease on <paramref name="repository"/>, says it is inside, and is killed there.</summary>
    private async Task KillInsideAsync(string repository)
    {
        var info = new ProcessStartInfo(OwnedProcessesProcessTests.OwnerStandIn())
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var word in new[] { "lease", Leases, repository, GitLibraryFaults.Package }) info.ArgumentList.Add(word);
        using var host = Process.Start(info)!;
        try
        {
            Assert.Equal("inside", await host.StandardOutput.ReadLineAsync().WaitAsync(Poll.Patience));
            host.Kill(entireProcessTree: false);
            await host.WaitForExitAsync().WaitAsync(Poll.Patience);
        }
        finally
        {
            if (!host.HasExited) host.Kill(entireProcessTree: true);
        }
    }

    private async Task<bool> LibraryAnswersAsync(string root)
    {
        var before = ProgramHome.Named;
        ProgramHome.Named = Home;
        try
        {
            return await GitLibrary.AskAsync(root, GitLibraryQuestion.Walks, call => call.Repository.Head.CanonicalName, CancellationToken.None) is not null;
        }
        finally
        {
            ProgramHome.Named = before;
        }
    }

    [Fact]
    public async Task A_repository_a_process_died_inside_a_lease_of_is_the_gits_at_the_next_start_and_the_page_says_why()
    {
        if (!OperatingSystem.IsWindows()) return;
        var broken = await BuildRepositoryAsync("fault-broken");
        var sound = await BuildRepositoryAsync("fault-sound");
        await KillInsideAsync(broken);

        Assert.False(await LibraryAnswersAsync(broken));
        Assert.True(await LibraryAnswersAsync(sound));

        var row = GitLibraryJournal.Of(broken)!;
        Assert.Equal(0, row.Library);
        Assert.True(row.Reasons.ContainsKey(GitLibraryJournal.NativeFault));
        Assert.DoesNotContain(GitLibraryJournal.NativeFault, GitLibraryJournal.Of(sound)!.Reasons.Keys);
        var listed = JsonSerializer.SerializeToElement(GitLibraryJournal.Page(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var shown = listed.EnumerateArray().Single(each => each.GetProperty("repository").GetString() == "fault-broken");
        Assert.Contains(shown.GetProperty("reasons").EnumerateArray(), each => each.GetProperty("reason").GetString() == GitLibraryJournal.NativeFault);
    }

    [Fact]
    public async Task The_refusal_lapses_when_the_package_moves_and_comes_back_when_it_returns()
    {
        if (!OperatingSystem.IsWindows()) return;
        var broken = await BuildRepositoryAsync("fault-lapsing");
        await KillInsideAsync(broken);
        Assert.False(await LibraryAnswersAsync(broken));

        var package = GitLibraryFaults.Package;
        GitLibraryFaults.Package = package + "-next";
        try
        {
            Assert.True(await LibraryAnswersAsync(broken));
        }
        finally
        {
            GitLibraryFaults.Package = package;
        }

        Assert.False(await LibraryAnswersAsync(broken));
    }

    [Fact]
    public async Task A_call_is_opened_in_the_file_before_the_library_is_entered_and_closed_after_it_returns()
    {
        var root = await BuildRepositoryAsync("fault-lease");
        var inside = "";
        var before = ProgramHome.Named;
        ProgramHome.Named = Home;
        try
        {
            Assert.NotNull(await GitLibrary.AskAsync(root, GitLibraryQuestion.Walks, call =>
            {
                inside = ReadLease();
                return call.Repository.Head.CanonicalName;
            }, CancellationToken.None));
        }
        finally
        {
            ProgramHome.Named = before;
        }

        var opened = inside.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal($"package\t{Uri.EscapeDataString(GitLibraryFaults.Package)}", opened[0]);
        var own = opened.Select(line => line.Split('\t')).Single(parts => parts is ["open", _, var repository] && Uri.UnescapeDataString(repository) == root);
        var after = ReadLease().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var closed = $"close\t{own[1]}";
        Assert.DoesNotContain(closed, opened);
        Assert.Contains(closed, after);
    }

    private string ReadLease()
    {
        using var stream = new FileStream(Directory.GetFiles(Leases, "*.log").Single(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return new StreamReader(stream).ReadToEnd();
    }

    [Fact]
    public void A_dead_process_that_closed_what_it_opened_left_nothing_and_one_that_did_not_left_its_repository()
    {
        Directory.CreateDirectory(Leases);
        File.WriteAllText(Path.Combine(Leases, "999999-1.log"), "package\tp1\nopen\t1\tone\nopen\t2\ttwo\nclose\t1\nclose\t2\n");
        File.WriteAllText(Path.Combine(Leases, "999998-1.log"), "package\tp1\nopen\t1\tone\nopen\t2\ttwo%20spaced\nclose\t1\nopen\t3\tone\n");

        var left = GitLibraryLeases.Left(Leases);

        Assert.Equal(["one", "two spaced"], left.Select(each => each.Repository).Order(StringComparer.Ordinal).ToArray());
        Assert.Empty(Directory.GetFiles(Leases, "*.log"));
    }

    [Fact]
    public void A_dead_process_leaves_its_repository_under_the_package_its_file_names()
    {
        Directory.CreateDirectory(Leases);
        File.WriteAllText(Path.Combine(Leases, "999997-1.log"), "package\told%20build\nopen\t1\tone\n");

        Assert.Equal([("one", "old build")], GitLibraryLeases.Left(Leases).ToArray());
    }

    [Fact]
    public async Task A_fault_found_after_an_update_is_journalled_under_the_package_that_faulted()
    {
        var broken = await BuildRepositoryAsync("fault-older-package");
        Directory.CreateDirectory(Leases);
        File.WriteAllText(Path.Combine(Leases, "999996-1.log"), $"package\tolder-build\nopen\t1\t{Uri.EscapeDataString(broken)}\n");

        var before = ProgramHome.Named;
        ProgramHome.Named = Home;
        try
        {
            Assert.Null(GitLibraryFaults.RefusalOf(broken));
        }
        finally
        {
            ProgramHome.Named = before;
        }

        Assert.Equal("older-build", GitLibraryJournal.FaultOf(broken));
    }

    [Fact]
    public async Task Every_question_of_a_start_finds_the_faults_the_first_one_is_still_reading()
    {
        var broken = await BuildRepositoryAsync("fault-settling");
        Directory.CreateDirectory(Leases);
        var busy = string.Concat(Enumerable.Range(0, 4000).Select(at => $"open\t{at}\tsound\nclose\t{at}\n"));
        for (var at = 0; at < 60; at++) File.WriteAllText(Path.Combine(Leases, $"{999000 + at}-1.log"), busy);
        File.WriteAllText(Path.Combine(Leases, "999999-1.log"), $"package\t{Uri.EscapeDataString(GitLibraryFaults.Package)}\nopen\t1\t{Uri.EscapeDataString(broken)}\n");
        var before = ProgramHome.Named;
        ProgramHome.Named = Home;
        try
        {
            using var go = new ManualResetEventSlim();
            var asks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                go.Wait();
                return GitLibraryFaults.RefusalOf(broken);
            })).ToList();
            go.Set();

            Assert.All(await Task.WhenAll(asks).WaitAsync(Poll.Patience), word => Assert.Equal(GitLibraryJournal.NativeFault, word));
        }
        finally
        {
            ProgramHome.Named = before;
        }
    }

    [Fact]
    public void An_orderly_exit_closes_the_leases_a_call_on_the_pool_still_holds()
    {
        var before = ProgramHome.Named;
        ProgramHome.Named = Home;
        try
        {
            Assert.NotNull(GitLibraryFaults.Enter("held-at-exit"));
            GitLibraryFaults.Exit();
        }
        finally
        {
            ProgramHome.Named = before;
        }

        var lines = ReadLease().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("open\t", lines[1]);
        Assert.StartsWith("close\t", lines[^1]);
    }

    [Fact]
    public void A_live_process_is_never_read_as_dead_inside_a_lease()
    {
        var leases = new GitLibraryLeases(Leases, "p1");
        var id = leases.Open("held");

        Assert.Empty(GitLibraryLeases.Left(Leases));

        leases.Close(id);
    }
}
