using System.Runtime.CompilerServices;
using Daoris.Ports.Processes;

namespace Daoris.Testing;

/// <summary>What a scripted process does: the lines it writes, its exit, and whether it runs on until it is stopped.</summary>
public sealed record ScriptedAnswer(IReadOnlyList<string> Output, int Exit, bool Waits = false);

/// <summary>One run of <see cref="ScriptedProcesses"/>: its lines written at once, its end at once or at its stop.</summary>
internal sealed class ScriptedRun : IChildRun
{
    private readonly ScriptedProcesses _owner;
    private readonly ScriptedAnswer _answer;
    private readonly TaskCompletionSource<ProcessEnd> _end = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<byte[]> _written = [];

    internal ScriptedRun(ScriptedProcesses owner, int id, DateTimeOffset created, ScriptedAnswer answer)
    {
        _owner = owner;
        _answer = answer;
        Id = id;
        Created = created;
        if (!answer.Waits) Finish(new ProcessEnd(answer.Exit, Stopped: false));
    }

    public int Id { get; }

    public DateTimeOffset Created { get; }

    /// <summary>Each write to its input, whole.</summary>
    internal IReadOnlyList<byte[]> Written
    {
        get
        {
            lock (_written) return [.. _written];
        }
    }

    public async IAsyncEnumerable<ProcessOutput> Output([EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var line in _answer.Output) yield return new ProcessOutput(ProcessStream.Output, line + "\n");
        await _end.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_written) _written.Add(input.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask<ProcessEnd> EndAsync(CancellationToken ct) => new(_end.Task.WaitAsync(ct));

    public ValueTask StopAsync(CancellationToken ct)
    {
        Finish(new ProcessEnd(-1, Stopped: true));
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => StopAsync(CancellationToken.None);

    private void Finish(ProcessEnd end)
    {
        if (_end.TrySetResult(end)) _owner.Ended(Id);
    }
}
