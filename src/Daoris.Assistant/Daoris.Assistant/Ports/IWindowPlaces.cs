namespace Daoris.Assistant.Ports;

/// <summary>
/// The window's places a go link names (its views, domains and parts) and the doors it has, each a press view's sentence. The
/// window declares them; the assistant reads them, so nothing here names the assistant.
/// </summary>
public interface IWindowPlaces
{
    /// <summary>The places, each name once within its kind.</summary>
    IReadOnlyList<WindowPlace> Places { get; }

    /// <summary>The doors, each on a view among <see cref="Places"/>.</summary>
    IReadOnlyList<WindowDoor> Doors { get; }
}

/// <summary>One place a go link names: its kind and its name.</summary>
public sealed record WindowPlace(WindowPlaceKind Kind, string Name);

/// <summary>What a place is.</summary>
public enum WindowPlaceKind
{
    View,
    Domain,
    Part,
}

/// <summary>One door: the view it is on and the sentence its press view says.</summary>
public sealed record WindowDoor(string View, string Sentence);
