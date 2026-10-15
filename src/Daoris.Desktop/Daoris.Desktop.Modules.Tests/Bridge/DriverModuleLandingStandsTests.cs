using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// ACCEPTALL1g (D167's 2026-10-15 amendment point 2, D145 §3): <c>LANDING</c> says whether the branch Accept lands on already stands,
/// an entry's or a chain's branch an earlier landing made, which the press moves on instead of making, so the review stops promising a
/// new branch there. The plan's own reading over real git is the driver's <c>EntryLandingTests</c>.
/// </summary>
public sealed class DriverModuleLandingStandsTests
{
    private static readonly LandingGate Open = LandingGate.Open("engine");

    [Fact]
    public void A_branch_that_already_stands_goes_on_the_wire_as_standing()
    {
        var read = Read(TreesRoutes.Planned("s1", new LandingPlan(LandingForm.Branch, "feature/x", LandingSource.Workspace) { Stands = true }, 0, Open, null));

        Assert.Equal(("s1", "branch", "feature/x"), (read.GetProperty("session").GetString(), read.GetProperty("form").GetString(), read.GetProperty("target").GetString()));
        Assert.True(read.GetProperty("stands").GetBoolean());
    }

    [Fact]
    public void A_branch_the_press_makes_or_a_merge_does_not_stand()
    {
        var made = Read(TreesRoutes.Planned("s1", new LandingPlan(LandingForm.Branch, "feature/x", LandingSource.Workspace), 0, Open, null));
        var merged = Read(TreesRoutes.Planned("s1", new LandingPlan(LandingForm.Merge, "main", LandingSource.Default), 2, Open, "feature/y"));

        Assert.False(made.GetProperty("stands").GetBoolean());
        Assert.False(merged.GetProperty("stands").GetBoolean());
        // The rest of the answer is as before.
        Assert.Equal((2, "feature/y"), (merged.GetProperty("uncommitted").GetInt32(), merged.GetProperty("leftOutOf").GetString()));
    }

    private static JsonElement Read(object wire) => JsonDocument.Parse(JsonSerializer.Serialize(wire, JsonSerializerOptions.Web)).RootElement;
}
