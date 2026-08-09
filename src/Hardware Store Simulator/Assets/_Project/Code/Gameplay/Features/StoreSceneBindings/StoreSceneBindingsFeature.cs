using HardwareStore.Gameplay.Features.StoreSceneBindings.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.StoreSceneBindings
{
    public sealed class StoreSceneBindingsFeature : Feature
    {
        public StoreSceneBindingsFeature(ISystemFactory systems) =>
            Add(systems.Create<ValidateStoreSceneBindingsSystem>());
    }
}
