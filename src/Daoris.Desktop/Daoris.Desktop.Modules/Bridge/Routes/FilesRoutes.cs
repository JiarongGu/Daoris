using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A repository's files git does not manage, the page's `bridge/repositories/files.ts`: each top's effective rule and the layers
/// below it, and the person's rules and permissions written there. Every route is the window's alone (<see cref="PipeClass.Window"/>):
/// a terminal reaches the same files by `daoris files`, and an agent's session is refused both. No answer carries a machine path.
/// </summary>
public sealed class FilesRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    // The rule file read for the page at the commit the checkout's line stands at. Git and the walks answer it, so it is told by event.
    // A repository with no checkout here, or one git cannot read, is an answer with a code the page words.
    [DriverRoute("FILES", PipeClass.Window)]
    [Owner("FILES", Repository = "repository")]
    [ByEvent("FILES", Untold = "the checkout's ignored files and its line's tip, which git answers and no scope tells")]
    private async Task<object?> FilesAsync(FilesRead read, CancellationToken cancellationToken)
    {
        var repository = ActField.Needs(read.Repository, "repository");
        if (await GitAnswers.CheckoutOfAsync(_loop, repository, null, cancellationToken).ConfigureAwait(false) is not { } root)
        {
            return new { Refusal = "no-checkout" };
        }

        try
        {
            var commit = await FilesLooks.CommitAsync(root, repository, DriverConfig.Load(_loop.ConfigPath), cancellationToken).ConfigureAwait(false);
            return new { Look = await FilesLooks.ReadAsync(_loop.Home, root, repository, commit, cancellationToken).ConfigureAwait(false) };
        }
        catch (DriverException)
        {
            return new { Refusal = "unreadable" };
        }
    }

    // *Read again*: the analysis reads the repository now and replaces its found layer. Nothing else is written.
    [DriverRoute("FILES_READ", PipeClass.Window)]
    [Owner("FILES_READ", Repository = "repository")]
    [Publishes("FILES_READ", StateScopes.Files)]
    private async Task<object?> FilesReadAsync(FilesRead press, CancellationToken cancellationToken)
    {
        var repository = ActField.Needs(press.Repository, "repository");
        if (await GitAnswers.CheckoutOfAsync(_loop, repository, null, cancellationToken).ConfigureAwait(false) is not { } root)
        {
            return new FilesAnswered(false, "no-checkout", $"{repository} has no checkout on this machine, so nothing was read.");
        }

        try
        {
            var commit = await FilesLooks.CommitAsync(root, repository, DriverConfig.Load(_loop.ConfigPath), cancellationToken).ConfigureAwait(false);
            await FilesLooks.ReadAgainAsync(_loop.Home, root, repository, commit, cancellationToken).ConfigureAwait(false);
            return new FilesAnswered(true, null, null);
        }
        catch (DriverException)
        {
            return new FilesAnswered(false, "unreadable", $"{repository} could not be read.");
        }
    }

    // A rule of the person's own, added at the end of their rules so it wins; a later one over the same paths changes it.
    [DriverRoute("FILES_SET", PipeClass.Window)]
    [Owner("FILES_SET", Repository = "repository")]
    [Publishes("FILES_SET", StateScopes.Files)]
    private Task<object?> FilesSetAsync(FilesSetPress press, CancellationToken cancellationToken) => Written(press.Repository, repository =>
        FilesEdits.Set(_loop.Home, repository, press.Machine, ActField.Needs(press.Kind, "kind"), press.Paths ?? [], press.Copy) is not null);

    // The person's rules naming these paths, taken out.
    [DriverRoute("FILES_REMOVE", PipeClass.Window)]
    [Owner("FILES_REMOVE", Repository = "repository")]
    [Publishes("FILES_REMOVE", StateScopes.Files)]
    private Task<object?> FilesRemoveAsync(FilesRemovePress press, CancellationToken cancellationToken) => Written(press.Repository, repository =>
        FilesEdits.Remove(_loop.Home, repository, press.Machine, press.Paths ?? []) > 0);

    // The person's file rewritten in git's syntax, every path it reads as written worded as the person chose, in one write.
    [DriverRoute("FILES_SETTLE", PipeClass.Window)]
    [Owner("FILES_SETTLE", Repository = "repository")]
    [Publishes("FILES_SETTLE", StateScopes.Files)]
    private Task<object?> FilesSettleAsync(FilesSettlePress press, CancellationToken cancellationToken) => Written(press.Repository, _ =>
    {
        FilesEdits.Settle(_loop.Home, press.Wordings ?? []);
        return true;
    });

    // A permission added or taken out: the one press that allows what an ask holds writes the person's allow with its maker, and
    // taking it back removes it. Refused while the person's file does not read, so no allow is made over a restriction unseen.
    [DriverRoute("FILES_PERMIT", PipeClass.Window)]
    [Owner("FILES_PERMIT", Repository = "repository")]
    [Publishes("FILES_PERMIT", StateScopes.Files)]
    private Task<object?> FilesPermitAsync(FilesPermitPress press, CancellationToken cancellationToken) => Written(press.Repository, repository =>
    {
        var add = press.Act switch
        {
            "add" => true,
            "remove" => false,
            var other => throw new DriverException($"a permission is `add`ed or `remove`d, not `{other}`."),
        };
        return FilesEdits.Permit(_loop.Home, repository, press.Machine, add, ActField.Needs(press.List, "list"), ActField.Needs(press.Rule, "rule"), "person");
    });

    /// <summary>One write of the person's layer, answered as data: a file that does not read is `unread`, any other refusal the driver's sentence.</summary>
    private Task<object?> Written(string? named, Func<string, bool> write)
    {
        var repository = ActField.Needs(named, "repository");
        if (FilesEdits.Unread(_loop.Home) is { } why) return Task.FromResult<object?>(new FilesAnswered(false, "unread", $"{why}, so nothing was written over it."));
        try
        {
            return Task.FromResult<object?>(write(repository) ? new FilesAnswered(true, null, null) : new FilesAnswered(false, "unchanged", "Nothing changed."));
        }
        catch (DriverException refused)
        {
            return Task.FromResult<object?>(new FilesAnswered(false, "refused", refused.Message));
        }
    }
}
