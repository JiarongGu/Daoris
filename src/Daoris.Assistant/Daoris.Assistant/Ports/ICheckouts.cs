namespace Daoris.Assistant.Ports;

/// <summary>
/// The checkouts on this machine, each with where it is, and one file's read answered. Whether a read is allowed is the core's
/// judgement; the port answers what the checkout holds.
/// </summary>
public interface ICheckouts
{
    /// <summary>The checkouts, by name in ordinal order.</summary>
    Task<IReadOnlyList<CheckoutPlace>> ListAsync(CancellationToken cancel);

    /// <summary>
    /// The file at <paramref name="path"/>, relative to the checkout: answered with its text, missing, or the checkout unknown. A path
    /// that is rooted or climbs out of the checkout is refused (<see cref="ArgumentException"/>) before any read.
    /// </summary>
    Task<CheckoutReading> ReadAsync(string checkout, string path, CancellationToken cancel);
}

/// <summary>A checkout: its name and where it is.</summary>
public sealed record CheckoutPlace(string Name, string Place);

/// <summary>One read's answer: the checkout and path asked, its state, and the text where the checkout answered it.</summary>
public sealed record CheckoutReading(string Checkout, string Path, CheckoutReadState State)
{
    public string? Text { get; init; }
}

/// <summary>How the checkout answers a read.</summary>
public enum CheckoutReadState
{
    Answered,
    Missing,
    UnknownCheckout,
}
