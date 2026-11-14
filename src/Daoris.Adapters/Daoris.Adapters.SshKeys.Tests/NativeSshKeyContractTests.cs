using Daoris.Ports.Git;
using Daoris.Testing;

namespace Daoris.Adapters.SshKeys.Tests;

public sealed class NativeSshKeyContractTests : SshKeyFormatContract
{
    protected override SshKeyConverter Convert => SshKeyFormats.Convert;
}
