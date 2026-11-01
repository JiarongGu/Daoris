using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// What the step editor reads (WORKFLOWEDIT1a): the saved workflows with the places that choose them and the presets built in
/// (<c>WORKFLOW_LIST</c>), one version as saved with its line and problems (<c>WORKFLOW_SHOW</c>), and what a step graph may hold
/// (<c>WORKFLOW_PALETTE</c>). All three read this machine's home and publish nothing. The list carries each default's purpose, order,
/// whether the set-up offers it and its delivery (SETUPUX1a).
/// </summary>
public sealed class WorkflowEditRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    [DriverRoute("WORKFLOW_LIST")]
    [ByEvent("WORKFLOW_LIST", Untold = "the workflow files under the home and the choices in the driver's file")]
    private Task<object?> WorkflowListAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var (saved, presets) = WorkflowOperations.Listing(_loop.Home, DriverConfig.Load(_loop.ConfigPath));
        return Task.FromResult<object?>(new
        {
            Workflows = saved.Select(each => new
            {
                each.Id,
                each.Name,
                each.Problem,
                each.Versions,
                each.Schema,
                each.Digest,
                each.Line,
                each.Pipeline,
                Chosen = each.Chosen.Select(scope => new { scope.Level, scope.Name, scope.Role, scope.Kind }).ToArray(),
            }).ToArray(),
            Presets = presets.Select(each => new
            {
                each.Id, each.Name, each.Graph, each.Line, Preset = true, each.Body,
                Names = each.Names is { } names ? new { names.En, names.Zh } : null,
                For = each.Default is { } shipped ? new { shipped.For.En, shipped.For.Zh } : null,
                Order = each.Default?.Order,
                SetUp = each.Default?.SetUp,
                Delivery = each.Default is { } own
                    ? new
                    {
                        own.Delivery.Method, own.Delivery.Accept, own.Delivery.Plugin, own.Delivery.Request,
                        Names = new { own.DeliveryWords.Names.En, own.DeliveryWords.Names.Zh },
                        For = new { own.DeliveryWords.For.En, own.DeliveryWords.For.Zh },
                    }
                    : null,
            }).ToArray(),
            // What WORKFLOW_USE can be asked beside a plain press (SETUPUX1c); a driver before it leaves this out.
            Use = new[] { "plan" },
        });
    }

    [DriverRoute("WORKFLOW_SHOW")]
    [ByEvent("WORKFLOW_SHOW", Untold = "a workflow's file under the home")]
    private Task<object?> WorkflowShowAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = Optional(request, "id")?.Trim();
        if (string.IsNullOrEmpty(id)) throw new DriverException("a workflow is shown by its `id` — name it.");
        WorkflowShown shown;
        try
        {
            shown = WorkflowOperations.Show(_loop.Home, id, Number(request, "version") is { } version ? (int)version : null);
        }
        catch (ChoiceWords refused)
        {
            throw new DriverException(refused.Message);
        }

        return Task.FromResult<object?>(new
        {
            shown.Id,
            shown.Name,
            shown.Version,
            shown.Newest,
            shown.Versions,
            shown.At,
            shown.Door,
            shown.Digest,
            shown.Schema,
            shown.Line,
            Body = shown.Body,
            Problems = shown.Problems.Select(each => new { each.Code, each.Wire, each.Node, each.Words }).ToArray(),
        });
    }

    [DriverRoute("WORKFLOW_PALETTE")]
    [ByEvent("WORKFLOW_PALETTE", Untold = "the plugins' manifests under the home")]
    private Task<object?> WorkflowPaletteAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var catalog = PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names);
        var palette = WorkflowPalette.Build(catalog.Contributing.Select(entry => (entry.Manifest.Id, entry.Manifest.Steps)));
        return Task.FromResult<object?>(palette);
    }
}
