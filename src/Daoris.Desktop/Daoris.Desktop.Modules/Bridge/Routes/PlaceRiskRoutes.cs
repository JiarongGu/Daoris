using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A place's risk and the owner's marks (<see cref="PlaceRiskBook"/>), set once by the owner: the window's row in a place's Settings, `daoris
/// settings risk` its terminal twin, and Ask Daoris's press, which goes on the owner's card in every mode (<see cref="PipeOwnerOnly"/>). The
/// roles' admission, the reporting line and the go-ahead read it at each act, so nothing restarts.
/// </summary>
public sealed class PlaceRiskRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// One change to the record, as the page and the verb send it: <c>repository</c> and/or <c>workspace</c> (neither is the machine), a
    /// <c>risk</c>, and the <c>mark</c> and <c>unmark</c> lists, judged by <see cref="PlaceRiskBook.Apply"/> before anything is written.
    /// </summary>
    [DriverRoute("SET_PLACE_RISK", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_PLACE_RISK", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_PLACE_RISK", nameof(PipeReads.PlaceRisk))]
    [NotAlready("SET_PLACE_RISK", "adds and takes away marks beside the risk, so one press may change a part while another stands")]
    [Publishes("SET_PLACE_RISK", StateScopes.Driver)]
    private object? SetPlaceRisk(SetPlaceRisk press)
    {
        var edit = new PlaceRiskEdit
        {
            Repository = press.Repository, Workspace = press.Workspace, Risk = press.Risk, Mark = press.Mark ?? [], Unmark = press.Unmark ?? [],
        };
        // The record's refusals come first, in its words, before anything is written.
        DriverConfig.Load(_loop.ConfigPath).PlaceRisk.Apply(edit);
        _context.Change(config => config with { PlaceRisk = config.PlaceRisk.Apply(edit) });
        return DriverRoutes.State(_loop);
    }
}

/// <summary>The press of a place's risk and marks: neither place named is the machine's record.</summary>
public sealed record SetPlaceRisk(string? Repository, string? Workspace, string? Risk, IReadOnlyList<string>? Mark, IReadOnlyList<string>? Unmark);
