using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The library's hold on a repository's files lasts one call, on Windows as anywhere: with the refs listed through the library
/// every ten milliseconds, a person's <c>repack -ad</c>, <c>gc --prune=now</c>, <c>commit-graph write</c> and <c>multi-pack-index
/// write</c> each exit 0, leave no old pack behind and never ask to try a delete again. The stand-in git asks through
/// (<c>GIT_ASK_YESNO</c>) is first seen asked, where a file is held open without sharing its delete, so its silence means something.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryHoldTests : LandedFixture
{
    private async Task<(string Root, string Marker, Dictionary<string, string> Asking)> PackedAsync()
    {
        var root = await BuildRepositoryAsync("held");
        for (var at = 0; at < 6; at++)
        {
            await CommitAsync(root, $"file{at}.txt", $"{at}\n", $"commit {at}");
            await GitAsync(root, "branch", $"b{at}");
            await GitAsync(root, "repack", "-d", "-q");
        }

        var marker = Path.Combine(Scratch, "asked.txt");
        var script = Path.Combine(Scratch, "ask-yesno");
        await File.WriteAllTextAsync(script, $"#!/bin/sh\necho \"$1\" >> '{marker.Replace('\\', '/')}'\nexit 1\n");
        return (root, marker, new Dictionary<string, string> { ["GIT_ASK_YESNO"] = script.Replace('\\', '/') });
    }

    private static async Task<(int Code, string Error)> RunAsync(string cwd, IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        var info = new ProcessStartInfo { FileName = "git", WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var (name, value) in environment) info.Environment[name] = value;
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        return (process.ExitCode, stderr.Result);
    }

    private static string[] Packs(string root) => Directory.GetFiles(Path.Combine(root, ".git", "objects", "pack"), "pack-*.*");

    [Fact]
    public async Task The_stand_in_is_asked_where_a_pack_is_held_without_sharing_its_delete()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, marker, asking) = await PackedAsync();
        var held = Packs(root).First(path => path.EndsWith(".pack", StringComparison.Ordinal));

        using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await RunAsync(root, asking, "repack", "-a", "-d", "-q");
        }

        Assert.True(File.Exists(marker), "git never asked the stand-in where a pack was held: its silence below would prove nothing");
    }

    [Fact]
    public async Task Housekeeping_under_library_reads_every_ten_milliseconds_exits_0_and_never_asks()
    {
        var (root, marker, asking) = await PackedAsync();
        var before = Packs(root);
        Assert.True(before.Length >= 6, string.Join(", ", before));

        using var stop = new CancellationTokenSource();
        int answered = 0, begun = 0, inCall = 0;
        var reads = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                Interlocked.Increment(ref begun);
                Volatile.Write(ref inCall, 1);
                var listing = await GitLibraryRefs.ListingAsync(root, CancellationToken.None);
                Volatile.Write(ref inCall, 0);
                if (listing is not null) Interlocked.Increment(ref answered);
                await Task.Delay(10);
            }
        });

        while (Volatile.Read(ref answered) == 0 && Volatile.Read(ref begun) < 50) await Task.Delay(10);
        var overlapped = new List<bool>();
        var codes = new List<(string Command, int Code, string Error)>();
        foreach (var command in new[]
                 {
                     new[] { "repack", "-a", "-d", "-q" },
                     ["gc", "--prune=now", "--quiet"],
                     ["commit-graph", "write", "--reachable"],
                     ["multi-pack-index", "write"],
                     ["repack", "-a", "-d", "-q", "--write-midx"],
                 })
        {
            var began = Volatile.Read(ref begun);
            var (code, error) = await RunAsync(root, asking, command);
            overlapped.Add(Volatile.Read(ref begun) > began || Volatile.Read(ref inCall) == 1);
            codes.Add((string.Join(' ', command), code, error));
        }

        stop.Cancel();
        await reads;

        Assert.All(codes, each => Assert.True(each.Code == 0, $"{each.Command} exited {each.Code}: {each.Error}{(File.Exists(marker) ? " (git asked: " + File.ReadAllText(marker).Trim() + ")" : "")}"));
        Assert.False(File.Exists(marker), File.Exists(marker) ? "git asked: " + await File.ReadAllTextAsync(marker) : "");
        Assert.Empty(before.Where(File.Exists));
        var sentToGit = string.Join(", ", GitLibraryJournal.Of(root)?.Reasons.Select(each => $"{each.Key} {each.Value}") ?? []);
        Assert.True(answered > 0, $"the library answered no read; they went to git: {sentToGit}");
        Assert.All(overlapped, each => Assert.True(each, $"a command ran with no library read beside it: {answered} answered of {begun}; the others went to git: {sentToGit}"));
        Assert.Equal(0, GitLibrary.Open);
    }
}
