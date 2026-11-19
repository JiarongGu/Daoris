namespace Daoris.Driver;

/// <summary>
/// `daoris update --fetch`, an install's update reading Daoris's own releases (<see cref="DaorisReleases"/>): the newest published
/// release, when it is newer than the build running, fetched and held to its checksum, unpacked beside the install as a staged build
/// (<c>update/staged/</c>), and held to the build's own manifest by <see cref="StagedBuild.Check"/> as a published stage is. It
/// installs nothing: what is staged is the application's to install when idle, or at the person's word, as any staged build is.
/// </summary>
public static class UpdateFetch
{
    /// <summary>The release fetched and staged, or the sentence saying why not.</summary>
    /// <param name="install">The install the build is staged beside.</param>
    /// <param name="running">The version this build runs, which a release must be newer than.</param>
    /// <param name="fetch">How it is fetched: the network and this machine's build, unless a test names its own.</param>
    /// <param name="process">The process the staging is named by: this one, unless a test names another.</param>
    /// <returns>0 staged, or nothing newer to stage · 1 refused, with the sentence in <paramref name="said"/>.</returns>
    public static async Task<(int Code, string Said)> RunAsync(
        string install, string? running, CancellationToken ct, ReleaseFetch? fetch = null, int? process = null)
    {
        var said = new System.Text.StringBuilder();
        var code = await RunAsync(install, running, line => said.AppendLine($"  {line}"), ct, fetch, process).ConfigureAwait(false);
        return (code, said.ToString());
    }

    private static async Task<int> RunAsync(
        string install, string? running, Action<string> write, CancellationToken ct, ReleaseFetch? fetch, int? process)
    {
        void Refuse(string sentence) => write($"update: {sentence}");

        if (StagedBuild.UnderWay(install))
        {
            Refuse("a swap is under way, so nothing is fetched until it ends.");
            return 1;
        }

        if (StagedBuild.IsStaged(install))
        {
            var held = StagedBuild.Read(install, out _);
            Refuse($"{(held is null ? "a build" : $"Daoris {held.Version}")} is already staged beside {install}; it is installed first, and a newer release is "
                + "fetched after it.");
            return 1;
        }

        try
        {
            var version = await ReleaseChannels.LatestAsync(DaorisReleases.Channel, ct, fetch?.Transport).ConfigureAwait(false);
            if (!DaorisReleases.IsNewer(version, running))
            {
                write($"update: Daoris {version} is the newest release, and this build ({running ?? "unknown"}) is not older: nothing was fetched.");
                return 0;
            }

            write($"update: Daoris {version} is published, and this build is {running ?? "unknown"}.");
            var where = StagedBuild.StagedOf(install);
            var staged = await ReleaseChannels.StageAsync(
                DaorisReleases.Channel, where, version, write, ct, fetch?.Transport, fetch?.Platform, null, process).ConfigureAwait(false);
            ReleaseChannels.Publish(staged, write);

            var problem = StagedBuild.Check(install);
            var manifest = problem is null ? StagedBuild.Read(install, out problem) : null;
            if (problem is null && manifest?.Version != version)
            {
                problem = new("version", $"the package says it is Daoris {manifest?.Version ?? "of no version"}, and the release is {version}.");
            }

            if (problem is not null)
            {
                StagedBuild.Unstage(install);
                Refuse($"Daoris {version}'s package is not a build this install installs, and it was removed: {problem.Sentence}");
                return 1;
            }

            write($"update: Daoris {version} is staged beside {install}. It is installed when idle; `daoris update` says where it stands, "
                + "`daoris update --now` installs it at once.");
            return 0;
        }
        catch (DriverException error)
        {
            Refuse(error.Message);
            return 1;
        }
    }
}
