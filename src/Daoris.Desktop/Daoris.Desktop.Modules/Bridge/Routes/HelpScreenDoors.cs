using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>The doors Ask Daoris's Apply goes through, each the code a screen's own route runs; <paramref name="service"/> is null before the loop's is up.</summary>
internal sealed class HelpScreenDoors(DriverContext context, ServiceClient? service) : IHelpDoors
{
    private readonly DriverLoop _loop = context.Loop;

    public void Change(Func<DriverConfig, DriverConfig> edit) => context.Change(edit);

    // The ask composer's door, `POST /api/asks`, with its review choice and words; an environment the workspace does not declare is refused first.
    public async Task<AskAnswer> AskAsync(string workspace, string sentence, string? review, string? reviewWords, CancellationToken ct, string? shortTitle = null)
    {
        var asks = service ?? throw NotReady();
        if (review is not null
            && await AskReviewCommand.UndeclaredAsync(review, workspace, new AskReviewWorld(asks, () => DriverConfig.Load(_loop.ConfigPath)), ct)
                .ConfigureAwait(false) is { } undeclared)
        {
            return new AskAnswer(false, undeclared, null, null);
        }

        return await asks.AskAsync(workspace, sentence, [], [], null, ct, review, reviewWords, shortTitle: shortTitle).ConfigureAwait(false);
    }
}
