namespace Daoris.Driver;

/// <summary>
/// A run's identity under Current's graphs: a digest of the unit alone, so a run begins when a graph naming a plugin's performed node first
/// governs the unit and an edit of that graph begins none; never at a wake, a look, a restart or a new revision either (a moved revision
/// lapses the bindings it moved in the same run). What an edit changes is each node's place (<see cref="NodeGraphs.Places"/>), which lapses
/// that node and what rests on it alone.
/// </summary>
public static class NodeRuns
{
    public static string Of(string unit) =>
        "run-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"node-run\n{unit}\n")))[..24];

    /// <summary>
    /// A run's name from the unit and the graph governing it, so each edit begins another: what a scope, a receipt and a firing recorded
    /// under it name, judged on the graph now (<see cref="WireRun.Legacy"/>) so nothing lapses or fires again.
    /// </summary>
    public static string Of(string unit, string graphDigest) =>
        "run-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"node-run\n{unit}\n{graphDigest}\n")))[..24];
}
