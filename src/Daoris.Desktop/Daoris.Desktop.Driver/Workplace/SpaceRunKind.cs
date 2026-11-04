using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One kind of a run, as <c>SYSTEM_SPACE</c>'s <c>run</c> says it: waiting, running or done, with what it removed and kept.</summary>
public sealed record SpaceRunKind(string Id, string State, int Removed, long Bytes, IReadOnlyDictionary<string, int> Kept);
