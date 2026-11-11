using System.Globalization;

namespace Daoris.Driver;

/// <summary>A size as a person reads it, said the same by every door that names one: a clear's, a copy's bound, a manifest's.</summary>
public static class ByteSizes
{
    /// <summary>In bytes, KB, MB or GB of 1024.</summary>
    public static string Said(long bytes) => bytes switch
    {
        1 => "1 byte",
        < 1024 => $"{bytes.ToString(CultureInfo.InvariantCulture)} bytes",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        < 1024L * 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.#} MB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024 * 1024):0.##} GB"),
    };
}
