using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>Which driven starts the layer's Claude run holds and which stay on the transport.</summary>
public sealed class ClaudeDrivenLayerRunTests
{
    private static HeldRun? RunOf(params string[] args)
    {
        var info = new ProcessStartInfo("claude");
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return ClaudeDrivenLayer.Run(null!, new TransportStart(info, StartClass.Background), new SpawnRequest("engine", ".", "http://127.0.0.1:9"));
    }

    [Fact]
    public void A_start_that_hands_the_sources_alone_stays_on_the_transport_the_layer_has_no_field_for_them()
    {
        Assert.Null(RunOf("--setting-sources", "user", "--strict-mcp-config", "-p"));
    }
}
