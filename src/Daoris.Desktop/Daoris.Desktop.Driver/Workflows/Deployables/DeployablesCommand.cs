namespace Daoris.Driver;

/// <summary>
/// <c>daoris repository deployables &lt;repository&gt;</c>: a read of the deployables a registered repository's line declares in its
/// <c>daoris.json</c>, as git objects at the line's commit, with each one's release node from the repository's values. It writes nothing.
/// </summary>
public static class DeployablesCommand
{
    public const string Usage = "usage: daoris repository deployables <repository>";

    /// <summary>The read said on <paramref name="output"/>: 0 where everything reads, 1 where an entry or a value does not, 2 where nothing could be read.</summary>
    public static async Task<int> RunAsync(string repository, TextWriter output, IRegistrationWorld world, DriverConfig config, CancellationToken ct = default)
    {
        var rows = await world.RegistrationsAsync(ct).ConfigureAwait(false);
        var row = rows.FirstOrDefault(each => string.Equals(each.Repository, repository.Trim(), StringComparison.OrdinalIgnoreCase));
        if (row is null || string.IsNullOrWhiteSpace(row.Root))
        {
            await output.WriteLineAsync($"deployables  `{repository.Trim()}` has no checkout on this machine's registry, so no line is read.").ConfigureAwait(false);
            return 2;
        }

        var files = await world.ReadLineAsync(row, ct).ConfigureAwait(false);
        if (files.Line is null || files.Commit is null || files.FileProblem is not null)
        {
            await output.WriteLineAsync($"deployables  `{row.Repository}`'s line does not read: {files.FileProblem ?? files.Problem ?? "git names none"}.").ConfigureAwait(false);
            return 2;
        }

        var declared = DeployableDeclaration.Read(files.Manifest, files.ManifestBlob);
        var targets = DeployableTargets.Judge(declared, config.WorkflowValues.TryGetValue(row.Repository, out var values) ? values : null);
        foreach (var line in DeployablesWords.Lines(row.Repository, files.Line, files.Commit, declared, targets))
        {
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }

        return declared.Problems.Count + targets.Problems.Count > 0 ? 1 : 0;
    }
}
