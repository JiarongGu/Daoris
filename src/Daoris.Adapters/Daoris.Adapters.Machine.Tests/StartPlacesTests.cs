using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine.Tests;

/// <summary>
/// The machine's places: four starts under way at most, the background three of them, so a person's start always finds one, and a
/// freed place goes to a person waiting before the background waiting.
/// </summary>
public sealed class StartPlacesTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static ValueTask<IAdmittedPlace> Background(StartPlaces admission, CancellationToken ct = default) =>
        admission.PlaceAsync(AdmissionClasses.Background, ct);

    private static ValueTask<IAdmittedPlace> Person(StartPlaces admission) =>
        admission.PlaceAsync(AdmissionClasses.Person, CancellationToken.None);

    private static async Task GiveBackAsync(IEnumerable<ValueTask<IAdmittedPlace>> places)
    {
        foreach (var place in places) await (await place.AsTask().WaitAsync(Patience)).DisposeAsync();
    }

    [Fact]
    public void The_shared_gate_lets_four_start_at_once()
    {
        Assert.Equal(4, StartPlaces.Bound);
        Assert.Equal(StartPlaces.Bound, StartPlaces.Shared.Places);
    }

    [Fact]
    public void The_shared_gate_leaves_one_of_its_four_places_to_a_person()
    {
        Assert.Equal(StartPlaces.Bound - 1, StartPlaces.BackgroundBound);
        Assert.Equal(StartPlaces.BackgroundBound, StartPlaces.Shared.BackgroundPlaces);
    }

    /// <summary>Four slow background starts held, as a git fan-out's are in an antivirus scan, and a person's start still finds its place.</summary>
    [Fact]
    public async Task Four_background_starts_held_leave_a_persons_start_its_place()
    {
        var admission = new StartPlaces(4, 3);
        var git = Enumerable.Range(0, 4).Select(_ => Background(admission)).ToList();

        Assert.Equal(3, git.Count(each => each.IsCompleted));
        var agent = Person(admission);

        Assert.True(agent.IsCompleted);
        Assert.False(git[3].IsCompleted);

        await (await agent).DisposeAsync();
        await GiveBackAsync(git);
        Assert.Equal(4, admission.Free);
    }

    /// <summary>The background bound holds: a fan-out of forty never has more than its three placed.</summary>
    [Fact]
    public async Task Background_starts_never_hold_more_than_their_places()
    {
        var admission = new StartPlaces(4, 3);

        var asks = Enumerable.Range(0, 40).Select(_ => Background(admission)).ToList();

        Assert.Equal(3, asks.Count(each => each.IsCompleted));
        Assert.Equal(1, admission.Free);
        await GiveBackAsync(asks);
        Assert.Equal(4, admission.Free);
    }

    /// <summary>A place that frees goes to a person waiting before the background waiting, whichever asked first.</summary>
    [Fact]
    public async Task A_freed_place_goes_to_the_person_waiting_before_the_background()
    {
        var admission = new StartPlaces(2, 1);
        var person1 = Person(admission);
        var git1 = Background(admission);
        Assert.True(person1.IsCompleted && git1.IsCompleted);

        // The gate is full: the background asks first, then a person.
        var git2 = Background(admission).AsTask();
        var person2 = Person(admission).AsTask();
        Assert.False(git2.IsCompleted || person2.IsCompleted);

        // The background's place frees: both waiting may take it, and the person does.
        await (await git1).DisposeAsync();
        var placed = await person2.WaitAsync(Patience);
        Assert.False(git2.IsCompleted);

        await placed.DisposeAsync();
        await (await person1).DisposeAsync();
        await (await git2.WaitAsync(Patience)).DisposeAsync();
        Assert.Equal(2, admission.Free);
    }

    [Fact]
    public async Task A_background_ask_cancelled_while_it_waits_takes_no_place_and_the_next_goes_on()
    {
        var admission = new StartPlaces(2, 1);
        var first = await Background(admission);
        using var cancel = new CancellationTokenSource();
        var second = Background(admission, cancel.Token).AsTask();
        var third = Background(admission).AsTask();

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        await first.DisposeAsync();
        await (await third.WaitAsync(Patience)).DisposeAsync();
        Assert.Equal(2, admission.Free);
    }
}
