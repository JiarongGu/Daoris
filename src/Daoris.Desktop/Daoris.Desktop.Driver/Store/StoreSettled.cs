namespace Daoris.Driver;

/// <summary>
/// Whether a start's confirmation is settled (the design §13.5, §19.7): read by whoever knows the install's swap journal and handed to the
/// store as a reading, since the store is below the install and names none of it. Every import and every migration that raises the store's
/// oldest-writer stamp runs only once it is, so a build rolled back before it confirmed meets every file as it was and a store it can
/// still write.
/// </summary>
/// <param name="Is">True where nothing can still roll this start back.</param>
/// <param name="Said">Where it is not, why, in words the start says once: what it was waiting on.</param>
public sealed record StoreSettled(bool Is, string? Said = null)
{
    /// <summary>Nothing can roll this start back.</summary>
    public static readonly StoreSettled Yes = new(true);

    /// <summary>A start that is not settled, and the reason.</summary>
    public static StoreSettled Not(string said) => new(false, said);
}
