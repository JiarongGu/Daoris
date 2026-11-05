using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>A press whose grounds are being read before its card (§15.2 item 9): what ends it meanwhile, and what it was sent with.</summary>
internal sealed class PipeReserved(
    IpcRequest request, PipeBinding binding, TaskCompletionSource<string> line, PipeConnection connection, PipePlace place, DateTimeOffset asked,
    bool hold = false)
{
    /// <summary>That it was sent with <c>hold: true</c> (RPC1g1): its card says the Confirm also lets its terminal hold what it opens.</summary>
    public bool Hold { get; } = hold;

    public IpcRequest Request { get; } = request;

    public PipeBinding Binding { get; } = binding;

    public TaskCompletionSource<string> Line { get; } = line;

    public PipeConnection Connection { get; } = connection;

    public PipePlace Place { get; } = place;

    public DateTimeOffset Asked { get; } = asked;

    /// <summary>Cancelled where a withdrawal or a close ends it while its grounds are read.</summary>
    public CancellationTokenSource Reading { get; } = new();
}
