using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>Why the press kept a unit it was asked to remove (KEEP1e, the cleanup design §3.3), beside each kind's own codes.</summary>
public static class SpaceKept
{
    /// <summary>It changed since the list the person read: a folder that held no files of its own now does, or a day no longer past.</summary>
    public const string Changed = "changed";

    /// <summary>It is no longer there.</summary>
    public const string Gone = "gone";

    /// <summary>Its name is none the list could have shown: never joined into a path.</summary>
    public const string Refused = "refused";

    /// <summary>Its removal failed part way; what went stays gone, and the rest waits.</summary>
    public const string Failed = "failed";

    /// <summary>The browser runs, so its caches stay (the cleanup design §3.2).</summary>
    public const string Running = "running";

    /// <summary>No Daoris runs to say whether the browser does, so its caches stay (§3.2).</summary>
    public const string NoShell = "no-shell";

    /// <summary>A program holds it: its rename aside was refused.</summary>
    public const string InUse = "in-use";

    /// <summary>An entry of this build names it, so it is no earlier build's (§3.5).</summary>
    public const string Named = "named";

    /// <summary>An update is under way, so its folders stay (§3.5).</summary>
    public const string Updating = "updating";
}
