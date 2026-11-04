using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>How a press to start a run was answered (<see cref="SpaceRuns.Start"/>).</summary>
/// <param name="Run">The run started, or the one under way it joined; null where another holds the home's run lock.</param>
/// <param name="Joined">A run was under way in this process, so the press answered its id.</param>
/// <param name="Held">Who holds the run lock elsewhere; null where none, or none named.</param>
public sealed record SpaceStart(SpaceRun? Run, bool Joined, KeepingHolder? Held);
