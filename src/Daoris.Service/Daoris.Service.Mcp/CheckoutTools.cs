using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Ask Daoris's reading of the checkouts (HELPMODE1c4; the Ask Daoris modes design §4.1): one typed tool, a request the driver judges
/// at every call and answers with what was read (<see cref="CheckoutBox"/>). It takes a repository and a path relative to its checkout,
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
        "Ask Daoris only, and only the checkouts the person granted under Ask Daoris's configured access (reading checkouts): each "
        + "readable checkout of this conversation's workspace on this machine, unless they turned it off. Read one, by its repository's "
        + "registered name and a path relative to its checkout: a file's text (at most 200 KB, said when cut) or a folder's entries; or "
        + "how it stands, `git status` or `git branch --list`. Daoris judges each read: a path out of the checkout (`..`, a drive, a link "
        + "leading out), git's own folder and anything git ignores are refused, and so is a checkout not granted, saying why. It changes "
        + "nothing, and never runs another git command.")]
    public Task<string> ReadAsync(
        [Description("The repository, by its registered name.")] string repository,
        [Description("A path relative to the checkout's top, with forward slashes (`src/Game.cs`): a file to read or a folder to list; none for the top. Only for `read`.")] string? path = null,
        [Description("`read` (the default: the path's file or folder), `status` (`git status`) or `branches` (`git branch --list`); the last two name no path.")] string? what = null,
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
