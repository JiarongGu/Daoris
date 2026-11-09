using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// Every declaration's name, held here alone: a repository's four at its top, and the home's. The members are read-only fields, never
/// constants, so a use of one outside the store is a reference the compiler resolves to this symbol and never folds into a string.
/// It stands in the lowest subdomain, since the service's and the knowledge's readers name it.
/// </summary>
public static class DeclarationNames
{
    /// <summary>The repository's manifest.</summary>
    public static readonly string Manifest = "daoris.json";

    /// <summary>The repository's rule file, its own layer of the file rules.</summary>
    public static readonly string Rules = "daoris.files.json";

    /// <summary>The repository's gates.</summary>
    public static readonly string Gates = "daoris.gates.json";

    /// <summary>The repository's lanes and rooms.</summary>
    public static readonly string Lanes = "daoris.lanes.json";

    /// <summary>The person's choices, under the home's data.</summary>
    public static readonly string DriverConfig = "driver.json";

    /// <summary>The person's own layer of the file rules, under the home's data.</summary>
    public static readonly string PersonRules = "files.json";

    /// <summary>The layer the analysis found, under the home's data.</summary>
    public static readonly string FoundRules = "files-found.json";

    /// <summary>The asks the file rules hold, under the home's data.</summary>
    public static readonly string FileAsks = "files-asks.json";

    /// <summary>The folder of the saved workflows, under the home's data.</summary>
    public static readonly string Workflows = "workflows";

    /// <summary>A repository's declarations, each at its top.</summary>
    public static readonly ImmutableArray<string> Repository = [Manifest, Rules, Gates, Lanes];

    /// <summary>The door's exclusion: a pathspec for each of the repository's declarations, so a git search reads none of them.</summary>
    internal static readonly ImmutableArray<string> Excluded = [.. Repository.Select(name => ":(exclude,literal)" + name)];
}
