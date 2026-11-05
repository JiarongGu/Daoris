using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;
using static Daoris.Desktop.GitAnswers;

namespace Daoris.Desktop;

/// <summary>
/// Git's ref acts behind the git domain's doors: a branch or tag made, deleted, renamed, finished or undone, a fetch, and a branch's
/// history edited from objects. Each is a person's press through the one act journal the window's git domain shares.
/// </summary>
internal sealed class GitRefDesk(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    private readonly ActRuns _acts = context.GitActs;

    public async Task<object?> GitRefCreateAsync(GitRefAct act, CancellationToken cancellationToken)
    {
        var repository = act.Repository ?? throw new DriverException("a ref act names its repository.");
        var kind = act.Kind;
        var name = act.Name;
        if (kind is not ("branch" or "tag") || string.IsNullOrWhiteSpace(name)) throw new DriverException("a create names its kind, `branch` or `tag`, and its name.");
        _ = _loop.Service ?? throw NotReady();

        var verb = new GitRefVerb(kind, repository, name, act.At, Yes: true);
        var world = new GitRefWorld(async (_, token) => await CheckoutOfAsync(_loop, repository, act.Workspace, token).ConfigureAwait(false), () => _acts);
        if (act.Said)
        {
            var writer = new StringWriter { NewLine = "\n" };
            var code = await GitRefCommand.ExecuteAsync(verb, world, "terminal", "window", writer, writer, cancellationToken).ConfigureAwait(false);
            var lines = writer.ToString().Split('\n');
            return new { Said = new TreesSaid(lines[^1].Length == 0 ? lines[..^1] : lines, [], code).ToWire() };
        }

        if (await world.CheckoutOf(repository, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var run = await GitRefActs.CreateAsync(_acts, new RefAsk(repository, checkout, kind, name, verb.At), "window", "window", cancellationToken).ConfigureAwait(false);
        return Answered(run, checkout, repository);
    }

    public async Task<object?> GitRefDeleteAsync(GitRefAct act, CancellationToken cancellationToken)
    {
        var repository = act.Repository ?? throw new DriverException("a ref act names its repository.");
        var kind = act.Kind;
        var name = act.Name;
        if (kind is not ("branch" or "tag") || string.IsNullOrWhiteSpace(name)) throw new DriverException("a delete names its kind, `branch` or `tag`, and its name.");
        _ = _loop.Service ?? throw NotReady();

        var verb = new GitRefVerb(kind, repository, name, act.At, Yes: true, Delete: true);
        var world = new GitRefWorld(
            async (_, token) => await CheckoutOfAsync(_loop, repository, act.Workspace, token).ConfigureAwait(false), () => _acts,
            async (_, checkout, token) => (await CanonicalLine.ResolveAsync(checkout, repository, act.Workspace, DriverConfig.Load(_loop.ConfigPath), token).ConfigureAwait(false)).Branch);
        if (act.Said)
        {
            var writer = new StringWriter { NewLine = "\n" };
            var code = await GitRefCommand.ExecuteAsync(verb, world, "terminal", "window", writer, writer, cancellationToken).ConfigureAwait(false);
            var lines = writer.ToString().Split('\n');
            return new { Said = new TreesSaid(lines[^1].Length == 0 ? lines[..^1] : lines, [], code).ToWire() };
        }

        if (await world.CheckoutOf(repository, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var line = await world.LineOf!(repository, checkout, cancellationToken).ConfigureAwait(false);
        var run = await GitRefActs.DeleteAsync(_acts, new RefDeleteAsk(repository, checkout, kind, name, verb.At, line), "window", "window", cancellationToken).ConfigureAwait(false);
        return Answered(run, checkout, repository);
    }

    public async Task<object?> GitRefRenameAsync(GitRefRenameAct act, CancellationToken cancellationToken)
    {
        var repository = act.Repository ?? throw new DriverException("a ref act names its repository.");
        var kind = act.Kind;
        var name = act.Name;
        var to = act.To;
        if (kind is not ("branch" or "tag") || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(to)) throw new DriverException("a rename names its kind, `branch` or `tag`, its name and the name it takes.");
        _ = _loop.Service ?? throw NotReady();

        var verb = new GitRefVerb(kind, repository, name, act.At, Yes: true, RenameTo: to);
        var world = new GitRefWorld(
            async (_, token) => await CheckoutOfAsync(_loop, repository, act.Workspace, token).ConfigureAwait(false), () => _acts,
            async (_, checkout, token) => (await CanonicalLine.ResolveAsync(checkout, repository, act.Workspace, DriverConfig.Load(_loop.ConfigPath), token).ConfigureAwait(false)).Branch);
        if (act.Said)
        {
            var writer = new StringWriter { NewLine = "\n" };
            var code = await GitRefCommand.ExecuteAsync(verb, world, "terminal", "window", writer, writer, cancellationToken).ConfigureAwait(false);
            var lines = writer.ToString().Split('\n');
            return new { Said = new TreesSaid(lines[^1].Length == 0 ? lines[..^1] : lines, [], code).ToWire() };
        }

        if (await world.CheckoutOf(repository, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var line = await world.LineOf!(repository, checkout, cancellationToken).ConfigureAwait(false);
        var run = await GitRefActs.RenameAsync(_acts, new RefRenameAsk(repository, checkout, kind, name, to, verb.At, line), "window", "window", cancellationToken).ConfigureAwait(false);
        return Answered(run, checkout, repository);
    }

    public async Task<object?> GitRefFinishAsync(GitActTarget target, CancellationToken cancellationToken)
    {
        var repository = target.Repository ?? throw new DriverException("a ref act names its repository.");
        var act = target.Act ?? throw new DriverException("a finish names the act it finishes.");
        _ = _loop.Service ?? throw NotReady();
        if (await CheckoutOfAsync(_loop, repository, target.Workspace, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var run = await GitRefActs.FinishAsync(_acts, repository, checkout, act, "window", "window", cancellationToken).ConfigureAwait(false);
        return Answered(run, checkout, repository);
    }

    public async Task<object?> GitRefUndoAsync(GitActTarget target, CancellationToken cancellationToken)
    {
        var repository = target.Repository ?? throw new DriverException("a ref act names its repository.");
        var act = target.Act ?? throw new DriverException("an undo names the act it undoes.");
        _ = _loop.Service ?? throw NotReady();
        if (await CheckoutOfAsync(_loop, repository, target.Workspace, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var run = await GitRefActs.UndoAsync(_acts, repository, checkout, act, "window", "window", cancellationToken).ConfigureAwait(false);
        return Answered(run, checkout, repository);
    }

    public async Task<object?> GitFetchPlanAsync(GitCheckoutRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(_loop, read.Repository, read.Workspace, cancellationToken, (root, token) => GitFetchActs.PlanReadAsync(_loop.Home, root, token));

    public async Task<object?> GitFetchAsync(GitFetchAct act, CancellationToken cancellationToken)
    {
        var repository = act.Repository ?? throw new DriverException("a fetch names its repository.");
        _ = _loop.Service ?? throw NotReady();

        var verb = new GitFetchVerb(repository, Yes: true);
        var world = new GitRefWorld(async (_, token) => await CheckoutOfAsync(_loop, repository, act.Workspace, token).ConfigureAwait(false), () => _acts);
        if (act.Said)
        {
            var writer = new StringWriter { NewLine = "\n" };
            var code = await GitFetchCommand.ExecuteAsync(verb, world, "terminal", "window", writer, writer, cancellationToken).ConfigureAwait(false);
            var lines = writer.ToString().Split('\n');
            return new { Said = new TreesSaid(lines[^1].Length == 0 ? lines[..^1] : lines, [], code).ToWire() };
        }

        if (await world.CheckoutOf(repository, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var run = await GitFetchActs.FetchAsync(_acts, new FetchAsk(repository, checkout, act.Workspace), "window", "window", cancellationToken).ConfigureAwait(false);
        return new
        {
            run.Act, run.Outcome, run.Refused, Said = WithoutRoots(run.Said, [checkout], repository) ?? "",
            Notes = run.Notes is null ? null : WithoutRoots(run.Notes, [checkout], repository),
        };
    }

    public async Task<object?> GitPushPlanAsync(GitCheckoutRead read, CancellationToken cancellationToken) =>
        await GitAnsweredAsync(_loop, read.Repository, read.Workspace, cancellationToken,
            (root, token) => GitPushActs.PlanReadAsync(_loop.Home, read.Repository!, read.Workspace, root, token));

    public async Task<object?> GitPushAsync(GitRefAct act, CancellationToken cancellationToken)
    {
        var repository = act.Repository ?? throw new DriverException("a push names its repository.");
        var kind = act.Kind;
        var name = act.Name;
        if (kind is not ("branch" or "tag") || string.IsNullOrWhiteSpace(name)) throw new DriverException("a push names its kind, `branch` or `tag`, and its name.");
        _ = _loop.Service ?? throw NotReady();

        var verb = new GitPushVerb(repository, kind, name, act.At, Yes: true);
        var world = new GitRefWorld(async (_, token) => await CheckoutOfAsync(_loop, repository, act.Workspace, token).ConfigureAwait(false), () => _acts);
        if (act.Said)
        {
            var writer = new StringWriter { NewLine = "\n" };
            var code = await GitPushCommand.ExecuteAsync(verb, world, "terminal", "window", writer, writer, cancellationToken).ConfigureAwait(false);
            var lines = writer.ToString().Split('\n');
            return new { Said = new TreesSaid(lines[^1].Length == 0 ? lines[..^1] : lines, [], code).ToWire() };
        }

        if (await world.CheckoutOf(repository, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var run = await GitPushActs.PushAsync(_acts, new PushAsk(repository, checkout, kind, name, act.At, act.Workspace), "window", "window", cancellationToken).ConfigureAwait(false);
        return new
        {
            run.Act, run.Outcome, run.Refused, Said = WithoutRoots(run.Said, [checkout], repository) ?? "",
            Notes = run.Notes is null ? null : WithoutRoots(run.Notes, [checkout], repository),
        };
    }

    public async Task<object?> GitHistoryEditAsync(GitHistoryEditAct act, CancellationToken cancellationToken)
    {
        var repository = act.Repository ?? throw new DriverException("a history edit names its repository.");
        var (edit, branch, target) = (act.Edit, act.Branch, act.Target);
        if (edit is not (GitHistoryEdits.Pick or GitHistoryEdits.Revert or GitHistoryEdits.Rebase) || string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(target))
        {
            throw new DriverException("a history edit names its edit (`pick`, `revert` or `rebase`), its branch and its target.");
        }

        // The pipe opens `plan: true` as a look; a payload saying `said` beside it would run as the press, so it is refused.
        if (act.Plan && act.Said) throw new DriverException("a history edit's plan and its words are asked apart.");
        _ = _loop.Service ?? throw NotReady();
        var workspace = act.Workspace;
        var world = new GitHistoryWorld(
            async (_, token) => await CheckoutOfAsync(_loop, repository, workspace, token).ConfigureAwait(false),
            async (_, root, token) => (await CanonicalLine.ResolveAsync(root, repository, workspace, DriverConfig.Load(_loop.ConfigPath), token).ConfigureAwait(false)).Branch,
            () => _acts, _loop.Home);
        var pins = PinsOf(act.Pins);
        if (act.Said)
        {
            var writer = new StringWriter { NewLine = "\n" };
            var code = await GitHistoryCommand.ExecuteAsync(new GitHistoryVerb(edit, repository, branch, target, Yes: true), pins, world, "terminal", "window", writer, writer, cancellationToken)
                .ConfigureAwait(false);
            var lines = writer.ToString().Split('\n');
            return new { Said = new TreesSaid(lines[^1].Length == 0 ? lines[..^1] : lines, [], code).ToWire() };
        }

        if (await world.CheckoutOf(repository, cancellationToken).ConfigureAwait(false) is not { } checkout) return NoCheckout(repository);
        var ask = new HistoryEditAsk(_loop.Home, repository, checkout, edit, branch, target)
        {
            Line = await world.LineOf(repository, checkout, cancellationToken).ConfigureAwait(false), Pins = pins,
        };
        if (act.Plan)
        {
            var (plan, refusal) = await GitHistoryEdits.PlanAsync(ask, cancellationToken).ConfigureAwait(false);
            return plan is null
                ? new { Refused = true, Said = WithoutRoots(refusal, [checkout], repository) ?? "" }
                : new
                {
                    Refused = false, Said = "",
                    Plan = new { plan.Edit, plan.Branch, plan.Tip, plan.Onto, Commits = plan.Commits.Select(each => new { each.Id, each.Subject }), plan.Commands },
                };
        }

        var run = await GitHistoryEdits.EditAsync(_acts, ask, "window", "window", cancellationToken).ConfigureAwait(false);
        return new { run.Run.Act, run.Run.Outcome, run.Run.Refused, Said = WithoutRoots(run.Run.Said, [checkout], repository) ?? "", run.Tip, run.Kept };
    }

    /// <summary>The ids a shown plan pinned, as the page or the verb sends them back; null where none was sent.</summary>
    private static HistoryEditPins? PinsOf(JsonElement? sent) =>
        sent is { } pins && pins.ValueKind == JsonValueKind.Object
        && pins.TryGetProperty("tip", out var tip) && tip.ValueKind == JsonValueKind.String
        && pins.TryGetProperty("onto", out var onto) && onto.ValueKind == JsonValueKind.String
        && pins.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Array
            ? new HistoryEditPins(tip.GetString()!, onto.GetString()!, [.. commits.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)])
            : null;
}
