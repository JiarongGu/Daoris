using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Reading and writing across repositories, the page's `bridge/across.ts` (READ1, D107): how each registered
/// repository's checkout stands, and the screen's half of `daoris driver across` over the same `driver.json`.
/// </summary>
public sealed partial class DriverModule
{
    // Every repository here, whether agents outside it read its checkout and what said so, and what its
    // sessions were declared to write into. The workspaces are the registry's, so this waits for the driver
    // as the lines do.
    [DriverRoute("ACROSS")]
    private async Task<object?> AcrossAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var config = DriverConfig.Load(_loop.ConfigPath);
        return new
        {
            Repositories = snapshot.Repositories
                .OrderBy(known => known.Repository, StringComparer.Ordinal)
                .Select(known =>
                {
                    var reading = AcrossRules.Reading(config, known.Repository, known.Workspace);
                    return new
                    {
                        known.Repository,
                        known.Workspace,
                        Checkout = known.Root is { Length: > 0 },
                        reading.Read,
                        Source = reading.Source.ToString().ToLowerInvariant(),
                        WritesTo = AcrossRules.WritesTo(config, known.Repository),
                    };
                })
                .ToArray(),
        };
    }

    // A repository's reading across, or a workspace's, or either cleared with no `read` — the same file
    // `daoris driver across … read` edits: one truth, two doors (D50).
    [DriverRoute("SET_READ_ACROSS")]
    private object? SetReadAcross(IpcRequest request)
    {
        var repository = Optional(request, "repository");
        var workspace = Optional(request, "workspace");
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("reading across is set for a `repository` or a `workspace` — name one of them.");
        }

        bool? read = request.Payload is { } payload && payload.TryGetProperty("read", out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;
        Change(config => workspace is not null
            ? config.WithWorkspaceReadAcross(workspace, read)
            : config.WithReadAcross(repository!, read));
        return State();
    }

    // A relationship declared, or taken back — the same file `daoris driver across <repo> write-to <other>`
    // edits. A repository naming itself is refused in the driver's own sentence.
    [DriverRoute("SET_WRITE_ACROSS")]
    private object? SetWriteAcross(IpcRequest request)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var to = Optional(request, "to") ?? "";
        var allow = Flag(request, "allow");
        Change(config => config.WithWriteAcross(repository, to, allow));
        return State();
    }
}
