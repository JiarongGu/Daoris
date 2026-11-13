using System.Text;
using Daoris.Ports.Git;

namespace Daoris.Testing;

/// <summary>
/// An <see cref="IGit"/> a test scripts: the first row that answers an ask answers it, the rest go <see cref="Beyond"/>, and with
/// nothing beyond an unscripted ask throws, naming it, so a start nobody scripted is never a silent success.
/// </summary>
public sealed class ScriptedGit : IGit
{
    private readonly List<Func<GitAsk, GitAnswer?>> _rows = [];
    private readonly List<GitAsk> _asked = [];
    private readonly List<Action<GitAsk>> _seen = [];
    private readonly Lock _gate = new();
    private IGit? _beyond;

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

    public ScriptedGit On(Func<GitAsk, bool> when, Func<GitAsk, GitAnswer> answer) => On(ask => when(ask) ? answer(ask) : null);

    /// <summary>Answers each ask <paramref name="answer"/> gives an answer for, asking it once per ask.</summary>
    public ScriptedGit On(Func<GitAsk, GitAnswer?> answer)
    {
        lock (_gate) _rows.Add(answer);
        return this;
    }

    /// <summary>Hands each ask no row answers to <paramref name="git"/>.</summary>
    public ScriptedGit Beyond(IGit git)
    {
        lock (_gate) _beyond = git;
        return this;
    }

    /// <summary>Tells <paramref name="seen"/> of each ask as it is made, before it is answered; it may be called from several threads.</summary>
    public ScriptedGit Seen(Action<GitAsk> seen)
    {
        lock (_gate) _seen.Add(seen);
        return this;
    }

    public Task<GitAnswer> RunAsync(GitAsk ask, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Scripted(ask) is { } answer ? Task.FromResult(answer) : Next(ask).RunAsync(ask, ct);
    }

    public Task<GitBytes> ReadBytesAsync(GitAsk ask, int limit, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Scripted(ask) is not { } answer) return Next(ask).ReadBytesAsync(ask, limit, ct);
        var bytes = Encoding.UTF8.GetBytes(answer.Output);
        var cut = bytes.Length > Math.Max(0, limit);
        return Task.FromResult(cut
            ? new GitBytes(null, bytes[..Math.Max(0, limit)], answer.Error, Cut: true)
            : new GitBytes(answer.Exit, bytes, answer.Error));
    }

    public Task<int?> ReadAsync(GitAsk ask, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Scripted(ask) is not { } answer) return Next(ask).ReadAsync(ask, take, ct);
        if (answer.Exit is null) return Task.FromResult<int?>(null);
        return Task.FromResult<int?>(answer.Output.Length == 0 || take(answer.Output.AsMemory()) ? answer.Exit : 0);
    }

    public string? InUse(string? home) => _beyond?.InUse(home);

    private GitAnswer? Scripted(GitAsk ask)
    {
        Action<GitAsk>[] seen;
        lock (_gate)
        {
            _asked.Add(ask);
            seen = [.. _seen];
        }

        foreach (var each in seen) each(ask);
        Func<GitAsk, GitAnswer?>[] rows;
        lock (_gate) rows = [.. _rows];
        foreach (var row in rows)
            if (row(ask) is { } answer) return answer;

        return null;
    }

    private IGit Next(GitAsk ask) =>
        _beyond ?? throw new InvalidOperationException($"no row of the scripted git answers `git {string.Join(' ', ask.Arguments)}` in {ask.Folder}.");
}
