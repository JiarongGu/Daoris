using Daoris.Ports.Processes;
using Daoris.Testing;

namespace Daoris.Adapters.Machine.Tests;

public sealed class StartPlacesContractTests : ProcessContract
{
    protected override IMachineAdmission Backend(int places) => new StartPlaces(places, places);
}

public sealed class MachineSealContractTests : SealContract
{
    protected override SecretSeal Backend => KeySeal.Port;

    protected override string Foreign => KeySeal.Prefix + "AAAAAAAA";
}
