using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The agents' answer to where the <see cref="ChangeTopics.Accounts"/> topic's files are (FOUNDATION1 mod-vii, the design §14.3): what a held
/// conversation's wait is computed from, the cool-offs and the harnesses' wiring, so an account freed early wakes the look's limits stage
/// (LIMITRESUME1c). Installed when the driver's assembly loads, before any watch is built.
/// </summary>
internal static class AccountsHomeTopic
{
    [ModuleInitializer]
    internal static void Install() => HomeTopics.Answer(
        ChangeTopics.Accounts, home => [HomePart.File(AccountCooling.PathOf(home)), HomePart.File(DaorisHome.Entry(home, HomeEntry.Harnesses))]);
}
