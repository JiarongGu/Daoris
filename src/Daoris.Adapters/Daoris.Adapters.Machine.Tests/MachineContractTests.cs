using Daoris.Ports.Processes;
using Daoris.Testing;

namespace Daoris.Adapters.Machine.Tests;

public sealed class StartPlacesContractTests : ProcessContract
{
    protected override IMachineAdmission Backend(int places) => new StartPlaces(places, places);
}

public sealed class MachineProcessHostContractTests : ProcessHostContract
{
    protected override IProcessHost Host { get; } = new MachineProcessHost(new StartPlaces(4, 3), (_, _) => null);

    protected override IProcessLiveness Liveness => MachineLiveness.Shared;

    protected override ProcessSpec Says(string text) => new("cmd.exe", ["/d", "/c", "echo " + text], "contract", AdmissionClasses.Background);

    protected override ProcessSpec Exits(int code) => new("cmd.exe", ["/d", "/c", "exit " + code], "contract", AdmissionClasses.Background);

    protected override ProcessSpec Waits() => new("ping.exe", ["-n", "60", "127.0.0.1"], "contract", AdmissionClasses.Background);
}

public sealed class MachineSealContractTests : SealContract
{
    protected override SecretSeal Backend => KeySeal.Port;

    protected override string Foreign => KeySeal.Prefix + "AAAAAAAA";
}
