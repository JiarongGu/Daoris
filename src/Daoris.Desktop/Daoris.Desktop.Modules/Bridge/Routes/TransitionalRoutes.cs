using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// This machine's list of old records (ONEWAY1ad-ii; D228, the oneway design §1.3): each conversion part's census as the loop last read it, then
/// each transitional read the register keeps with its condition's state, the home's floor, and the build from which the conversion's Undo is
/// refused. The list Ask Daoris's room reads too.
/// </summary>
public sealed class TransitionalRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    // The list, `{}`: it reads the home's receipts, its store's stamp and the files each condition names, and the census as last read; it writes
    // and asks nothing. Answers `{ floor: {build, storeWriter}, floorNeeded, lines: [{kind, id, ended, count, reaches, ends, row, waits}] }`.
    [DriverRoute("KEPT_READS")]
    [ByEvent("KEPT_READS", Untold = "the home's receipts, its store's stamp, the files the register's conditions read and the conversion's census")]
    private object? List(IpcRequest request)
    {
        var floor = HomeFloor.Of(_loop.Home);
        return new
        {
            Floor = new { floor.Build, floor.StoreWriter },
            TransitionalRegister.FloorNeeded,
            Lines = TransitionalRegister.List(_loop.Home, _loop.Conversion.Parts())
                .Select(each => new { each.Kind, each.Id, each.Ended, each.Count, each.Reaches, each.Ends, each.Row, each.Waits }).ToArray(),
        };
    }
}
