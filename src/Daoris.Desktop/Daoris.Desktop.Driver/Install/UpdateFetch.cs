namespace Daoris.Driver;

/// <summary>
/// `daoris update --fetch`, an install's update reading Daoris's own releases (<see cref="DaorisReleases"/>): the newest published
/// release, when it is newer than the install's build, fetched and held to its checksum, unpacked beside the install in a staging of
/// its own, and held there to the build's own manifest (<see cref="StagedBuild.Check"/>) and to the release's version. Only a build that
/// passed is moved into <c>update/staged/</c>, whole, where the application, which watches that folder, can meet it, and a build another
/// publisher staged meanwhile is kept, never removed. It installs nothing: what is staged is the application's to install when idle, or
/// at the person's word, as any staged build is.
/// </summary>
public static class UpdateFetch
{
    /// <summary>The release fetched and staged, or the sentence saying why not.</summary>
    /// <param name="install">The install the build is staged beside.</param>
    /// <param name="running">The version the install's build is, which a release must be newer than; null where it does not read.</param>
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

            var unpacked = Path.Combine(staged.Staging, "package");
            var manifest = StagedBuild.Read(install, out var problem, unpacked);
            problem ??= manifest is null ? new("manifest", "the package carries no build.json.") : StagedBuild.Check(install, unpacked);
            if (problem is null && manifest?.Version != version)
            {
                problem = new("version", $"the package says it is Daoris {manifest?.Version ?? "of no version"}, and the release is {version}.");
            }

            if (problem is not null)
            {
                ReleaseChannels.Discard(staged);
                Refuse($"Daoris {version}'s package is not a build this install installs, and nothing was staged: {problem.Sentence}");
                return 1;
            }

            // What the first look read may have moved while the package downloaded: a build staged since, or a swap begun.
            if (StagedBuild.UnderWay(install) || StagedBuild.IsStaged(install))
            {
                ReleaseChannels.Discard(staged);
                Refuse($"a build was staged beside {install} while Daoris {version} downloaded, so it is kept and this one was not staged.");
                return 1;
            }

            if (!ReleaseChannels.Publish(staged, write).Published)
            {
                Refuse($"a build was staged beside {install} while Daoris {version} downloaded, so it is kept and this one was not staged.");
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
