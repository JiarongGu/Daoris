namespace Daoris.Driver;

/// <summary>
/// What a run's checks' newest attempts left in its folder (FABRIC-PIPELINE1d; the pipeline design §4.4): each result as the executor wrote it,
/// read by the run's view and the line's read alike. A result that is not there or does not read is none.
/// </summary>
public static class CheckResults
{
    /// <summary>The result of <paramref name="node"/>'s newest check attempt on <paramref name="run"/>; null where there is none or it does not read.</summary>
    public static VerifyResult? Newest(string home, string run, NodeRecord? record, string node)
    {
        if (record?.Attempts.LastOrDefault(attempt => attempt.Node == node && attempt.Plugin == NodeKinds.Check) is not { } attempt) return null;
        try
        {
            return VerifyResult.Read(VerifyFiles.ResultOf(home, run, attempt.Call.Invocation));
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>The gate each failed check named, by node, from its newest attempt's result.</summary>
    public static Dictionary<string, string> Failures(string home, string run, NodeGraph graph, NodeRecord? record)
    {
        var named = new Dictionary<string, string>(StringComparer.Ordinal);
        if (record is null) return named;
        foreach (var node in graph.Nodes.Where(each => each.Kind == NodeKinds.Check && record.Statuses.GetValueOrDefault(each.Id)?.State == NodeStates.Failed))
        {
            if (Newest(home, run, record, node.Id)?.Failed is { } gate) named[node.Id] = gate;
        }

        return named;
    }
}
