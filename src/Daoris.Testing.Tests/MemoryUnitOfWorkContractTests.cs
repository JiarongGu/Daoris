using Daoris.Ports.Store;

namespace Daoris.Testing.Tests;

public sealed class MemoryUnitOfWorkContractTests : UnitOfWorkContract
{
    protected override string Place(string name) => MemoryUnitOfWork.MemoryPlace(name);

    protected override IDesktopStoreEngine Open(string place, DesktopStoreEngineOptions options) => MemoryUnitOfWork.Open(place, options);

    protected override bool Woken(string place) => MemoryUnitOfWork.Woken(place);

    protected override void Release(string place) => MemoryUnitOfWork.Forget(place);
}
