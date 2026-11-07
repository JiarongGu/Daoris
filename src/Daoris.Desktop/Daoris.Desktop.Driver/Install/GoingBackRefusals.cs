namespace Daoris.Driver;

/// <summary>Why installing a staged build over the running one is a going back refused, as <see cref="UpdateRefusal"/> asks it.</summary>
internal interface IGoingBackRefusal
{
    /// <summary>The refusal's code and sentence for <paramref name="staged"/> over <paramref name="running"/> on <paramref name="home"/>; null where none is.</summary>
    (string Code, string Sentence)? Refused(string home, string? staged, string? running);
}

/// <summary>The refusal of a going back the update's doors ask: the deliveries it waits on are Workplace's, which installs it.</summary>
internal static class GoingBackRefusals
{
    private static IGoingBackRefusal? _current;

    internal static IGoingBackRefusal Current
    {
        get => _current ?? throw new InvalidOperationException("no refusal of a going back is installed: Workplace installs it when the driver loads");
        set => _current = value;
    }
}
