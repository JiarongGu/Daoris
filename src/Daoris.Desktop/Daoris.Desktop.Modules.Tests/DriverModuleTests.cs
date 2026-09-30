using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The session-control surface's host half (D46 §6, D49) — the largest thing the page talks to, and
/// the one with the most to get wrong.
/// </summary>
/// <remarks>
/// What stays here is the dispatcher's own contract (MOD5): a malformed call and an unknown route. Each
/// domain's routes are tested beside it, in the class named for its partial (`DriverModuleTreesTests` for
/// `DriverModule.Trees.cs`), and the table's shape in `DriverModuleRoutesTests`.
/// </remarks>
public sealed class DriverModuleTests : DriverModuleBridge
{
    [Fact]
    public async Task A_request_missing_what_it_needs_is_refused_rather_than_defaulted()
    {
        // A drivable toggle with no repository would otherwise opt in "" — a row nobody can see and
        // nothing can remove.
        Assert.NotEmpty(await RefusalAsync(Module(), "SET_DRIVABLE", new { drivable = true }));
    }

    [Fact]
    public async Task An_unknown_request_type_is_refused()
    {
        Assert.Contains("NO_ROUTE", await RefusalAsync(Module(), "FROBNICATE"));
    }

    /// <summary>
    /// A route is its exact name, as the switch's labels were: the table is ordinal, so a name in another
    /// case is a route nobody declared (MOD5).
    /// </summary>
    [Fact]
    public async Task A_route_in_another_case_is_one_nobody_declared()
    {
        Assert.Contains("NO_ROUTE", await RefusalAsync(Module(), "state"));
    }
}
