using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A set-up step's session asks Daoris to serve its build and says what it showed (REVIEWENV1b; the review environment design
/// §2.6). Allowed only to a set-up step's own session, which its connector names; the verdict has no tool, since it is the
/// person's alone.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, where the build is served and the set-up said on the session's record.</param>
[McpServerToolType]
public sealed class ReviewTools(IntakeScope? intake = null, SessionLedger? ledger = null)
{
    [McpServerTool(Name = "review_serve")]
    [Description(
        "For a set-up step only: ask Daoris to serve the build you made in your tree to your tab of Daoris's browser, at the "
        + "environment's address, rather than stopping or taking over anything the person runs. Name the folder the build "
        + "wrote, from your tree's root, and the address the environment declares. Daoris answers whether it serves it, and "
        + "the set-up you say next with review_ready names the folder.")]
    public async Task<string> ServeForReviewAsync(
        [Description("The folder your build wrote, from your tree's root with forward slashes, such as dist/app.")] string folder,
        [Description("The environment's address, an absolute http or https origin such as http://localhost:4200.")] string address,
        CancellationToken ct = default) =>
        ledger is null
            ? NoSession
            : (await ledger.ServeAsync(intake?.Session, folder, address, DateTimeOffset.UtcNow, ct).ConfigureAwait(false)).Message;

    [McpServerTool(Name = "review_ready")]
    [Description(
        "For a set-up step only: say what you set up for the person's review, once it is shown: where to look, what it shows, "
        + "and how to show it again by hand. Say it before you close your quest done; a set-up step's done is refused until "
        + "one is said. Daoris reads the commit your tree holds when you end and keeps the set-up with it, and the person then "
        + "says whether it is right. Say it again in a later turn when you show it again.")]
    public async Task<string> ReadyForReviewAsync(
        [Description("Where to look: the address your tab is at, an absolute http or https address.")] string look,
        [Description("What you showed and what to look at, in at most 300 characters.")] string shows,
        [Description("How to show it again by hand: the address and the clicks, in at most 600 characters.")] string again,
        [Description("Only for work that needed a process of its own: the command, quoted from the procedure.")] string? run = null,
        CancellationToken ct = default) =>
        ledger is null
            ? NoSession
            : (await ledger.ReadyAsync(intake?.Session, look, shows, again, run, DateTimeOffset.UtcNow, ct).ConfigureAwait(false)).Message;

    private const string NoSession =
        "This connector speaks for no session the driver started here, so there is no set-up step to say it for.";
}
