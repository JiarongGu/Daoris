namespace Daoris.Driver;

/// <summary>Where a pull request a plugin's step opened is written on its branch's landing record (G8).</summary>
public interface IPullRequestRecords
{
    /// <summary>Keeps <paramref name="address"/> and <paramref name="plugin"/> on the standing landing of <paramref name="branch"/>; false where there is none or it already holds them.</summary>
    bool Keep(string repository, string branch, string plugin, string address);
}

/// <summary>The landing record of a home, <c>landings.json</c>, as <see cref="IPullRequestRecords"/>.</summary>
public sealed class PullRequestRecords(string home) : IPullRequestRecords
{
    /// <inheritdoc/>
    public bool Keep(string repository, string branch, string plugin, string address)
    {
        var landings = new LandedBranches(home);
        if (landings.Of(repository, branch) is not { } held
            || (string.Equals(held.PullRequest, address, StringComparison.Ordinal) && string.Equals(held.Plugin, plugin, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        landings.PullRequested(repository, branch, plugin, address);
        return true;
    }
}
