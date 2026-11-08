using System.Diagnostics;
using LibGit2Sharp;
using GitLibraryBench;

// The git library's bench, run from the repository's root; everything it makes is under --scratch (default _fixtures/gitlib).
//
//   starts [--count 20]                         a start's cost: process creation against git's own work, idle and loaded
//   setup --name <n> --source <repo> [--line main] [--trees 6]
//                                               a clone laid out as Daoris's trees are (a path, or a public address)
//   reads --name <n> [--passes 5]               Review's and the look's reads, through the binary and in process, cold and warm
//   threads --name <n>                          a repository's open cost and memory, and reads on many threads
//   parity                                      each library answer beside git's, on the cases the census names
//   opens                                       which repositories the library opens and reads, beside git
var arguments = args.ToList();
string Option(string name, string fallback)
{
    var at = arguments.IndexOf($"--{name}");
    return at >= 0 && at + 1 < arguments.Count ? arguments[at + 1] : fallback;
}

var scratch = Path.GetFullPath(Option("scratch", Path.Combine("_fixtures", "gitlib")));
Directory.CreateDirectory(scratch);
var git = GitBinary.Resolve(arguments.Contains("--git") ? Option("git", "") : null);
Console.WriteLine($"git: {(await git.RunAsync(scratch, ["--version"])).Output.Trim()} ({git.File})");

switch (arguments.FirstOrDefault())
{
    case "starts":
        await StartCost.RunAsync(git, Directory.GetCurrentDirectory(), scratch, int.Parse(Option("count", "20")));
        break;
    case "setup":
        var made = await Fixtures.SetUpAsync(git, scratch, Option("name", "repo"), Option("source", "."), Option("line", "main"), int.Parse(Option("trees", "6")));
        Console.WriteLine($"laid out {made.Root}: line {made.Line}, base {made.Base}, {made.Trees.Count} trees");
        break;
    case "reads":
        await ReadsAsync(git, Layout.Read(scratch, Option("name", "repo")), int.Parse(Option("passes", "5")));
        break;
    case "threads":
        await Threads.RunAsync(Layout.Read(scratch, Option("name", "repo")));
        break;
    case "parity":
        await Parity.RunAsync(git, scratch);
        break;
    case "opens":
        await Opens.RunAsync(git, scratch);
        break;
    default:
        Console.Error.WriteLine("usage: git-library-bench starts|setup|reads|threads|parity|opens [options]");
        return 2;
}

return 0;

static async Task ReadsAsync(GitBinary git, Layout layout, int passes)
{
    Report.Machine();
    Console.WriteLine($"{layout.Root}: {layout.Trees.Count} trees, Review of {layout.Review}");

    var open = Stopwatch.StartNew();
    var cold = new Repository(layout.Review);
    open.Stop();
    var coldReview = ReviewReads.Library(cold, layout);
    cold.Dispose();
    var coldLook = LookOnce(layout);

    var binaryReview = new List<List<(string Ask, double Ms)>>();
    var binaryLook = new List<(int Starts, double Ms)>();
    for (var pass = 0; pass < passes; pass++)
    {
        binaryReview.Add(await ReviewReads.BinaryAsync(git, layout));
        binaryLook.Add(await LookReads.BinaryAsync(git, layout));
    }

    var reopened = new List<double>();
    var reopenedLook = new List<double>();
    for (var pass = 0; pass < passes; pass++)
    {
        var clock = Stopwatch.StartNew();
        using (var repository = new Repository(layout.Review)) ReviewReads.Library(repository, layout);
        reopened.Add(clock.Elapsed.TotalMilliseconds);
        reopenedLook.Add(LookOnce(layout));
    }

    var held = new List<List<(string Ask, double Ms)>>();
    var heldLook = new List<double>();
    using (var repository = new Repository(layout.Review))
    {
        var trees = new Dictionary<string, Repository>(StringComparer.OrdinalIgnoreCase);
        Repository Held(string path) => trees.TryGetValue(path, out var kept) ? kept : trees[path] = new Repository(path);
        for (var pass = 0; pass < passes + 1; pass++)
        {
            held.Add(ReviewReads.Library(repository, layout));
            heldLook.Add(LookReads.Library(layout, Held));
        }

        foreach (var kept in trees.Values) kept.Dispose();
    }

    Console.WriteLine($"Review, binary: {binaryReview[0].Count} starts");
    Report.Row("Review through the binary, one start at a time", [.. binaryReview.Select(each => each.Sum(ask => ask.Ms))]);
    Report.Row("Review in process, cold (first in the process, open included)", [open.Elapsed.TotalMilliseconds + coldReview.Sum(ask => ask.Ms)],
        $"open {Report.Ms(open.Elapsed.TotalMilliseconds)}");
    Report.Row("Review in process, opened for each Review", reopened);
    Report.Row("Review in process, repository held", [.. held.Skip(1).Select(each => each.Sum(ask => ask.Ms))]);
    foreach (var name in binaryReview[0].Select(each => each.Ask).Distinct())
    {
        var binary = binaryReview.Select(each => each.Where(ask => ask.Ask == name).Sum(ask => ask.Ms)).ToArray();
        var library = held.Skip(1).Select(each => each.Where(ask => ask.Ask == name || (name.Contains("untracked") && ask.Ask.Contains("untracked"))
            || (name.EndsWith("the line", StringComparison.Ordinal) && name != "merge-base with the line" && ask.Ask == "ahead and behind the line")).Sum(ask => ask.Ms)).ToArray();
        Console.WriteLine($"    {name}: binary {Report.Ms(Report.Median(binary))}, held {Report.Ms(Report.Median(library))}");
    }

    Console.WriteLine($"The look, binary: {binaryLook[0].Starts} starts");
    Report.Row("the look through the binary", [.. binaryLook.Select(each => each.Ms)]);
    Report.Row("the look in process, cold", [coldLook]);
    Report.Row("the look in process, opened for each look", reopenedLook);
    Report.Row("the look in process, repositories held", [.. heldLook.Skip(1)]);
    foreach (var failure in ReviewReads.Failures) Console.WriteLine($"  the library refused: {failure}");
}

static double LookOnce(Layout layout)
{
    var opened = new List<Repository>();
    var ms = LookReads.Library(layout, path =>
    {
        var repository = new Repository(path);
        opened.Add(repository);
        return repository;
    });
    foreach (var repository in opened) repository.Dispose();
    return ms;
}
