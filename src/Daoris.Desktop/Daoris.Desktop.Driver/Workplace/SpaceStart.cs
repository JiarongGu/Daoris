using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>How a press to start a run is answered (<see cref="SpaceRuns.Start"/>).</summary>
/// <param name="Run">The run started, or the one under way it joined; null where another holds the home's run lock.</param>
/// <param name="Joined">A run is under way in this process, so the press answers its id.</param>
/// <param name="Held">Who holds the run lock elsewhere; null where none, or none named.</param>
public sealed record SpaceStart(SpaceRun? Run, bool Joined, KeepingHolder? Held);
