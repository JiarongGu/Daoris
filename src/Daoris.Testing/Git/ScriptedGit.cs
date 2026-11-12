using System.Runtime.CompilerServices;
using System.Text;
using Daoris.Ports.Git;

namespace Daoris.Testing;

/// <summary>
/// An <see cref="IGit"/> that answers each ask by a predicate on it, as a test scripts git: the first row whose predicate holds
/// answers, and an ask no row answers throws, naming it, so a start nobody scripted is a failure and never a silent success.
/// </summary>
public sealed class ScriptedGit : IGit
{
    private readonly List<(Func<GitAsk, bool> When, Func<GitAsk, GitAnswer> Answer)> _rows = [];
    private readonly List<GitAsk> _asked = [];
    private readonly Lock _gate = new();

    /// <summary>Every ask made so far, in order.</summary>
    public IReadOnlyList<GitAsk> Asked
    {
        get
        {
            lock (_gate) return [.. _asked];
        }
    }

    /// <summary>Answers an ask whose arguments, joined by a space, are exactly <paramref name="arguments"/>.</summary>
    public ScriptedGit On(string arguments, GitAnswer answer) =>
        On(ask => string.Join(' ', ask.Arguments) == arguments, _ => answer);

    /// <summary>Answers each ask <paramref name="when"/> holds for with what <paramref name="answer"/> makes of it.</summary>
    public ScriptedGit On(Func<GitAsk, bool> when, Func<GitAsk, GitAnswer> answer)
    {
        lock (_gate) _rows.Add((when, answer));
        return this;
    }

    public Task<GitAnswer> RunAsync(GitAsk ask, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Answer(ask));
    }

    public Task<GitBytes> ReadBytesAsync(GitAsk ask, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var answer = Answer(ask);
        return Task.FromResult(new GitBytes(answer.Exit, Encoding.UTF8.GetBytes(answer.Output), answer.Error));
    }

    public async IAsyncEnumerable<string> ReadLinesAsync(GitAsk ask, [EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var answer = Answer(ask);
        using var reader = new StringReader(answer.Output);
        while (reader.ReadLine() is { } line)
        {
            ct.ThrowIfCancellationRequested();
            yield return line;
        }

        await Task.CompletedTask;
    }

    public string? InUse(string? home) => null;

    private GitAnswer Answer(GitAsk ask)
    {
        lock (_gate)
        {
            _asked.Add(ask);
            foreach (var (when, answer) in _rows)
                if (when(ask)) return answer(ask);
        }

        throw new InvalidOperationException($"no row of the scripted git answers `git {string.Join(' ', ask.Arguments)}` in {ask.Folder}.");
    }
}
