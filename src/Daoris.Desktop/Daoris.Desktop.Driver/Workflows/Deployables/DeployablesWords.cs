namespace Daoris.Driver;

/// <summary>
/// <c>daoris repository deployables &lt;repository&gt;</c>'s lines: the deployables the repository's line declares, each with what it
/// provides and consumes and the release node its target is keyed by, then what does not read.
/// </summary>
public static class DeployablesWords
{
    /// <summary>The read of <paramref name="repository"/> on <paramref name="line"/> at <paramref name="commit"/>, one line per fact.</summary>
    public static IReadOnlyList<string> Lines(string repository, string line, string commit, DeclaredDeployables declared, DeployableTargetsRead targets)
    {
        var at = $"on `{line}` at `{TextBounds.ShortCommit(commit, 7)}`";
        var lines = new List<string>();
        if (declared.Items.Count == 0)
        {
            lines.Add($"deployables  {repository}: none {at}. Name each in {DeclarationNames.Manifest}'s `{DeployableDeclaration.Key}`, "
                + $"and its release node in {WorkflowValuesKey.Key}.");
        }
        else
        {
            var blob = declared.Blob is { } id ? $", {DeclarationNames.Manifest} blob `{id}`" : "";
            lines.Add($"deployables  {repository}: {declared.Items.Count} {at}{blob}");
            foreach (var target in targets.Targets)
            {
                var deployable = target.Deployable;
                lines.Add($"  {deployable.Id}  paths {string.Join(", ", deployable.Paths)}{Uses("provides", deployable.Provides)}{Uses("consumes", deployable.Consumes)}");
                lines.Add(target.Standing switch
                {
                    DeployableStanding.Undeclared => "    no release node names it: its target is undeclared",
                    DeployableStanding.Several => $"    named by {Nodes(target.Nodes)}, several in one workflow: name it on one",
                    _ => $"    released by {Nodes(target.Nodes)}",
                });
            }
        }

        lines.AddRange(declared.Problems.Concat(targets.Problems).Select(problem => $"  ! {problem}"));
        return lines;
    }

    private static string Uses(string verb, IReadOnlyList<DeployableUse> uses) =>
        uses.Count == 0 ? "" : $"  {verb} {string.Join(", ", uses.Select(use => $"{use.Resource} ({use.Mode})"))}";

    private static string Nodes(IReadOnlyList<DeployableNode> nodes) =>
        string.Join("; ", nodes.Select(node => $"{node.Workflow}/{node.Node}, deploying on {node.DeploysOn}"
            + (node.DeploysOnDeclared ? "" : " (undeclared, so every exposing act waits)")));
}
