using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>What the git domains share in answering: the checkout of a repository, the sentence without its path, and an act's run as the page reads it.</summary>
internal static class GitAnswers
{
    public static object NoCheckout(string repository) => new
    {
        Refused = true, Code = Refusals.GitNoCheckout, Said = $"{repository} has no checkout on this machine, so nothing was changed.",
    };

    /// <summary>An act's run as the page reads it: the record, its outcome, and the words with the checkout's path said as the repository (D47 §4).</summary>
    public static object Answered(ActRun run, string checkout, string repository) => new
    {
        run.Act, run.Outcome, run.Refused, Said = WithoutRoots(run.Said, [checkout], repository) ?? "",
    };

    /// <summary>The first checkout of the repository that exists on this machine, or null.</summary>
    public static async Task<string?> CheckoutOfAsync(DriverLoop loop, string repository, string? workspace, CancellationToken cancellationToken)
    {
        var service = loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, workspace);
        return rows.FirstOrDefault(row => Directory.Exists(row.Root)).Root;
    }

    /// <summary>
    /// A read of the repository the payload names, answered as the driver answered it: its value, or a code the page words and
    /// the sentence beside it, and the git commands that give the same answer. A repository with no checkout here is an answer
    /// too, so the page words it as it words the rest. Writes nothing.
    /// </summary>
    public static async Task<object?> GitAnsweredAsync<T>(
        DriverLoop loop, string? named, string? workspace, CancellationToken cancellationToken, Func<string, CancellationToken, Task<GitAnswer<T>>> read) where T : class
    {
        var repository = named ?? throw new DriverException("a Git read names its repository.");
        var service = loop.Service ?? throw NotReady();
        var rows = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, workspace);
        if (rows.FirstOrDefault(row => Directory.Exists(row.Root)).Root is not { } root)
        {
            return new
            {
                Value = (T?)null, Refusal = Refusals.GitNoCheckout,
                Problem = $"{repository} has no checkout on this machine, so there is no Git history here to read.",
                Commands = Array.Empty<string>(), Remembered = false,
            };
        }

        var answer = await read(root, cancellationToken).ConfigureAwait(false);
        return new
        {
            answer.Value,
            Refusal = GitRefusalCode(answer.Refusal),
            Problem = WithoutRoots(answer.Problem, [root], repository),
            answer.Commands,
            answer.Remembered,
        };
    }

    /// <summary>The code the page words for a refusal the driver gave, or null where it answered (D147).</summary>
    private static string? GitRefusalCode(GitReadRefusal refusal) => refusal switch
    {
        GitReadRefusal.None => null,
        GitReadRefusal.NotTop => Refusals.GitNotTop,
        GitReadRefusal.NotACommit => Refusals.GitNotACommit,
        GitReadRefusal.NotAPath => Refusals.GitNotAPath,
        GitReadRefusal.NotAPage => Refusals.GitNotAPage,
        GitReadRefusal.TooManyTips => Refusals.GitTooManyTips,
        _ => Refusals.GitNotAnswered,
    };

    /// <summary>
    /// 🔴 A sentence with the checkout's path said as the repository (D47 §4): the driver's sentences name the root they were
    /// asked of, in either slash, and the machine's path is the desktop's alone.
    /// </summary>
    public static string? WithoutRoots(string? problem, IEnumerable<string?> roots, string repository)
    {
        if (problem is null) return null;
        foreach (var root in roots.OfType<string>().Where(root => root.Length > 0))
        {
            var full = Path.GetFullPath(root);
            foreach (var form in new[] { full, root, full.Replace('\\', '/'), root.Replace('\\', '/'), full.Replace('/', '\\'), root.Replace('/', '\\') }
                         .Distinct(StringComparer.Ordinal).OrderByDescending(form => form.Length))
            {
                problem = problem.Replace(form, $"{repository}'s checkout", StringComparison.OrdinalIgnoreCase);
            }
        }

        return problem;
    }
}
