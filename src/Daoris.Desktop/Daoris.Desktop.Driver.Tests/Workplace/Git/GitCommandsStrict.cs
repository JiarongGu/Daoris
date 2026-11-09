using System.Runtime.CompilerServices;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The tests run the git gate strict, so a start whose command or verb has no row fails the test that starts it.</summary>
internal static class GitCommandsStrict
{
    [ModuleInitializer]
    internal static void Strict() => GitStartGate.Strict = true;
}
