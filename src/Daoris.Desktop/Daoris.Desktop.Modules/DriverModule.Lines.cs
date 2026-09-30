using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Each repository's line and how its work lands, the page's `bridge/lines.ts` (MOD5): the lines and
/// landing rules as set and as they resolve (WSR1, WSR2), and the clean-up of the branches sessions and
/// landings left (WSR3).
/// </summary>
public sealed partial class DriverModule
{
    // A repository's line, or a workspace's (WSR2), or either cleared with no branch — the same
    // file `daoris driver line` edits: one truth, two doors (D50).
    [DriverRoute("SET_LINE")]
    private object? SetLine(IpcRequest request)
    {
        var branch = Optional(request, "branch");
        var repository = Optional(request, "repository");
        var workspace = Optional(request, "workspace");
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a line is set for a `repository` or a `workspace` — name one of them.");
        }

        Change(config => workspace is not null
            ? config.WithWorkspaceLine(workspace, branch)
            : config.WithLine(repository!, branch));
        return State();
    }

    // Every repository's line here and what said so (WSR2), for the screen. The checkouts are
    // the registry's, so this waits for the driver like the review does.
    [DriverRoute("LINES")]
    private async Task<object?> LinesAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var config = DriverConfig.Load(_loop.ConfigPath);
        var lines = await CanonicalLine.OfAsync(
            config,
            snapshot.Repositories
                .OrderBy(known => known.Repository, StringComparer.Ordinal)
                .Select(known => (known.Repository, (string?)known.Workspace, known.Root)),
            cancellationToken).ConfigureAwait(false);
        return new
        {
            Lines = lines.Select(line => new { line.Repository, line.Workspace, line.Branch, line.Source }).ToArray(),
            // How each one's work lands (WSR1), from the same file and the same circles.
            Landings = lines.Select(line => (line, landing: LandingRules.Choose(config, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    pair.landing.Rule.Form,
                    pair.landing.Rule.Pattern,
                    pair.landing.Rule.Tidy,
                    pair.landing.Rule.Plugin,
                    pair.landing.Source,
                })
                .ToArray(),
        };
    }

    // A repository's landing rule or a workspace's, or either cleared with no form — the same file
    // `daoris driver landing` edits: one truth, two doors (D50).
    [DriverRoute("SET_LANDING")]
    private object? SetLanding(IpcRequest request)
    {
        var repository = Optional(request, "repository");
        var workspace = Optional(request, "workspace");
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a landing rule is set for a `repository` or a `workspace` — name one of them.");
        }

        var rule = Optional(request, "form") is { } form
            ? new LandingRule(form, Optional(request, "pattern"), Flag(request, "tidy"), Optional(request, "plugin"))
            : null;
        // The rule's plugin must be able to land work here, said as `daoris driver landing` says it (D100);
        // the shape is the file's own question, asked first by the edit below.
        if (rule is { Plugin: { } plugin } && LandingRules.Problem(rule) is null
            && LandingRules.PluginProblem(plugin, PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names)) is { } problem)
        {
            throw new DriverException(problem);
        }

        Change(config => workspace is not null
            ? config.WithWorkspaceLanding(workspace, rule)
            : config.WithLanding(repository!, rule));
        return State();
    }

    // The clean-up (WSR3, D88): every session branch here with what it holds — then, on the
    // person's press, those the proof clears, and only those the page listed to go (`only`).
    /// <summary>
    /// The clean-up's list, or its press (WSR3, D88), over every repository with a checkout here. A tree a
    /// session still running or waiting names is kept whatever it holds. Beside the session branches, the
    /// branches landings made (WSR5): each goes where its work reached the line, by the same press.
    /// </summary>
    [DriverRoute("SWEEP_PLAN")]
    [DriverRoute("SWEEP")]
    private async Task<object?> SweepAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var registry = await service.RegistryAsync(cancellationToken);
        var inUse = (await service.ActiveSessionsAsync(cancellationToken))
            .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var repositories = registry
            .Where(row => !string.IsNullOrWhiteSpace(row.Root))
            .OrderBy(row => row.Repository, StringComparer.Ordinal)
            .Select(row => (row.Repository, (string?)row.Workspace, row.Root))
            .ToList();
        var trees = new SessionTrees(_loop.Home);

        object Row(SweepItem item) => new
        {
            item.Repository, item.Workspace, item.Branch, HasTree = item.Tree is not null,
            item.Kind, item.Commits, item.Where, item.Detail, item.Removable,
        };

        // A landed branch's row: what the proof found, and the files that keep it where some do.
        object Landed(LandedItem item) => new
        {
            item.Repository, item.Workspace, item.Branch, item.Kind, item.Where, item.Files, item.Detail,
            item.PullRequest, item.Commits, item.Removable,
        };

        if (request.Type == "SWEEP_PLAN")
        {
            var plan = await trees.CleanPlanAsync(repositories, inUse, cancellationToken);
            return new { Branches = plan.Sessions.Select(Row).ToArray(), Landed = plan.Landed.Select(Landed).ToArray() };
        }

        HashSet<string>? only = null;
        if (request.Payload is { } payload && payload.TryGetProperty("only", out var named) && named.ValueKind == JsonValueKind.Array)
        {
            only = named.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        }

        var done = await trees.CleanAsync(repositories, inUse, only, cancellationToken);
        _loop.Nudge();
        return new
        {
            Results = done.Sessions.Select(result => new { Branch = Row(result.Item), result.Removed, result.Message }).ToArray(),
            Landed = done.Landed.Select(result => new { Branch = Landed(result.Item), result.Removed, result.Message }).ToArray(),
            Removed = done.Sessions.Count(result => result.Removed) + done.Landed.Count(result => result.Removed),
        };
    }
}
