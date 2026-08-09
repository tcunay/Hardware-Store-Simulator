using HardwareStore.Infrastructure.Systems;
using HardwareStore.Infrastructure.View.Systems;

namespace HardwareStore.Infrastructure.View
{
    public sealed class BindViewFeature : Feature
    {
        public BindViewFeature(ISystemFactory systems)
        {
            Add(systems.Create<BindEntityViewFromSceneSystem>());
            Add(systems.Create<BindEntityViewFromPrefabSystem>());
        }
    }
}
