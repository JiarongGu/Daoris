using Daoris.Ports.Processes;
using Xunit;

namespace Daoris.Testing;

/// <summary>
/// The cases every <see cref="IMachineAdmission"/> must pass, written once against the port: the scripted fake and the machine's
/// adapter each hand a backend of a number of places, so a start a test's fake admits is admitted as the machine admits it.
/// </summary>
public abstract class ProcessContract
{
    /// <summary>A backend that lends a person's starts <paramref name="places"/> places.</summary>
    protected abstract IMachineAdmission Backend(int places);

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task A_free_place_is_lent_at_once_under_the_class_asked()
    {
        var placing = Backend(1).PlaceAsync(AdmissionClasses.Person, CancellationToken.None);

        Assert.True(placing.IsCompleted);
        await using var place = await placing;
        Assert.Equal(AdmissionClasses.Person, place.AdmissionClass);
    }

    [Fact]
    public async Task A_full_admission_lends_the_next_place_when_one_is_given_back()
    {
        var admission = Backend(1);
        var first = await admission.PlaceAsync(AdmissionClasses.Person, CancellationToken.None);
        var second = admission.PlaceAsync(AdmissionClasses.Person, CancellationToken.None).AsTask();
        Assert.False(second.IsCompleted);

        await first.DisposeAsync();

        await using var placed = await second.WaitAsync(Patience);
        Assert.Equal(AdmissionClasses.Person, placed.AdmissionClass);
    }

    [Fact]
    public async Task A_wait_given_up_throws_takes_no_place_and_the_next_goes_on()
    {
        var admission = Backend(1);
        var first = await admission.PlaceAsync(AdmissionClasses.Person, CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var given = admission.PlaceAsync(AdmissionClasses.Person, cancel.Token).AsTask();
        var after = admission.PlaceAsync(AdmissionClasses.Person, CancellationToken.None).AsTask();

        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => given);
        await first.DisposeAsync();

        await using var placed = await after.WaitAsync(Patience);
    }

    [Fact]
    public async Task An_ask_with_a_cancelled_token_is_not_placed()
    {
        var admission = Backend(1);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await admission.PlaceAsync(AdmissionClasses.Person, cancel.Token));

        var placing = admission.PlaceAsync(AdmissionClasses.Person, CancellationToken.None);
        Assert.True(placing.IsCompleted);
        await using var place = await placing;
    }

    [Fact]
    public async Task A_place_given_back_twice_frees_one_place()
    {
        var admission = Backend(1);
        var first = await admission.PlaceAsync(AdmissionClasses.Person, CancellationToken.None);
        await first.DisposeAsync();
        await first.DisposeAsync();

        var held = admission.PlaceAsync(AdmissionClasses.Person, CancellationToken.None);
        var next = admission.PlaceAsync(AdmissionClasses.Person, CancellationToken.None);

        Assert.True(held.IsCompleted);
        Assert.False(next.IsCompleted);
        await (await held).DisposeAsync();
        await using var placed = await next.AsTask().WaitAsync(Patience);
    }
}
