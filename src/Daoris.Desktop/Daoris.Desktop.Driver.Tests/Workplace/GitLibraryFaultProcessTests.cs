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
        foreach (var word in new[] { "lease", Leases, repository }) info.ArgumentList.Add(word);
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

        Assert.Single(inside.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith("open\t", inside);
        var after = ReadLease().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, after.Length);
        Assert.StartsWith("close\t", after[1]);
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
        File.WriteAllText(Path.Combine(Leases, "999999-1.log"), "open\t1\tone\nopen\t2\ttwo\nclose\t1\nclose\t2\n");
        File.WriteAllText(Path.Combine(Leases, "999998-1.log"), "open\t1\tone\nopen\t2\ttwo%20spaced\nclose\t1\nopen\t3\tone\n");

        var left = GitLibraryLeases.Left(Leases);

        Assert.Equal(["one", "two spaced"], left.Order(StringComparer.Ordinal).ToArray());
        Assert.Empty(Directory.GetFiles(Leases, "*.log"));
    }

    [Fact]
    public void A_live_process_is_never_read_as_dead_inside_a_lease()
    {
        var leases = new GitLibraryLeases(Leases);
        var id = leases.Open("held");

        Assert.Empty(GitLibraryLeases.Left(Leases));

        leases.Close(id);
    }
}
