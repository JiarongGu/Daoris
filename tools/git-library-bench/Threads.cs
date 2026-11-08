using System.Diagnostics;
using LibGit2Sharp;

namespace GitLibraryBench;

/// <summary>
/// A repository's open, its cost in time and memory, and Review read on many threads at once, each thread with a repository of its
/// own: libgit2 shares no object between threads, so a repository is one thread's at a time.
/// </summary>
internal static class Threads
{
    public static async Task RunAsync(Layout layout)
    {
        Report.Machine();
        var first = Stopwatch.StartNew();
        using (new Repository(layout.Review)) { }
        first.Stop();

        var opens = new List<double>();
        for (var index = 0; index < 20; index++)
        {
            var clock = Stopwatch.StartNew();
            using var repository = new Repository(layout.Review);
            _ = repository.Head.Tip.Sha;
            opens.Add(clock.Elapsed.TotalMilliseconds);
        }

        Report.Row("open a tree's repository and read its head", [first.Elapsed.TotalMilliseconds, .. opens]);

        GC.Collect();
        var before = Process.GetCurrentProcess().PrivateMemorySize64;
        var held = Enumerable.Range(0, 20).Select(_ => new Repository(layout.Review)).ToList();
        foreach (var repository in held) _ = repository.RetrieveStatus(new StatusOptions { IncludeUntracked = true }).Count();
        var after = Process.GetCurrentProcess().PrivateMemorySize64;
        Console.WriteLine($"  twenty repositories held, each having read its status: {(after - before) / 20 / 1024} KiB of private memory each");
        foreach (var repository in held) repository.Dispose();

        foreach (var width in new[] { 1, 4, 8 })
        {
            var clock = Stopwatch.StartNew();
            await Task.WhenAll(Enumerable.Range(0, width).Select(_ => Task.Run(() =>
            {
                using var repository = new Repository(layout.Review);
                ReviewReads.Library(repository, layout);
            })));
            Console.WriteLine($"  {width} Reviews at once, a repository each: {Report.Ms(clock.Elapsed.TotalMilliseconds)}");
        }
    }
}
