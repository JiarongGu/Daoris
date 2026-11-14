using Daoris.Ports.Git;
using Daoris.Testing;

namespace Daoris.Testing.Tests;

public sealed class ScriptedSshKeyContractTests : SshKeyFormatContract
{
    protected override SshKeyConverter Convert => new ScriptedSshKeys().Convert;
}
