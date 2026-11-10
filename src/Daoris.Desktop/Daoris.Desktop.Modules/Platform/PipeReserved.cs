using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>A press whose grounds are being read before its card: what ends it meanwhile, and what it is sent with.</summary>
internal sealed class PipeReserved(
    IpcRequest request, PipeBinding binding, TaskCompletionSource<string> line, PipeConnection connection, PipePlace place, DateTimeOffset asked,
    bool hold = false, PipeAsker? asker = null)
{
    /// <summary>That it is sent with <c>hold: true</c>: its card says the Confirm also lets its terminal hold what it opens.</summary>
    public bool Hold { get; } = hold;

    public PipeAsker Asker { get; } = asker ?? connection.Asker;

    /// <summary>The place an Ask Daoris press named, read before its card; null for every other press.</summary>
    public Func<IpcRequest, CancellationToken, Task<Daoris.Driver.HelpPressPlace?>>? PlaceOf { get; init; }

    public IpcRequest Request { get; } = request;

    public PipeBinding Binding { get; } = binding;

    public TaskCompletionSource<string> Line { get; } = line;

    public PipeConnection Connection { get; } = connection;

    public PipePlace Place { get; } = place;

    public DateTimeOffset Asked { get; } = asked;

    /// <summary>Cancelled where a withdrawal or a close ends it while its grounds are read.</summary>
    public CancellationTokenSource Reading { get; } = new();
}
