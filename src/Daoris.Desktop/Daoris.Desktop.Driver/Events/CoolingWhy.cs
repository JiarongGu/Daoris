namespace Daoris.Driver;

/// <summary>Why a cool-off lasts until then, by code: the page's <c>harness.cooling.why.*</c>.</summary>
public static class CoolingWhy
{
    /// <summary>The agent named the time.</summary>
    public const string Stated = "stated";

    /// <summary>The agent named the time without a zone, read in this machine's.</summary>
    public const string Assumed = "assumed";

    /// <summary>The agent named no time: Daoris's default.</summary>
    public const string Default = "default";

    /// <summary>The agent named a date more than 8 days off: Daoris's default.</summary>
    public const string NotBelieved = "notBelieved";

    /// <summary>The code for an entry, read as <see cref="CoolingWords"/> reads it for its sentence.</summary>
    public static string Of(CoolingEntry entry) =>
        entry.Stated ? (entry.AssumedZone ? Assumed : Stated) : entry.NotBelieved ? NotBelieved : Default;
}
