namespace Daoris.Driver;

/// <summary>
/// The work a watch's run starts beside its looks, each pass on the pool so no look waits on its git or on a conversation's start: the tidy
/// of session branches, the wake of the conversations that wait for an account and the following of every line.
/// It keeps no state: the run holds each pass's task and begins the next only once that one has ended.
/// </summary>
internal sealed class WatchBeside(DriverWatch watch, ServiceClient service, string home)
{
    private LookStages Stages => watch.Stages;

    private LookSaid Lines => watch.Lines;

    private MachineLog? Log => watch.Log;

    private ReviewDesk? Reviews => watch.Reviews;

    private Task FirstScreenAsync(CancellationToken ct) => watch.FirstScreenAsync(ct);
    /// <summary>
    /// One pass of the <c>limits</c> stage, started on the pool so the look beside it does not wait on a
    /// conversation's start. A closing loop ends it with nothing said; a failure past its own catches is written to the log.
    /// </summary>
    internal Task LimitsAsync(Func<DriverConfig, CancellationToken, Task> limits, DriverConfig config, CancellationToken ct) =>
        Task.Run(async () =>
        {
            try
            {
                await Stages.TimeAsync("limits", () => limits(config, ct), beside: true).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The loop closed.
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or IOException or UnauthorizedAccessException)
            {
                // The next look's pass asks again.
            }
        }, CancellationToken.None);

    /// <summary>
    /// One tidy pass, started on the pool so the look beside it does not wait on its git. A closing loop ends it
    /// with nothing said; what else stops it is said once, in a later look's report.
    /// </summary>
    internal Task<IReadOnlyList<string>> TidyAsync(BranchTidying tidy, CancellationToken ct)
    {
        tidy.Began();
        return Task.Run<IReadOnlyList<string>>(async () =>
        {
            try
            {
                await FirstScreenAsync(ct).ConfigureAwait(false);
                // A review step in progress keeps the tree it shows from, as a session in use keeps its own.
                return await Stages.TimeAsync(
                        "tidy", () => tidy.LookAsync(service, Log, ct, token => ReviewServed.ReviewingAsync(Reviews, service, token)), beside: true)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return [];
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or IOException or UnauthorizedAccessException)
            {
                return [Lines.Retry("tidy", $"tidy  the session branches were not looked at this time, and are looked at again later: {error.Message}")];
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// Every repository with a checkout here, followed from its line: what the pass says, for a look's report,
    /// or null where the service does not answer, so the next look tries again. Started on the pool, so the look beside it
    /// does not wait on its git.
    /// </summary>
    internal Task<IReadOnlyList<string>?> FollowEveryLineAsync(DriverConfig config, CancellationToken ct) =>
        Task.Run<IReadOnlyList<string>?>(async () =>
        {
            try
            {
                await FirstScreenAsync(ct).ConfigureAwait(false);
                var report = await Stages.TimeAsync(
                        "follow", () => RegistrationFollow.FollowAsync(new RegistrationWorld(service, home, config), only: null, ct), beside: true)
                    .ConfigureAwait(false);
                // Each line classed as it is made.
                return
                [
                    .. report.Followed.Select(followed => RegistrationFollow.EventLine(followed, Lines)).OfType<string>(),
                    .. report.Refresh is { } refresh
                        ? [Lines.Retry("registry", $"registry  the index was not read again after registering: {refresh}")]
                        : Array.Empty<string>(),
                ];
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // A closing loop, its first-screen wait among what it ends: nothing followed, nothing said.
                return null;
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                return null;
            }
        }, CancellationToken.None);
}
