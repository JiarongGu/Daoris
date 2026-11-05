namespace Daoris.Driver;

/// <summary>
/// The accounts a hand-over's agent and account are judged by (the agent-provider design §4.3, row m): one read for the conversation's chat
/// runner, the curve's move and the loop's agent ask alike, so the three never answer one question three ways.
/// </summary>
internal static class HandOverAccounts
{
    /// <summary>
    /// The accounts the roster holds for <paramref name="agent"/>, or null where it has no such agent or one that holds no conversations;
    /// none for an agent without an account pool.
    /// </summary>
    public static IReadOnlyList<string>? Of(AdapterSet adapters, string harnessesHome, string agent)
    {
        try
        {
            var resolved = adapters.Resolve(agent);
            if (!resolved.Interactive) return null;
            return resolved.Toolchain is not null ? HarnessSettings.Profiles(harnessesHome, adapters.Registry.OwnerOfAgent(resolved.Name).Value) : [];
        }
        catch (DriverException)
        {
            return null;
        }
    }
}
