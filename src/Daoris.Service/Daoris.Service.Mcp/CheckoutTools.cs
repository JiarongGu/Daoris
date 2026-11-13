using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Ask Daoris's reading of the checkouts (the Ask Daoris modes design §4.1): one typed tool, a request the driver judges
/// at every call and answers with what it read (<see cref="CheckoutBox"/>). It takes a repository and a path relative to its checkout,
/// never a path beyond it, and reads only the checkouts the person grants, which the driver reads at every call. It serves every
/// agent alike, in place of the permission rules only Claude took.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box reads.</param>
/// <param name="box">Where the request is written and its answer read, under the home.</param>
[McpServerToolType]
public sealed class CheckoutTools(IntakeScope? intake = null, SessionLedger? ledger = null, CheckoutBox? box = null)
{
    [McpServerTool(Name = "checkout_read")]
    [Description(
        "Ask Daoris only, and only checkouts the person granted under Ask Daoris's configured access: read one by its registered "
        + "name and a path relative to its checkout (a file's text, at most 200 KB, or a folder's entries), or how it stands, "
        + "`git status` or `git branch --list`. Daoris judges each read: a path out of the checkout, git's own folder, anything git "
        + "ignores and a checkout not granted are refused. It changes nothing, and never runs another git command.")]
    public Task<string> ReadAsync(
        [Description("The repository, by its registered name.")] string repository,
        [Description("Path relative to the checkout's top, forward slashes: a file or folder; none for the top. Only for `read`.")] string? path = null,
        [Description("`read` (default), `status` or `branches`; the last two name no path.")] string? what = null,
        CancellationToken ct = default) =>
        ReadCheckoutAsync(string.IsNullOrWhiteSpace(what) ? CheckoutBox.Read : what.Trim(), repository, path, ct);

    private async Task<string> ReadCheckoutAsync(string operation, string? repository, string? path, CancellationToken ct)
    {
        var checkouts = box ?? CheckoutBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return await checkouts.ReadAsync(record, session, operation, repository, path, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
    }
}
