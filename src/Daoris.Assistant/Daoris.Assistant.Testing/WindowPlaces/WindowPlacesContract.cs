using Daoris.Assistant.Ports;
using Xunit;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The cases every <see cref="IWindowPlaces"/> passes: <see cref="FixtureWindowPlaces"/> in the assistant's suite, the modules' places
/// and press views in theirs.
/// </summary>
public abstract class WindowPlacesContract
{
    /// <summary>The window's places under test.</summary>
    protected abstract IWindowPlaces Backend { get; }

    [Fact]
    public void The_window_has_views()
    {
        Assert.Contains(Backend.Places, place => place.Kind == WindowPlaceKind.View);
    }

    [Fact]
    public void Each_place_is_named_once_within_its_kind_and_never_blank()
    {
        Assert.All(Backend.Places, place => Assert.False(string.IsNullOrWhiteSpace(place.Name), $"a {place.Kind} has a blank name"));
        Assert.Empty(Backend.Places.GroupBy(place => (place.Kind, place.Name)).Where(group => group.Count() > 1).Select(group => group.Key));
    }

    [Fact]
    public void Each_door_is_on_a_view_among_the_places_and_says_a_sentence()
    {
        var views = Backend.Places.Where(place => place.Kind == WindowPlaceKind.View).Select(place => place.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(Backend.Doors, door =>
        {
            Assert.True(views.Contains(door.View), $"the door '{door.Sentence}' is on '{door.View}', which is no view");
            Assert.False(string.IsNullOrWhiteSpace(door.Sentence), $"a door on '{door.View}' says nothing");
        });
    }
}
