using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A session record opened in a tree as a start opens one: inside the start's hold on its repository, let go once the record is open.</summary>
internal static class HeldOpens
{
    public static async Task<(string? SessionId, string Message)> OpenAsync(ServiceClient client, string home, string quest, string adapter, string? profile, string tree)
    {
        var held = StartHold.TryTake(home, tree) ?? throw new InvalidOperationException($"`{tree}`'s repository is held alone.");
        using (held.Lock)
        {
            return await client.OpenSessionAsync(quest, adapter, null, profile, tree, null, starting: held);
        }
    }
}
